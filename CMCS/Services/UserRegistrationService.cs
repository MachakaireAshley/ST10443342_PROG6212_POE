using CMCS.Models;
using Microsoft.AspNetCore.Identity;

namespace CMCS.Services
{
    public class UserRegistrationService
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public UserRegistrationService(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task<IdentityResult> RegisterUserAsync(string email, string password, string firstName, string lastName, UserRole role, string roleName)
        {
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FirstName = firstName,
                LastName = lastName,
                Role = role,
                DateRegistered = DateTime.Now
            };

            var result = await _userManager.CreateAsync(user, password);

            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, roleName);
            }

            return result;
        }
    }
}