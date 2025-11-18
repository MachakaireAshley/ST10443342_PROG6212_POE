using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace CMCS.Models
{
    public enum ClaimStatus
    {
        Pending,
        CoordinatorApproved,
        Approved,
        Rejected
    }

    public class Claim
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ClaimId { get; set; }

        [Required]
        [Display(Name = "User")]
        public string UserId { get; set; } = string.Empty;  

        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; } = null!;

        [Required]
        [Display(Name = "Submission Date")]
        [DataType(DataType.Date)]
        public DateTime SubmitDate { get; set; } = DateTime.Now;

        [Required(ErrorMessage = "Period is required")]
        [Display(Name = "Claim Period")]
        [StringLength(20, ErrorMessage = "Period cannot be longer than 20 characters")]
        public string Period { get; set; } = string.Empty;

        [Required(ErrorMessage = "Amount is required")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
        [DataType(DataType.Currency)]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal Amount { get; set; }

        [Required]
        [Display(Name = "Status")]
        public ClaimStatus Status { get; set; } = ClaimStatus.Pending;

        [Required(ErrorMessage = "Workload hours are required")]
        [Range(0.1, 180, ErrorMessage = "Workload must be between 0.1 and 180 hours")]
        [Display(Name = "Workload Hours")]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal Workload { get; set; }

        [Required(ErrorMessage = "Hourly rate is required")]
        [Range(1, 1000, ErrorMessage = "Hourly rate must be between R1 and R1000")]
        [Display(Name = "Hourly Rate")]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal HourlyRate { get; set; } = 250.00m;

        [Display(Name = "Description")]
        [StringLength(500, ErrorMessage = "Description cannot be longer than 500 characters")]
        [DataType(DataType.MultilineText)]
        public string Description { get; set; } = string.Empty;

        [Display(Name = "Approval Date")]
        [DataType(DataType.Date)]
        public DateTime? ApprovalDate { get; set; }

        [Display(Name = "Rejection Reason")]
        [StringLength(500, ErrorMessage = "Rejection reason cannot be longer than 500 characters")]
        public string? RejectionReason { get; set; }

        [Display(Name = "Processed By")]
        public string? ProcessedByUserId { get; set; }

        [ForeignKey("ProcessedByUserId")]
        public virtual ApplicationUser? ProcessedByUser { get; set; }

        [Display(Name = "Processed Date")]
        public DateTime? ProcessedDate { get; set; }

        public virtual ICollection<Document> Documents { get; set; } = new List<Document>();

        [NotMapped]
        [Display(Name = "Total Amount")]
        [DataType(DataType.Currency)]
        public decimal TotalAmount => Workload * HourlyRate;
    }
}