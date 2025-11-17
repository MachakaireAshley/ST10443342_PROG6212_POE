using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace CMCS.Models
{
    public class Document
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int DocumentId { get; set; }

        [Required]
        [Display(Name = "Claim")]
        public int ClaimId { get; set; }

        [ForeignKey("ClaimId")]
        public virtual Claim Claim { get; set; }

        [Required(ErrorMessage = "File name is required")]
        [Display(Name = "File Name")]
        [StringLength(255, ErrorMessage = "File name cannot be longer than 255 characters")]
        public string FileName { get; set; }

        [Required]
        [Display(Name = "File Path")]
        [StringLength(500, ErrorMessage = "File path cannot be longer than 500 characters")]
        public string FilePath { get; set; }

        [Required]
        [Display(Name = "Upload Date")]
        public DateTime UploadDate { get; set; } = DateTime.Now;

        [Display(Name = "Description")]
        [StringLength(500, ErrorMessage = "Description cannot be longer than 500 characters")]
        public string Description { get; set; }

        [Required]
        [Display(Name = "File Size")]
        public long FileSize { get; set; }

        [Required]
        [Display(Name = "Content Type")]
        [StringLength(100, ErrorMessage = "Content type cannot be longer than 100 characters")]
        public string ContentType { get; set; }
    }
}
