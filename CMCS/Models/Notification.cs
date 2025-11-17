using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMCS.Models
{
    public class Notification
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int NotificationId { get; set; }

        [Required]
        public string Content { get; set; } = string.Empty;

        [Required]
        public DateTime Date { get; set; } = DateTime.Now;

        public bool IsRead { get; set; } = false;

        [StringLength(100)]
        public string Sender { get; set; } = "System";

        public string? UserId { get; set; } 

        public bool IsSystemNotification { get; set; } = true;

        public NotificationType Type { get; set; } = NotificationType.Info;
    }

    public enum NotificationType
    {
        Info,
        Success,
        Warning,
        Error
    }
}