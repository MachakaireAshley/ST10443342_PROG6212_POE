using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;

namespace CMCS.Models
{
    public class ApplicationUser : IdentityUser
    {
        [Required]
        [Display(Name = "First Name")]
        [StringLength(50)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Last Name")]
        [StringLength(50)]
        public string LastName { get; set; } = string.Empty;

        [Display(Name = "User Role")]
        public UserRole Role { get; set; } = UserRole.Lecturer;

        [Display(Name = "Date Registered")]
        public DateTime DateRegistered { get; set; } = DateTime.Now;

        [Required]
        [Display(Name = "Hourly Rate")]
        [Range(1, 1000, ErrorMessage = "Hourly rate must be between R1 and R1000")]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal HourlyRate { get; set; } = 250.00m;
        [Display(Name = "Full Name")]
        public string FullName => $"{FirstName} {LastName}";

        // Navigation property for claims
        public virtual ICollection<Claim> Claims { get; set; } = new List<Claim>();

        // Helper method to check if user can be managed
        public bool CanBeManagedBy(ApplicationUser manager)
        {
            return manager.Role == UserRole.AcademicManager &&
                   this.Role != UserRole.AcademicManager; // Admins can't manage other admins
        }
    }
}