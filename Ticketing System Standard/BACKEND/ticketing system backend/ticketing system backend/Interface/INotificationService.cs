using System.Collections.Generic;
using System.Threading.Tasks;
using ticketing_system_backend.Models;

namespace ticketing_system_backend.Interface
{
    public interface INotificationService
    {
        Task<(IEnumerable<Notification> Notifications, int TotalCount)> GetNotificationsAsync(int userId, bool? isRead, int pageNumber, int pageSize);
        Task<bool> MarkAsReadAsync(int id, int userId);
        Task<bool> MarkAllAsReadAsync(int userId);
        Task<Notification> CreateNotificationAsync(int userId, string title, string message, string entityType, int entityId, string redirectUrl);
        Task<bool> DeleteNotificationAsync(int id, int userId);
        Task TriggerTicketNotificationAsync(int ticketId, string action, int actorUserId, string? noteText = null, string? oldStatus = null, string? oldPriority = null, int? oldAssigneeId = null);
        Task TriggerCustomerAssignmentNotificationAsync(int customerId, int? oldPmId, int? newPmId, int actorUserId);
    }
}
