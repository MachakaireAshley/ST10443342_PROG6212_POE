using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMCS.Models
{
    public class Message
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int MessageId { get; set; }

        [Required]
        [StringLength(100)]
        public string Sender { get; set; } = string.Empty;

        [Required]
        public string Content { get; set; } = string.Empty;

        [Required]
        public DateTime Date { get; set; } = DateTime.Now;

        public bool IsRead { get; set; } = false;

        [Required]
        public string RecipientId { get; set; } = string.Empty;

        public virtual ApplicationUser? Recipient { get; set; }

        public MessageType Type { get; set; } = MessageType.General;
    }

    public enum MessageType
    {
        General,
        Urgent,
        System
    }
}