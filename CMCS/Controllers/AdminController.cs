using System.ComponentModel.DataAnnotations;
using CMCS.Data;
using CMCS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CMCS.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class AdminController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context;

        public AdminController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
        }

        // GET: Admin/Users
        public async Task<IActionResult> Users()
        {
            var users = await _userManager.Users
                .OrderBy(u => u.LastName)
                .ThenBy(u => u.FirstName)
                .ToListAsync();

            var userViewModels = new List<UserViewModel>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                userViewModels.Add(new UserViewModel
                {
                    Id = user.Id,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Role = user.Role,
                    HourlyRate = user.HourlyRate, // ADD THIS
                    DateRegistered = user.DateRegistered,
                    AspNetRoles = roles.ToList()
                });
            }

            return View(userViewModels);
        }

        // GET: Admin/CreateUser
        public IActionResult CreateUser()
        {
            return View();
        }

        // POST: Admin/CreateUser
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(CreateUserViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Check if email already exists
                var existingUser = await _userManager.FindByEmailAsync(model.Email);
                if (existingUser != null)
                {
                    ModelState.AddModelError("Email", "A user with this email already exists.");
                    return View(model);
                }

                var user = new ApplicationUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    Role = model.Role,
                    HourlyRate = model.HourlyRate, // ADD THIS - POE REQUIREMENT
                    DateRegistered = DateTime.Now,
                    EmailConfirmed = true
                };

                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    // Add to appropriate role based on UserRole
                    string roleName = model.Role switch
                    {
                        UserRole.AcademicManager => "Administrator",
                        UserRole.ProgramCoordinator => "Coordinator",
                        UserRole.Lecturer => "Lecturer",
                        _ => "Lecturer"
                    };

                    await _userManager.AddToRoleAsync(user, roleName);

                    // Create notification for admin
                    await CreateNotification($"New user {user.FullName} created successfully with hourly rate R{user.HourlyRate:N2}", "System", true);

                    // Store in session - POE REQUIREMENT
                    HttpContext.Session.SetString("LastCreatedUser", user.Email);

                    TempData["SuccessMessage"] = $"User {user.Email} created successfully with hourly rate R{user.HourlyRate:N2}!";
                    return RedirectToAction(nameof(Users));
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            return View(model);
        }

        // GET: Admin/EditUser
        public async Task<IActionResult> EditUser(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            var model = new EditUserViewModel
            {
                Id = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Role = user.Role,
                HourlyRate = user.HourlyRate // ADD THIS
            };

            return View(model);
        }

        // POST: Admin/EditUser
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(EditUserViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByIdAsync(model.Id);
                if (user == null)
                {
                    return NotFound();
                }

                // Store old role and hourly rate for notification
                var oldRole = user.Role;
                var oldHourlyRate = user.HourlyRate;

                user.FirstName = model.FirstName;
                user.LastName = model.LastName;
                user.Role = model.Role;
                user.HourlyRate = model.HourlyRate; // ADD THIS

                var result = await _userManager.UpdateAsync(user);

                if (result.Succeeded)
                {
                    // Remove all current roles
                    var currentRoles = await _userManager.GetRolesAsync(user);
                    await _userManager.RemoveFromRolesAsync(user, currentRoles);

                    // Add new role based on UserRole
                    string roleName = model.Role switch
                    {
                        UserRole.AcademicManager => "Administrator",
                        UserRole.ProgramCoordinator => "Coordinator",
                        UserRole.Lecturer => "Lecturer",
                        _ => "Lecturer"
                    };

                    await _userManager.AddToRoleAsync(user, roleName);

                    // Create notification for changes
                    var notificationMessage = $"User {user.FullName} updated";
                    if (oldRole != model.Role)
                    {
                        notificationMessage += $", role changed from {oldRole} to {model.Role}";
                    }
                    if (oldHourlyRate != model.HourlyRate)
                    {
                        notificationMessage += $", hourly rate changed from R{oldHourlyRate:N2} to R{model.HourlyRate:N2}";
                    }

                    await CreateNotification(notificationMessage, "System", true);

                    TempData["SuccessMessage"] = $"User {user.Email} updated successfully!";
                    return RedirectToAction(nameof(Users));
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            return View(model);
        }

        // POST: Admin/DeleteUser
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            // Prevent admin from deleting themselves
            var currentUser = await _userManager.GetUserAsync(User);
            if (user.Id == currentUser.Id)
            {
                TempData["ErrorMessage"] = "You cannot delete your own account.";
                return RedirectToAction(nameof(Users));
            }

            var userEmail = user.Email;
            var result = await _userManager.DeleteAsync(user);
            if (result.Succeeded)
            {
                // Create notification for user deletion
                await CreateNotification($"User {userEmail} deleted from system", "System", true);

                TempData["SuccessMessage"] = $"User {userEmail} deleted successfully!";
            }
            else
            {
                TempData["ErrorMessage"] = $"Error deleting user: {string.Join(", ", result.Errors.Select(e => e.Description))}";
            }

            return RedirectToAction(nameof(Users));
        }

        // GET: Admin/SystemStats
        public async Task<IActionResult> SystemStats()
        {
            // Use session for stats - POE REQUIREMENT
            var statsKey = "SystemStats";
            SystemStatsViewModel stats = HttpContext.Session.Get<SystemStatsViewModel>(statsKey);

            if (stats == null)
            {
                stats = new SystemStatsViewModel
                {
                    TotalUsers = await _userManager.Users.CountAsync(),
                    TotalLecturers = await _userManager.Users.CountAsync(u => u.Role == UserRole.Lecturer),
                    TotalCoordinators = await _userManager.Users.CountAsync(u => u.Role == UserRole.ProgramCoordinator),
                    TotalManagers = await _userManager.Users.CountAsync(u => u.Role == UserRole.AcademicManager),
                    TotalClaims = await _context.Claims.CountAsync(),
                    PendingClaims = await _context.Claims.CountAsync(c => c.Status == ClaimStatus.Pending || c.Status == ClaimStatus.CoordinatorApproved),
                    ApprovedClaims = await _context.Claims.CountAsync(c => c.Status == ClaimStatus.Approved),
                    RejectedClaims = await _context.Claims.CountAsync(c => c.Status == ClaimStatus.Rejected),
                    TotalAmount = await _context.Claims.Where(c => c.Status == ClaimStatus.Approved).SumAsync(c => c.Amount)
                };

                HttpContext.Session.Set(statsKey, stats);
            }

            return View(stats);
        }

        private async Task CreateNotification(string content, string sender, bool isSystem = false)
        {
            var notification = new Notification
            {
                Content = content,
                Date = DateTime.Now,
                IsRead = false,
                Sender = sender,
                IsSystemNotification = isSystem
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
        }
    }

    // Enhanced View Models
    public class UserViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public UserRole Role { get; set; }
        public decimal HourlyRate { get; set; } // ADD THIS
        public DateTime DateRegistered { get; set; }
        public List<string> AspNetRoles { get; set; } = new List<string>();
    }

    public class CreateUserViewModel
    {
        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Last Name")]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Role")]
        public UserRole Role { get; set; }

        [Required]
        [Display(Name = "Hourly Rate (R)")]
        [Range(1, 1000, ErrorMessage = "Hourly rate must be between R1 and R1000")]
        [DataType(DataType.Currency)]
        public decimal HourlyRate { get; set; } = 250.00m;

        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class EditUserViewModel
    {
        public string Id { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Last Name")]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Role")]
        public UserRole Role { get; set; }

        [Required]
        [Display(Name = "Hourly Rate (R)")]
        [Range(1, 1000, ErrorMessage = "Hourly rate must be between R1 and R1000")]
        [DataType(DataType.Currency)]
        public decimal HourlyRate { get; set; } = 250.00m;
    }

    public class SystemStatsViewModel
    {
        public int TotalUsers { get; set; }
        public int TotalLecturers { get; set; }
        public int TotalCoordinators { get; set; }
        public int TotalManagers { get; set; }
        public int TotalClaims { get; set; }
        public int PendingClaims { get; set; }
        public int ApprovedClaims { get; set; }
        public int RejectedClaims { get; set; }
        public decimal TotalAmount { get; set; }
    }
}

// Session extension methods for POE requirement
public static class SessionExtensions
{
    public static void Set<T>(this ISession session, string key, T value)
    {
        session.SetString(key, System.Text.Json.JsonSerializer.Serialize(value));
    }

    public static T Get<T>(this ISession session, string key)
    {
        var value = session.GetString(key);
        return value == null ? default(T) : System.Text.Json.JsonSerializer.Deserialize<T>(value);
    }
}