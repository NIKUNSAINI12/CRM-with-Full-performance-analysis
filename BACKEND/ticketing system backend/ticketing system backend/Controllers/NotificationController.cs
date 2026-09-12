using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ticketing_system_backend.Interface;

namespace ticketing_system_backend.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class NotificationController : ControllerBase
    {
        private readonly INotificationService _notificationService;

        public NotificationController(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        [HttpGet]
        public async Task<IActionResult> GetNotifications([FromQuery] bool? isRead, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
        {
            var userIdClaim = User.FindFirst("uid")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out var userId))
                return Unauthorized(new { error = "User ID not found in token." });

            var (notifications, totalCount) = await _notificationService.GetNotificationsAsync(userId, isRead, pageNumber, pageSize);
            
            // Get current total unread count for badge
            var (_, unreadCount) = await _notificationService.GetNotificationsAsync(userId, false, 1, 1);

            return Ok(new
            {
                notifications,
                totalCount,
                unreadCount
            });
        }

        [HttpPut("{id}/read")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var userIdClaim = User.FindFirst("uid")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out var userId))
                return Unauthorized(new { error = "User ID not found in token." });

            var success = await _notificationService.MarkAsReadAsync(id, userId);
            if (!success)
                return NotFound(new { error = "Notification not found or access denied." });

            return NoContent();
        }

        [HttpPut("read-all")]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var userIdClaim = User.FindFirst("uid")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out var userId))
                return Unauthorized(new { error = "User ID not found in token." });

            await _notificationService.MarkAllAsReadAsync(userId);
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteNotification(int id)
        {
            var userIdClaim = User.FindFirst("uid")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out var userId))
                return Unauthorized(new { error = "User ID not found in token." });

            var success = await _notificationService.DeleteNotificationAsync(id, userId);
            if (!success)
                return NotFound(new { error = "Notification not found or access denied." });

            return NoContent();
        }
    }
}
