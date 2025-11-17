using CMCS.Data;
using CMCS.Models;
using CMCS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CMCS.Controllers
{
    [Authorize(Roles = "Coordinator,Administrator")]
    public class CoordinatorController : Controller
    {
        private readonly ILogger<CoordinatorController> _logger;
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public CoordinatorController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, ILogger<CoordinatorController> logger)
        {
            _db = db;
            _userManager = userManager;
            _logger = logger;
        }

        public async Task<IActionResult> Dashboard(string? lecturerName, ClaimStatus? status)
        {
            // Show both Pending and CoordinatorApproved claims by default
            var claims = _db.Claims
                .Include(c => c.User)
                .Include(c => c.Documents)
                .Include(c => c.ProcessedByUser)
                .AsNoTracking();

            if (!string.IsNullOrWhiteSpace(lecturerName))
            {
                var term = lecturerName.Trim();
                claims = claims.Where(c =>
                    EF.Functions.Like(c.User.FirstName, $"%{term}%") ||
                    EF.Functions.Like(c.User.LastName, $"%{term}%"));
            }

            // If no status filter is applied, show pending and coordinator approved claims
            if (!status.HasValue)
            {
                claims = claims.Where(c => c.Status == ClaimStatus.Pending || c.Status == ClaimStatus.CoordinatorApproved);
            }
            else
            {
                claims = claims.Where(c => c.Status == status.Value);
            }

            var filteredClaims = await claims.OrderByDescending(c => c.SubmitDate).ToListAsync();

            ViewBag.TotalPending = await _db.Claims.CountAsync(c => c.Status == ClaimStatus.Pending);
            ViewBag.CoordinatorApproved = await _db.Claims.CountAsync(c => c.Status == ClaimStatus.CoordinatorApproved);
            ViewBag.WaitingForManager = await _db.Claims.CountAsync(c => c.Status == ClaimStatus.CoordinatorApproved);
            return View(filteredClaims);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
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
            var verificationResult = await VerifyClaimAgainstCriteria(claim);
            if (!verificationResult.IsValid)
            {
                TempData["ErrorMessage"] = $"Claim cannot be approved: {verificationResult.ErrorMessage}";
                return RedirectToAction(nameof(Dashboard));
            }

            if (claim.Status != ClaimStatus.Pending)
            {
                TempData["ErrorMessage"] = "Only pending claims can be approved by coordinators.";
                return RedirectToAction(nameof(Dashboard));
            }

            claim.Status = ClaimStatus.CoordinatorApproved;
            claim.ProcessedByUserId = currentUser.Id;
            claim.ProcessedDate = DateTime.Now;
            claim.RejectionReason = null;

            await _db.SaveChangesAsync();

            // SIMPLE NOTIFICATION CALL
            await CreateClaimNotification(claim, "coordinator_approved");

            TempData["SuccessMessage"] = $"Claim #{claim.ClaimId} has been approved by coordinator and sent to manager for final approval!";
            return RedirectToAction(nameof(Dashboard));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string rejectionReason)
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

            if (claim.Status != ClaimStatus.Pending)
            {
                TempData["ErrorMessage"] = "Only pending claims can be rejected by coordinators.";
                return RedirectToAction(nameof(Dashboard));
            }

            claim.Status = ClaimStatus.Rejected;
            claim.ProcessedDate = DateTime.Now;
            claim.ProcessedByUserId = currentUser.Id;
            claim.RejectionReason = rejectionReason.Trim();

            await _db.SaveChangesAsync();

            // SIMPLE NOTIFICATION CALL
            await CreateClaimNotification(claim, "rejected");

            TempData["SuccessMessage"] = $"Claim #{claim.ClaimId} has been rejected.";
            return RedirectToAction(nameof(Dashboard));
        }

        // ADD THIS METHOD DIRECTLY TO THE CONTROLLER
        private async Task CreateClaimNotification(Claim claim, string action)
        {
            try
            {
                var notificationService = HttpContext.RequestServices.GetRequiredService<INotificationService>();

                var message = action switch
                {
                    "coordinator_approved" => $"Your claim #{claim.ClaimId} has been approved by coordinator and sent to manager for final approval",
                    "rejected" => $"Your claim #{claim.ClaimId} has been rejected. Reason: {claim.RejectionReason}",
                    _ => $"Update on your claim #{claim.ClaimId}"
                };

                var notificationType = action == "coordinator_approved" ? NotificationType.Success : NotificationType.Error;

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
        private async Task<VerificationResult> VerifyClaimAgainstCriteria(Claim claim)
        {
            var result = new VerificationResult { IsValid = true };

            // Predefined criteria checks
            if (claim.Workload <= 0)
            {
                result.IsValid = false;
                result.ErrorMessage = "Workload must be greater than 0";
            }
            else if (claim.Workload > 160) // Maximum 160 hours per month
            {
                result.IsValid = false;
                result.ErrorMessage = "Workload exceeds maximum allowed hours (160)";
            }
            else if (claim.HourlyRate <= 0 || claim.HourlyRate > 500) // Rate validation
            {
                result.IsValid = false;
                result.ErrorMessage = "Hourly rate is outside acceptable range";
            }
            else if (claim.Amount != claim.Workload * claim.HourlyRate) // Calculation validation
            {
                result.IsValid = false;
                result.ErrorMessage = "Amount calculation is incorrect";
            }
            else if (!claim.Documents.Any()) // Document requirement
            {
                result.IsValid = false;
                result.ErrorMessage = "Supporting documents are required";
            }

            return result;
        }

        // ADD THIS HELPER CLASS - POE REQUIREMENT
        public class VerificationResult
        {
            public bool IsValid { get; set; }
            public string ErrorMessage { get; set; } = string.Empty;
        }
    }
}