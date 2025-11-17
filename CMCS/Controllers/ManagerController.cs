using CMCS.Data;
using CMCS.Models;
using CMCS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static CMCS.Controllers.CoordinatorController;

namespace CMCS.Controllers
{
    [Authorize(Roles = "Manager,Administrator")]
    public class ManagerController : Controller
    {
        private readonly ILogger<ManagerController> _logger;
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public ManagerController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, ILogger<ManagerController> logger)
        {
            _db = db;
            _userManager = userManager;
            _logger = logger;
        }

        public async Task<IActionResult> Dashboard(string? lecturerName)
        {
            // Managers see claims that need final approval - both pending claims AND coordinator-approved claims
            var claims = _db.Claims
                .Include(c => c.User)
                .Include(c => c.Documents)
                .Include(c => c.ProcessedByUser)
                .Where(c => c.Status == ClaimStatus.Pending || c.Status == ClaimStatus.CoordinatorApproved)
                .AsNoTracking();

            if (!string.IsNullOrWhiteSpace(lecturerName))
            {
                var term = lecturerName.Trim();
                claims = claims.Where(c =>
                    EF.Functions.Like(c.User.FirstName, $"%{term}%") ||
                    EF.Functions.Like(c.User.LastName, $"%{term}%"));
            }

            var pendingClaims = await claims.OrderBy(c => c.SubmitDate).ToListAsync();
            ViewBag.LecturerName = lecturerName ?? "";

            return View(pendingClaims);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FinalApprove(int id)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            var claim = await _db.Claims
                .Include(c => c.User)
                .Include(c => c.Documents)
                .FirstOrDefaultAsync(c => c.ClaimId == id);

            if (claim == null)
            {
                TempData["ErrorMessage"] = "Claim not found.";
                return RedirectToAction(nameof(Dashboard));
            }
            var finalCheck = await PerformFinalVerification(claim);
            if (!finalCheck.IsValid)
            {
                TempData["ErrorMessage"] = $"Final approval failed: {finalCheck.ErrorMessage}";
                return RedirectToAction(nameof(Dashboard));
            }

            // Managers can approve both Pending and CoordinatorApproved claims
            if (claim.Status != ClaimStatus.Pending && claim.Status != ClaimStatus.CoordinatorApproved)
            {
                TempData["ErrorMessage"] = "Only pending or coordinator-approved claims can be finally approved.";
                return RedirectToAction(nameof(Dashboard));
            }

            // Manager final approval
            claim.Status = ClaimStatus.Approved;
            claim.ProcessedDate = DateTime.Now;
            claim.ProcessedByUserId = currentUser.Id;
            claim.ApprovalDate = DateTime.Now;
            claim.RejectionReason = null;

            await _db.SaveChangesAsync();

            // ADD NOTIFICATION HERE
            await CreateClaimNotification(claim, "approved");

            TempData["SuccessMessage"] = $"Claim #{claim.ClaimId} has been finally approved and settled!";
            return RedirectToAction(nameof(Dashboard));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FinalReject(int id, string rejectionReason)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            var claim = await _db.Claims
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.ClaimId == id);

            if (claim == null)
            {
                TempData["ErrorMessage"] = "Claim not found.";
                return RedirectToAction(nameof(Dashboard));
            }

            if (string.IsNullOrWhiteSpace(rejectionReason))
            {
                TempData["ErrorMessage"] = "Rejection reason is required.";
                return RedirectToAction(nameof(Dashboard));
            }

            // Managers can reject both Pending and CoordinatorApproved claims
            if (claim.Status != ClaimStatus.Pending && claim.Status != ClaimStatus.CoordinatorApproved)
            {
                TempData["ErrorMessage"] = "Only pending or coordinator-approved claims can be rejected.";
                return RedirectToAction(nameof(Dashboard));
            }

            claim.Status = ClaimStatus.Rejected;
            claim.ProcessedDate = DateTime.Now;
            claim.ProcessedByUserId = currentUser.Id;
            claim.RejectionReason = rejectionReason.Trim();

            await _db.SaveChangesAsync();

            // ADD NOTIFICATION HERE
            await CreateClaimNotification(claim, "rejected");

            TempData["SuccessMessage"] = $"Claim #{claim.ClaimId} has been rejected.";
            return RedirectToAction(nameof(Dashboard));
        }

        private async Task CreateClaimNotification(Claim claim, string action)
        {
            try
            {
                var notificationService = HttpContext.RequestServices.GetRequiredService<INotificationService>();

                var message = action switch
                {
                    "approved" => $"Your claim #{claim.ClaimId} for {claim.Period} has been approved. Amount: R {claim.Amount:N2}",
                    "rejected" => $"Your claim #{claim.ClaimId} has been rejected. Reason: {claim.RejectionReason}",
                    "submitted" => $"New claim #{claim.ClaimId} submitted for {claim.Period}. Amount: R {claim.Amount:N2}",
                    "coordinator_approved" => $"Your claim #{claim.ClaimId} has been approved by coordinator and sent to manager for final approval",
                    _ => $"Update on your claim #{claim.ClaimId}"
                };

                var notificationType = action switch
                {
                    "approved" or "coordinator_approved" => NotificationType.Success,
                    "rejected" => NotificationType.Error,
                    _ => NotificationType.Info
                };

                await notificationService.CreateNotificationAsync(
                    message,
                    "Claims System",
                    claim.UserId,
                    notificationType
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating notification for claim {ClaimId}", claim.ClaimId);
            }
        }
        private async Task<VerificationResult> PerformFinalVerification(Claim claim)
        {
            var result = new VerificationResult { IsValid = true };

            // Additional manager-level checks
            if (claim.Status != ClaimStatus.CoordinatorApproved && claim.Status != ClaimStatus.Pending)
            {
                result.IsValid = false;
                result.ErrorMessage = "Claim must be coordinator-approved or pending for final approval";
            }
            else if (claim.Amount > 10000) // Large amount requires additional review
            {
                result.IsValid = false;
                result.ErrorMessage = "Claims over R10,000 require additional review";
            }

            return result;
        }
    }
}