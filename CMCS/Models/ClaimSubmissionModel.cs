using System.ComponentModel.DataAnnotations;

namespace CMCS.Models
{
    public class ClaimSubmissionViewModel
    {
        [Required(ErrorMessage = "Period is required")]
        [Display(Name = "Claim Period (e.g., August 2025)")]
        public string Period { get; set; } = string.Empty;

        [Required(ErrorMessage = "Workload hours are required")]
        [Range(0.1, 200, ErrorMessage = "Workload must be between 0.1 and 200 hours")]
        [Display(Name = "Workload Hours")]
        public decimal Workload { get; set; }

        [Display(Name = "Hourly Rate")]
        public decimal HourlyRate { get; set; } = 250.00m;

        [Display(Name = "Description")]
        public string Description { get; set; } = string.Empty;

        [Display(Name = "Upload Supporting Documents (Optional)")]
        public List<IFormFile>? Documents { get; set; } 

        [Display(Name = "Total Amount")]
        [DataType(DataType.Currency)]
        public decimal TotalAmount => Workload * HourlyRate;
    }
}