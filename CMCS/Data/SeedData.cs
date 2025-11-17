using CMCS.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CMCS.Data
{
    public static class SeedData
    {
        public static async Task Initialize(IServiceProvider serviceProvider, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            var context = serviceProvider.GetRequiredService<ApplicationDbContext>();

            Console.WriteLine("=== Starting Database Seeding ===");

            // 1. Create Roles
            await CreateRoles(roleManager);

            // 2. Create Admin User
            var adminUser = await CreateUser(
                userManager,
                "admin@cmcs.com",
                "Admin123!",
                "System",
                "Administrator",
                UserRole.AcademicManager,
                "Administrator"
            );

            // 3. Create Coordinator User
            var coordinatorUser = await CreateUser(
                userManager,
                "smithj@cmcs.com",
                "Coordinator123!",
                "Jane",
                "Smith",
                UserRole.ProgramCoordinator,
                "Coordinator"
            );

            // 4. Create Manager User
            var managerUser = await CreateUser(
                userManager,
                "machakairea@cmcs.com",
                "Manager123!",
                "Ashley",
                "Machakaire",
                UserRole.AcademicManager,
                "Manager"
            );

            // 5. Create Multiple Lecturers
            var lecturers = new[]
            {
                await CreateUser(userManager, "muzangwaj@cmcs.com", "Lecturer123!", "Joice", "Muzangwa", UserRole.Lecturer, "Lecturer"),
                await CreateUser(userManager, "johnsond@cmcs.com", "Lecturer123!", "David", "Johnson", UserRole.Lecturer, "Lecturer"),
                await CreateUser(userManager, "williamss@cmcs.com", "Lecturer123!", "Sarah", "Williams", UserRole.Lecturer, "Lecturer"),
                await CreateUser(userManager, "brownm@cmcs.com", "Lecturer123!", "Michael", "Brown", UserRole.Lecturer, "Lecturer"),
                await CreateUser(userManager, "davise@cmcs.com", "Lecturer123!", "Emily", "Davis", UserRole.Lecturer, "Lecturer")
            };

            // 6. Create sample claims for the current month to show data on dashboard
            await CreateCurrentMonthClaims(context, lecturers, coordinatorUser, managerUser);

            Console.WriteLine("=== Database Seeding Completed ===");
        }

        private static async Task CreateRoles(RoleManager<IdentityRole> roleManager)
        {
            string[] roleNames = { "Administrator", "Coordinator", "Manager", "Lecturer" };

            foreach (var roleName in roleNames)
            {
                var roleExist = await roleManager.RoleExistsAsync(roleName);
                if (!roleExist)
                {
                    var result = await roleManager.CreateAsync(new IdentityRole(roleName));
                    if (result.Succeeded)
                    {
                        Console.WriteLine($"✓ Created role: {roleName}");
                    }
                }
                else
                {
                    Console.WriteLine($"✓ Role already exists: {roleName}");
                }
            }
        }

        private static async Task<ApplicationUser?> CreateUser(
            UserManager<ApplicationUser> userManager,
            string email,
            string password,
            string firstName,
            string lastName,
            UserRole role,
            string roleName,
            decimal hourlyRate = 250.00m)
        {
            var existingUser = await userManager.FindByEmailAsync(email);
            if (existingUser != null)
            {
                Console.WriteLine($"✓ User already exists: {email}");
                return existingUser;
            }

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FirstName = firstName,
                LastName = lastName,
                Role = role,
                HourlyRate = hourlyRate,
                DateRegistered = DateTime.Now,
                EmailConfirmed = true
            };

            var createResult = await userManager.CreateAsync(user, password);
            if (createResult.Succeeded)
            {
                await userManager.AddToRoleAsync(user, roleName);
                Console.WriteLine($"✓ Created user: {email} with hourly rate: {hourlyRate}");
                return user;
            }

            Console.WriteLine($"✗ Failed to create user {email}");
            return null;
        }

        private static async Task CreateCurrentMonthClaims(ApplicationDbContext context, ApplicationUser?[] lecturers, ApplicationUser? coordinatorUser, ApplicationUser? managerUser)
        {
            var validLecturers = lecturers.Where(l => l != null).ToArray();
            if (!validLecturers.Any() || coordinatorUser == null || managerUser == null)
            {
                Console.WriteLine("✗ Cannot create sample claims - some users are null");
                return;
            }

            // Only create claims if none exist
            if (!context.Claims.Any())
            {
                var claims = new List<Claim>();
                var random = new Random();
                var currentDate = DateTime.Now;
                var currentMonth = currentDate.Month;
                var currentYear = currentDate.Year;
                var monthName = currentDate.ToString("MMMM");

                // Course descriptions for realistic claims
                var courseDescriptions = new[]
                {
                    "Introduction to Programming",
                    "Database Systems",
                    "Web Development",
                    "Software Engineering",
                    "Data Structures and Algorithms"
                };

                foreach (var lecturer in validLecturers)
                {
                    if (lecturer == null) continue;


                    var claimCount = random.Next(2, 5);
                    for (int i = 0; i < claimCount; i++)
                    {
                        var workload = random.Next(10, 31);
                        var amount = workload * 250.00m;
                        var course = courseDescriptions[random.Next(courseDescriptions.Length)];
                        var status = (ClaimStatus)random.Next(0, 3);

                        var claim = new Claim
                        {
                            UserId = lecturer.Id,
                            SubmitDate = currentDate.AddDays(-random.Next(0, 15)),
                            Period = $"{monthName} {currentYear}",
                            Amount = amount,
                            Status = status,
                            Workload = workload,
                            HourlyRate = 250.00m,
                            Description = $"Teaching hours for {course}"
                        };

                        // Set processed info for approved/rejected claims
                        if (status == ClaimStatus.Approved)
                        {
                            claim.ProcessedByUserId = managerUser.Id;
                            claim.ProcessedDate = claim.SubmitDate.AddDays(random.Next(1, 5));
                            claim.ApprovalDate = claim.ProcessedDate;
                        }
                        else if (status == ClaimStatus.Rejected)
                        {
                            claim.ProcessedByUserId = coordinatorUser.Id;
                            claim.ProcessedDate = claim.SubmitDate.AddDays(random.Next(1, 5));
                            claim.RejectionReason = "Incomplete documentation provided";
                        }

                        claims.Add(claim);
                        Console.WriteLine($"  - Created {status} claim for {lecturer.FirstName} {lecturer.LastName}: {workload} hours");
                    }
                }

                // Add some coordinator-approved claims for manager testing
                var coordinatorApprovedLecturer = validLecturers[0];
                if (coordinatorApprovedLecturer != null)
                {
                    claims.Add(new Claim
                    {
                        UserId = coordinatorApprovedLecturer.Id,
                        SubmitDate = currentDate.AddDays(-3),
                        Period = $"{monthName} {currentYear}",
                        Amount = 6250.00m,
                        Status = ClaimStatus.Pending,
                        Workload = 25,
                        HourlyRate = 250.00m,
                        Description = "Advanced Programming Workshop",
                        ProcessedByUserId = coordinatorUser.Id,
                        ProcessedDate = currentDate.AddDays(-1)
                    });
                }

                context.Claims.AddRange(claims);
                await context.SaveChangesAsync();
                Console.WriteLine($"✓ Created {claims.Count} sample claims for current month");
            }
            else
            {
                Console.WriteLine("✓ Claims already exist");
            }
        }
    }
}