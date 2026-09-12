using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.AspNetCore.SignalR;
using ticketing_system_backend.Hubs;
using ticketing_system_backend.Interface;
using ticketing_system_backend.Models;

namespace ticketing_system_backend.Services
{
    public class NotificationService : INotificationService
    {
        private readonly IDbConnection _connection;
        private readonly IHubContext<NotificationHub> _hubContext;

        public NotificationService(IDbConnection connection, IHubContext<NotificationHub> hubContext)
        {
            _connection = connection;
            _hubContext = hubContext;
        }

        public async Task<(IEnumerable<Notification> Notifications, int TotalCount)> GetNotificationsAsync(int userId, bool? isRead, int pageNumber, int pageSize)
        {
            var parameters = new
            {
                UserId = userId,
                IsRead = isRead,
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            using var multi = await _connection.QueryMultipleAsync(
                "usp_GetNotifications",
                parameters,
                commandType: CommandType.StoredProcedure
            );

            var totalCount = await multi.ReadFirstAsync<int>();
            var notifications = await multi.ReadAsync<Notification>();

            return (notifications, totalCount);
        }

        public async Task<bool> MarkAsReadAsync(int id, int userId)
        {
            var affected = await _connection.ExecuteAsync(
                "usp_MarkNotificationAsRead",
                new { Id = id, UserId = userId },
                commandType: CommandType.StoredProcedure
            );
            return affected > 0;
        }

        public async Task<bool> MarkAllAsReadAsync(int userId)
        {
            var affected = await _connection.ExecuteAsync(
                "usp_MarkAllNotificationsAsRead",
                new { UserId = userId },
                commandType: CommandType.StoredProcedure
            );
            return affected > 0;
        }

        public async Task<Notification> CreateNotificationAsync(int userId, string title, string message, string entityType, int entityId, string redirectUrl)
        {
            var parameters = new
            {
                UserId = userId,
                Title = title,
                Message = message,
                EntityType = entityType,
                EntityId = entityId,
                RedirectUrl = redirectUrl
            };

            var id = await _connection.ExecuteScalarAsync<int>(
                "usp_InsertNotification",
                parameters,
                commandType: CommandType.StoredProcedure
            );

            var notification = new Notification
            {
                Id = id,
                UserId = userId,
                Title = title,
                Message = message,
                EntityType = entityType,
                EntityId = entityId,
                RedirectUrl = redirectUrl,
                IsRead = false,
                CreatedAt = DateTime.UtcNow.AddMinutes(330) // IST (matches trigger's timezone)
            };

            // Push notification to client via SignalR
            await _hubContext.Clients.User(userId.ToString()).SendAsync("ReceiveNotification", notification);

            return notification;
        }

        public async Task<bool> DeleteNotificationAsync(int id, int userId)
        {
            var affected = await _connection.ExecuteAsync(
                "usp_DeleteNotification",
                new { Id = id, UserId = userId },
                commandType: CommandType.StoredProcedure
            );
            return affected > 0;
        }

        public async Task TriggerTicketNotificationAsync(int ticketId, string action, int actorUserId, string? noteText = null, string? oldStatus = null, string? oldPriority = null, int? oldAssigneeId = null)
        {
            // 1. Get current ticket details and actor details
            var ticket = await _connection.QuerySingleOrDefaultAsync<dynamic>(@"
                SELECT t.TicketId, t.CustomerId, t.AssignedTo, t.Status, t.Priority, t.Subject, t.TicketNumber,
                       c.FullName AS CustomerName, a.FullName AS AssigneeName, a.ManagerId AS AssigneeManagerId
                FROM dbo.Tickets t
                LEFT JOIN dbo.Users c ON t.CustomerId = c.Id
                LEFT JOIN dbo.Users a ON t.AssignedTo = a.Id
                WHERE t.TicketId = @TicketId", new { TicketId = ticketId });

            if (ticket == null) return;

            int customerId = ticket.CustomerId;
            string ticketNumber = ticket.TicketNumber ?? "";
            string ticketSubject = ticket.Subject ?? "";
            string currentStatus = ticket.Status ?? "";
            string currentPriority = ticket.Priority ?? "";
            int? currentAssigneeId = ticket.AssignedTo;
            string customerName = ticket.CustomerName ?? "Customer";

            var actor = await _connection.QuerySingleOrDefaultAsync<dynamic>(
                "SELECT FullName, Role FROM dbo.Users WHERE Id = @ActorUserId", new { ActorUserId = actorUserId });
            string actorName = actor?.FullName ?? "System";
            string actorRole = actor?.Role ?? "User";

            // Get PMs of the customer
            var pmIds = (await _connection.QueryAsync<int>(@"
                SELECT PMId 
                FROM dbo.CustomerPMMappings 
                WHERE CustomerId = @CustomerId", new { CustomerId = customerId })).ToList();

            // Also include any manager of the assignee
            int? assigneeManagerId = ticket.AssigneeManagerId;
            if (assigneeManagerId.HasValue && !pmIds.Contains(assigneeManagerId.Value))
            {
                pmIds.Add(assigneeManagerId.Value);
            }

            // Get all Super Managers
            var superManagerIds = (await _connection.QueryAsync<int>(@"
                SELECT u.Id
                FROM dbo.Users u
                WHERE u.Role IN ('Super Admin', 'SuperManager')")).ToList();

            // Exclude the actor themselves from receiving notifications
            pmIds = pmIds.Where(id => id != actorUserId).Distinct().ToList();
            superManagerIds = superManagerIds.Where(id => id != actorUserId).Distinct().ToList();

            // Check if ticket is assigned to someone who is not the actor
            bool hasAssignee = currentAssigneeId.HasValue;
            int? assigneeId = currentAssigneeId;
            bool shouldNotifyAssignee = hasAssignee && assigneeId.Value != actorUserId;

            if (action == "Created")
            {
                // Notify Assignee
                if (shouldNotifyAssignee)
                {
                    await CreateNotificationAsync(assigneeId!.Value, "New Ticket Assigned", $"New Ticket Assigned: Ticket #{ticketNumber}", "Ticket", ticketId, $"/tickets/{ticketId}");
                }
                // Notify PMs
                foreach (var pmId in pmIds)
                {
                    await CreateNotificationAsync(pmId, "New Ticket Created", $"New Ticket #{ticketNumber} created for {customerName}", "Ticket", ticketId, $"/tickets/{ticketId}");
                }
                // Notify Super Managers
                foreach (var smId in superManagerIds)
                {
                    await CreateNotificationAsync(smId, "New Ticket Created", $"New Ticket #{ticketNumber} created by {actorName}", "Ticket", ticketId, $"/tickets/{ticketId}");
                }
            }
            else if (action == "CommentAdded")
            {
                // Notify Assignee
                if (shouldNotifyAssignee)
                {
                    await CreateNotificationAsync(assigneeId!.Value, "New Note Added", $"New note added to Ticket #{ticketNumber}", "Ticket", ticketId, $"/tickets/{ticketId}");
                }
                // Notify PMs
                if (actorRole == "Assignee" || actorRole == "Developer" || actorRole == "Customer")
                {
                    foreach (var pmId in pmIds)
                    {
                        await CreateNotificationAsync(pmId, "Developer Note Added", $"Developer added a note to Ticket #{ticketNumber}", "Ticket", ticketId, $"/tickets/{ticketId}");
                    }
                }
                // Notify Super Managers
                foreach (var smId in superManagerIds)
                {
                    await CreateNotificationAsync(smId, "New Note Added", $"New note added to Ticket #{ticketNumber}", "Ticket", ticketId, $"/tickets/{ticketId}");
                }
            }
            else // General Update
            {
                bool assigneeChanged = currentAssigneeId != oldAssigneeId;
                bool statusChanged = !string.IsNullOrEmpty(currentStatus) && !currentStatus.Equals(oldStatus ?? "", StringComparison.OrdinalIgnoreCase);
                bool priorityChanged = !string.IsNullOrEmpty(currentPriority) && !currentPriority.Equals(oldPriority ?? "", StringComparison.OrdinalIgnoreCase);

                // 1. Handle Assignee Changes
                if (assigneeChanged)
                {
                    // New assignee notification
                    if (shouldNotifyAssignee)
                    {
                        await CreateNotificationAsync(assigneeId!.Value, "New Ticket Assigned", $"New Ticket Assigned: Ticket #{ticketNumber}", "Ticket", ticketId, $"/tickets/{ticketId}");
                    }
                    // Old assignee notification
                    if (oldAssigneeId.HasValue && oldAssigneeId.Value != actorUserId)
                    {
                        await CreateNotificationAsync(oldAssigneeId.Value, "Ticket Unassigned", $"Ticket #{ticketNumber} has been unassigned from you.", "Ticket", ticketId, $"/tickets/{ticketId}");
                    }
                    // Notify Super Managers
                    var newAssigneeName = ticket.AssigneeName ?? "Unassigned";
                    foreach (var smId in superManagerIds)
                    {
                        await CreateNotificationAsync(smId, "Ticket Assignee Changed", $"Ticket #{ticketNumber} assignee changed to {newAssigneeName}", "Ticket", ticketId, $"/tickets/{ticketId}");
                    }
                }

                // 2. Handle Status Changes
                if (statusChanged)
                {
                    bool isClosed = currentStatus.Equals("Closed", StringComparison.OrdinalIgnoreCase);
                    bool isCompleted = currentStatus.Equals("Resolved", StringComparison.OrdinalIgnoreCase) || currentStatus.Equals("Resolved Developer", StringComparison.OrdinalIgnoreCase);

                    // Notify Assignee
                    if (shouldNotifyAssignee)
                    {
                        if (isClosed && actorRole.Equals("PM", StringComparison.OrdinalIgnoreCase))
                        {
                            await CreateNotificationAsync(assigneeId!.Value, "Ticket Closed", $"Ticket #{ticketNumber} was closed by Project Manager", "Ticket", ticketId, $"/tickets/{ticketId}");
                        }
                        else
                        {
                            await CreateNotificationAsync(assigneeId!.Value, "Ticket Status Changed", $"Ticket #{ticketNumber} status changed to {currentStatus}", "Ticket", ticketId, $"/tickets/{ticketId}");
                        }
                    }

                    // Notify PMs
                    if (actorRole == "Assignee" || actorRole == "Developer" || actorRole == "Customer")
                    {
                        foreach (var pmId in pmIds)
                        {
                            if (isCompleted)
                            {
                                await CreateNotificationAsync(pmId, "Ticket Completed", $"Ticket #{ticketNumber} status changed to {currentStatus}", "Ticket", ticketId, $"/tickets/{ticketId}");
                            }
                            else
                            {
                                await CreateNotificationAsync(pmId, "Ticket Status Changed", $"Ticket #{ticketNumber} status changed to {currentStatus}", "Ticket", ticketId, $"/tickets/{ticketId}");
                            }
                        }
                    }

                    // Notify Super Managers
                    foreach (var smId in superManagerIds)
                    {
                        if (isClosed)
                        {
                            await CreateNotificationAsync(smId, "Ticket Closed", $"Ticket #{ticketNumber} closed successfully", "Ticket", ticketId, $"/tickets/{ticketId}");
                        }
                        else
                        {
                            await CreateNotificationAsync(smId, "Ticket Status Changed", $"Ticket #{ticketNumber} status changed to {currentStatus}", "Ticket", ticketId, $"/tickets/{ticketId}");
                        }
                    }
                }

                // 3. Handle Priority Changes
                if (priorityChanged)
                {
                    // Notify Assignee
                    if (shouldNotifyAssignee)
                    {
                        await CreateNotificationAsync(assigneeId!.Value, "Ticket Priority Changed", $"Priority changed to {currentPriority} for Ticket #{ticketNumber}", "Ticket", ticketId, $"/tickets/{ticketId}");
                    }
                    // Notify PMs
                    if (actorRole == "Assignee" || actorRole == "Developer")
                    {
                        foreach (var pmId in pmIds)
                        {
                            await CreateNotificationAsync(pmId, "Ticket Priority Changed", $"Ticket #{ticketNumber} priority changed to {currentPriority}", "Ticket", ticketId, $"/tickets/{ticketId}");
                        }
                    }
                    // Notify Super Managers
                    foreach (var smId in superManagerIds)
                    {
                        await CreateNotificationAsync(smId, "Ticket Priority Changed", $"Ticket #{ticketNumber} priority changed to {currentPriority}", "Ticket", ticketId, $"/tickets/{ticketId}");
                    }
                }

                // 4. Handle Generic Update if none of the above main fields changed
                if (!assigneeChanged && !statusChanged && !priorityChanged)
                {
                    // Notify Assignee
                    if (shouldNotifyAssignee)
                    {
                        await CreateNotificationAsync(assigneeId!.Value, "Ticket Updated", $"Ticket #{ticketNumber} has been updated", "Ticket", ticketId, $"/tickets/{ticketId}");
                    }
                    // Notify PMs
                    if (actorRole == "Assignee" || actorRole == "Developer" || actorRole == "Customer")
                    {
                        foreach (var pmId in pmIds)
                        {
                            await CreateNotificationAsync(pmId, "Ticket Updated", $"Ticket #{ticketNumber} status changed to {currentStatus}", "Ticket", ticketId, $"/tickets/{ticketId}");
                        }
                    }
                    // Notify Super Managers
                    foreach (var smId in superManagerIds)
                    {
                        await CreateNotificationAsync(smId, "Ticket Updated", $"Ticket #{ticketNumber} updated", "Ticket", ticketId, $"/tickets/{ticketId}");
                    }
                }
            }
        }

        public async Task TriggerCustomerAssignmentNotificationAsync(int customerId, int? oldPmId, int? newPmId, int actorUserId)
        {
            var customerName = await _connection.QuerySingleOrDefaultAsync<string>(
                "SELECT FullName FROM dbo.Users WHERE Id = @CustomerId", new { CustomerId = customerId }) ?? "Customer";

            // Scenario 1: New PM assigned
            if (newPmId.HasValue && newPmId != oldPmId)
            {
                // Notify PM
                await CreateNotificationAsync(
                    newPmId.Value,
                    "New Customer Assignment",
                    $"You have been assigned to Customer {customerName}.",
                    "Customer",
                    customerId,
                    $"/customers/{customerId}"
                );

                // Notify Customer
                var pmName = await _connection.QuerySingleOrDefaultAsync<string>(
                    "SELECT FullName FROM dbo.Users WHERE Id = @PmId", new { PmId = newPmId.Value }) ?? "Project Manager";
                
                await CreateNotificationAsync(
                    customerId,
                    "PM Assigned",
                    $"PM {pmName} has been assigned to manage your account.",
                    "Customer",
                    customerId,
                    $"/profile"
                );
            }

            // Scenario 2: PM reassigned (remove old PM mapping)
            if (oldPmId.HasValue && oldPmId != newPmId)
            {
                await CreateNotificationAsync(
                    oldPmId.Value,
                    "Customer Reassigned",
                    $"Customer {customerName} has been reassigned.",
                    "Customer",
                    customerId,
                    $"/customers"
                );
            }
        }
    }
}
