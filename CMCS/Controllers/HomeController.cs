using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using CMCS.Data;
using CMCS.Models;
using CMCS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CMCS.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly UserManager<ApplicationUser> _userManager;

        public HomeController(ILogger<HomeController> logger,
                            ApplicationDbContext db,
                            IWebHostEnvironment env,
                            UserManager<ApplicationUser> userManager)
        {
            _logger = logger;
            _db = db;
            _env = env;
            _userManager = userManager;
        }

        private async Task<DashboardViewModel> GetRealDashboardData(ApplicationUser currentUser)
        {
            var notificationService = HttpContext.RequestServices.GetRequiredService<INotificationService>();

            List<Claim> userClaims;
            List<Notification> userNotifications;
            List<Message> userMessages;

            if (User.IsInRole("Lecturer"))
            {
                // Lecturers only see their own claims
                userClaims = await _db.Claims
                    .Include(c => c.User)
                    .Where(c => c.UserId == currentUser.Id)
                    .OrderByDescending(c => c.SubmitDate)
                    .Take(5)
                    .ToListAsync();
            }
            else
            {
                // Admin, Coordinator, and Manager see ALL claims
                userClaims = await _db.Claims
                    .Include(c => c.User)
                    .OrderByDescending(c => c.SubmitDate)
                    .Take(5)
                    .ToListAsync();
            }

            try
            {
                userNotifications = await notificationService.GetUserNotificationsAsync(currentUser.Id);
                userMessages = await notificationService.GetUserMessagesAsync(currentUser.Id);
            }
            catch (Exception ex)
            {
                // If notification service fails, use empty lists
                _logger.LogWarning(ex, "Failed to load notifications/messages");
                userNotifications = new List<Notification>();
                userMessages = new List<Message>();
            }

            // Auto-generate welcome notification if none exist
            if (!userNotifications.Any())
            {
                try
                {
                    await notificationService.CreateNotificationAsync(
                        "Welcome to the Claims Management System!",
                        "System",
                        currentUser.Id,
                        NotificationType.Info
                    );
                    userNotifications = await notificationService.GetUserNotificationsAsync(currentUser.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to create welcome notification");
                }
            }

            // Combine Pending and CoordinatorApproved into single Pending count
            var dashboard = new DashboardViewModel
            {
                // Combine both pending statuses into single Pending count
                PendingClaims = await _db.Claims.CountAsync(c => c.UserId == currentUser.Id &&
                    (c.Status == ClaimStatus.Pending || c.Status == ClaimStatus.CoordinatorApproved)),
                RejectedClaims = await _db.Claims.CountAsync(c => c.UserId == currentUser.Id && c.Status == ClaimStatus.Rejected),
                AcceptedClaims = await _db.Claims.CountAsync(c => c.UserId == currentUser.Id && c.Status == ClaimStatus.Approved),
                CoordinatorApprovedClaims = await _db.Claims.CountAsync(c => c.UserId == currentUser.Id && c.Status == ClaimStatus.CoordinatorApproved),
                TotalClaims = await _db.Claims.CountAsync(c => c.UserId == currentUser.Id),
                RecentClaims = userClaims,
                Notifications = userNotifications,
                Messages = userMessages
            };

            return dashboard;
        }

        // Update the Index method in HomeController
        public async Task<IActionResult> Index()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            // Store user info in session
            HttpContext.Session.SetString("CurrentUserName", currentUser.FullName);
            HttpContext.Session.SetString("CurrentUserRole", currentUser.Role.ToString());
            HttpContext.Session.SetString("CurrentUserHourlyRate", currentUser.HourlyRate.ToString());

            var dashboard = await GetRealDashboardData(currentUser);
            ViewBag.CurrentMonth = DateTime.Now.ToString("MMMM yyyy");

            return View(dashboard);
        }

        [HttpGet]
        public async Task<IActionResult> SubmitClaim()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            // Pull hourly rate from HR data (User record)
            var model = new ClaimSubmissionViewModel
            {
                HourlyRate = currentUser.HourlyRate // This is set by HR
            };

            // Store claim session data 
            HttpContext.Session.SetString("CurrentHourlyRate", currentUser.HourlyRate.ToString());

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitClaim(ClaimSubmissionViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var currentUser = await _userManager.GetUserAsync(User);
                    if (currentUser == null) return Challenge();

                    // Validation - cannot exceed 180 hours per month
                    if (model.Workload > 180)
                    {
                        ModelState.AddModelError("Workload", "Workload cannot exceed 180 hours per month.");
                        return View(model);
                    }

                    // Additional validation: Check if user has already submitted a claim for this period
                    var existingClaim = await _db.Claims
                        .FirstOrDefaultAsync(c => c.UserId == currentUser.Id && c.Period == model.Period);

                    if (existingClaim != null)
                    {
                        ModelState.AddModelError("Period", "You have already submitted a claim for this period.");
                        return View(model);
                    }

                    // Use hourly rate from HR data (User record), not from form
                    var hourlyRateFromHR = currentUser.HourlyRate;
                    var calculatedAmount = model.Workload * hourlyRateFromHR;

                    var claim = new Claim
                    {
                        UserId = currentUser.Id,
                        Period = model.Period,
                        Workload = model.Workload,
                        HourlyRate = hourlyRateFromHR, 
                        Description = model.Description,
                        Amount = calculatedAmount,
                        SubmitDate = DateTime.Now,
                        Status = ClaimStatus.Pending
                    };

                    _db.Claims.Add(claim);
                    await _db.SaveChangesAsync();

                    // Handle document uploads only if files are provided - make this optional
                    if (model.Documents != null && model.Documents.Count > 0)
                    {
                        try
                        {
                            await UploadDocumentsAsync(claim.ClaimId, model.Documents);
                        }
                        catch (Exception ex)
                        {
                            // Log document upload error but don't fail the claim submission
                            _logger.LogWarning(ex, "Document upload failed for claim {ClaimId}, but claim was submitted", claim.ClaimId);
                            // Continue with claim submission even if document upload fails
                        }
                    }

                    // Store claim in session for tracking
                    HttpContext.Session.SetString($"LastClaim_{currentUser.Id}", $"{claim.ClaimId}_{claim.Period}");

                    // ADD NOTIFICATION FOR CLAIM SUBMISSION
                    await CreateClaimNotification(claim, "submitted");

                    TempData["SuccessMessage"] = $"Claim submitted successfully! Amount: R{calculatedAmount:N2}";
                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error submitting claim for user {UserId}", User.Identity.Name);
                    ModelState.AddModelError("", "An error occurred while submitting your claim. Please try again.");
                }
            }

            // If we got this far, something failed; redisplay form
            return View(model);
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

        private async Task UploadDocumentsAsync(int claimId, List<IFormFile> files)
        {
            var uploadsDir = Path.Combine(_env.WebRootPath, "uploads");

            // Create uploads directory if it doesn't exist
            if (!Directory.Exists(uploadsDir))
            {
                Directory.CreateDirectory(uploadsDir);
            }

            foreach (var file in files)
            {
                if (file.Length > 0)
                {
                    try
                    {
                        // Validate file type
                        var allowedExtensions = new[] { ".pdf", ".docx", ".xlsx", ".doc", ".xls", ".jpg", ".jpeg", ".png" };
                        var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();

                        if (!allowedExtensions.Contains(fileExtension))
                        {
                            throw new InvalidOperationException($"File type {fileExtension} is not allowed.");
                        }

                        // Validate file size (5MB limit)
                        if (file.Length > 5 * 1024 * 1024)
                        {
                            throw new InvalidOperationException("File size must be less than 5MB.");
                        }

                        // Generate unique filename
                        var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
                        var filePath = Path.Combine(uploadsDir, uniqueFileName);

                        // Save file
                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await file.CopyToAsync(stream);
                        }

                        // Save document record to database
                        var document = new Document
                        {
                            ClaimId = claimId,
                            FileName = file.FileName,
                            FilePath = uniqueFileName,
                            UploadDate = DateTime.Now,
                            FileSize = file.Length,
                            ContentType = file.ContentType,
                            Description = $"Supporting document for claim {claimId}"
                        };

                        _db.Documents.Add(document);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error uploading document {FileName} for claim {ClaimId}", file.FileName, claimId);
                        throw new InvalidOperationException($"Error uploading {file.FileName}: {ex.Message}");
                    }
                }
            }

            await _db.SaveChangesAsync();
        }

        [HttpGet]
        public async Task<IActionResult> UploadDocuments()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            // Get user's pending and submitted claims that can have documents uploaded
            var userClaims = await _db.Claims
                .Where(c => c.UserId == currentUser.Id &&
                           (c.Status == ClaimStatus.Pending || c.Status == ClaimStatus.CoordinatorApproved))
                .OrderByDescending(c => c.SubmitDate)
                .Select(c => new SelectListItem
                {
                    Value = c.ClaimId.ToString(),
                    Text = $"CL-{c.ClaimId:D4} - {c.Period} - R{c.Amount:C} - {c.Status}"
                })
                .ToListAsync();

            var viewModel = new UploadDocumentsViewModel
            {
                UserClaims = userClaims
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadDocuments(UploadDocumentsViewModel model)
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null) return Challenge();

                if (model.Files == null || model.Files.Count == 0)
                {
                    TempData["ErrorMessage"] = "Please select at least one document to upload.";
                    return await GetUploadDocumentsViewWithClaims();
                }

                // Verify that the claim belongs to the current user
                var claim = await _db.Claims.FirstOrDefaultAsync(c => c.ClaimId == model.ClaimId && c.UserId == currentUser.Id);
                if (claim == null)
                {
                    TempData["ErrorMessage"] = "Claim not found or you don't have permission to upload documents for this claim.";
                    return await GetUploadDocumentsViewWithClaims();
                }

                await UploadDocumentsAsync(model.ClaimId, model.Files.ToList());
                TempData["SuccessMessage"] = $"{model.Files.Count} document(s) uploaded successfully for claim CL-{model.ClaimId:D4}!";

                return RedirectToAction("ViewHistory");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error uploading documents: {ex.Message}";
                return await GetUploadDocumentsViewWithClaims();
            }
        }

        private async Task<IActionResult> GetUploadDocumentsViewWithClaims()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var userClaims = await _db.Claims
                .Where(c => c.UserId == currentUser.Id &&
                           (c.Status == ClaimStatus.Pending || c.Status == ClaimStatus.CoordinatorApproved))
                .OrderByDescending(c => c.SubmitDate)
                .Select(c => new SelectListItem
                {
                    Value = c.ClaimId.ToString(),
                    Text = $"CL-{c.ClaimId:D4} - {c.Period} - R{c.Amount:C} - {c.Status}"
                })
                .ToListAsync();

            var viewModel = new UploadDocumentsViewModel
            {
                UserClaims = userClaims
            };

            return View(viewModel);
        }

        public async Task<IActionResult> GenerateReport()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            return View();
        }

        public async Task<IActionResult> ViewHistory()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            var claims = await _db.Claims
                .Include(c => c.User)
                .Include(c => c.ProcessedByUser)
                .Where(c => c.UserId == currentUser.Id)
                .OrderByDescending(c => c.SubmitDate)
                .ToListAsync();

            return View(claims);
        }

        [AllowAnonymous]
        public IActionResult Privacy()
        {
            return View();
        }

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }

    public class UploadDocumentsViewModel
    {
        [Required(ErrorMessage = "Please select a claim")]
        [Display(Name = "Claim")]
        public int ClaimId { get; set; }

        [Required(ErrorMessage = "Please select at least one file")]
        [Display(Name = "Documents")]
        public IFormFileCollection Files { get; set; }

        public List<SelectListItem> UserClaims { get; set; } = new List<SelectListItem>();
    }
}