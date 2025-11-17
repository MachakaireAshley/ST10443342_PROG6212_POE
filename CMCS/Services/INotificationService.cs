using CMCS.Data;
using CMCS.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CMCS.Services
{
    public interface INotificationService
    {
        Task CreateNotificationAsync(string content, string sender, string? userId = null, NotificationType type = NotificationType.Info);
        Task CreateMessageAsync(string content, string sender, string recipientId, MessageType type = MessageType.General);
        Task<List<Notification>> GetUserNotificationsAsync(string userId);
        Task<List<Message>> GetUserMessagesAsync(string userId);
        Task MarkNotificationAsReadAsync(int notificationId);
        Task MarkMessageAsReadAsync(int messageId);
    }

    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public NotificationService(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task CreateNotificationAsync(string content, string sender, string? userId = null, NotificationType type = NotificationType.Info)
        {
            var notification = new Notification
            {
                Content = content,
                Sender = sender,
                Date = DateTime.Now,
                IsRead = false,
                UserId = userId,
                IsSystemNotification = string.IsNullOrEmpty(userId),
                Type = type
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
        }

        public async Task CreateMessageAsync(string content, string sender, string recipientId, MessageType type = MessageType.General)
        {
            var message = new Message
            {
                Content = content,
                Sender = sender,
                RecipientId = recipientId,
                Date = DateTime.Now,
                IsRead = false,
                Type = type
            };

            _context.Messages.Add(message);
            await _context.SaveChangesAsync();
        }

        public async Task<List<Notification>> GetUserNotificationsAsync(string userId)
        {
            return await _context.Notifications
                .Where(n => n.UserId == null || n.UserId == userId)
                .OrderByDescending(n => n.Date)
                .Take(10)
                .ToListAsync();
        }

        public async Task<List<Message>> GetUserMessagesAsync(string userId)
        {
            return await _context.Messages
                .Where(m => m.RecipientId == userId)
                .OrderByDescending(m => m.Date)
                .Take(10)
                .ToListAsync();
        }

        public async Task MarkNotificationAsReadAsync(int notificationId)
        {
            var notification = await _context.Notifications.FindAsync(notificationId);
            if (notification != null)
            {
                notification.IsRead = true;
                await _context.SaveChangesAsync();
            }
        }

        public async Task MarkMessageAsReadAsync(int messageId)
        {
            var message = await _context.Messages.FindAsync(messageId);
            if (message != null)
            {
                message.IsRead = true;
                await _context.SaveChangesAsync();
            }
        }
    }
}