using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ticketing_system_backend.Interface;

namespace ticketing_system_backend.Services
{
    public class NotificationSchedulerService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<NotificationSchedulerService> _logger;
        // Run check every 1 hour
        private readonly TimeSpan _checkInterval = TimeSpan.FromHours(1);

        public NotificationSchedulerService(IServiceProvider serviceProvider, ILogger<NotificationSchedulerService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Notification Scheduler Service is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckDeadlinesAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while checking ticket deadlines.");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }

            _logger.LogInformation("Notification Scheduler Service is stopping.");
        }

        private async Task CheckDeadlinesAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Checking ticket deadlines...");

            using var scope = _serviceProvider.CreateScope();
            var connection = scope.ServiceProvider.GetRequiredService<IDbConnection>();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

            // Current IST time
            var nowIst = DateTime.UtcNow.AddMinutes(330);
            var todayStart = nowIst.Date;

            // Fetch all active tickets with a deadline
            var tickets = (await connection.QueryAsync<dynamic>(@"
                SELECT t.TicketId, t.CustomerId, t.AssignedTo, t.Status, t.Subject, t.TicketNumber, td.DeadlineDate
                FROM dbo.Tickets t
                INNER JOIN dbo.TicketDeadlines td ON t.TicketId = td.TicketId
                WHERE t.Status NOT IN ('Closed', 'Resolved')")).ToList();

            foreach (var ticket in tickets)
            {
                if (stoppingToken.IsCancellationRequested) break;

                int ticketId = ticket.TicketId;
                string ticketNumber = ticket.TicketNumber ?? "";
                DateTime deadlineDate = ticket.DeadlineDate;
                int customerId = ticket.CustomerId;
                int? assigneeId = ticket.AssignedTo;

                // Calculate states
                bool isDueToday = deadlineDate.Date == todayStart;
                bool isOverdue = deadlineDate.Date < todayStart;

                if (isDueToday)
                {
                    // Check if already notified today for deadline today
                    var alreadyNotified = await connection.ExecuteScalarAsync<int>(@"
                        SELECT COUNT(*) 
                        FROM dbo.Notifications 
                        WHERE EntityId = @TicketId 
                          AND Title = 'Deadline Today' 
                          AND CreatedAt >= @TodayStart", 
                        new { TicketId = ticketId, TodayStart = todayStart });

                    if (alreadyNotified == 0)
                    {
                        _logger.LogInformation("Triggering 'Deadline Today' notifications for Ticket #{TicketNumber}", ticketNumber);
                        
                        // Resolve PMs of customer
                        var pmIds = (await connection.QueryAsync<int>(@"
                            SELECT PMId 
                            FROM dbo.CustomerPMMappings 
                            WHERE CustomerId = @CustomerId", new { CustomerId = customerId })).ToList();

                        // Resolve Super Managers
                        var superManagers = (await connection.QueryAsync<int>(@"
                            SELECT u.Id
                            FROM dbo.Users u
                            WHERE u.Role IN ('Super Admin', 'SuperManager')")).ToList();

                        // Notify Assignee
                        if (assigneeId.HasValue)
                        {
                            await notificationService.CreateNotificationAsync(assigneeId.Value, "Deadline Today", $"Deadline Today: Ticket #{ticketNumber} is due today.", "Ticket", ticketId, $"/tickets/{ticketId}");
                        }
                        // Notify PMs
                        foreach (var pmId in pmIds)
                        {
                            await notificationService.CreateNotificationAsync(pmId, "Deadline Today", $"Deadline Today: Ticket #{ticketNumber} is due today.", "Ticket", ticketId, $"/tickets/{ticketId}");
                        }
                        // Notify Super Managers
                        foreach (var smId in superManagers)
                        {
                            await notificationService.CreateNotificationAsync(smId, "Deadline Today", $"Deadline Today: Ticket #{ticketNumber} is due today.", "Ticket", ticketId, $"/tickets/{ticketId}");
                        }
                    }
                }
                else if (isOverdue)
                {
                    // Check if already notified today for overdue
                    var alreadyNotified = await connection.ExecuteScalarAsync<int>(@"
                        SELECT COUNT(*) 
                        FROM dbo.Notifications 
                        WHERE EntityId = @TicketId 
                          AND Title = 'Ticket Overdue' 
                          AND CreatedAt >= @TodayStart", 
                        new { TicketId = ticketId, TodayStart = todayStart });

                    if (alreadyNotified == 0)
                    {
                        _logger.LogInformation("Triggering 'Ticket Overdue' notifications for Ticket #{TicketNumber}", ticketNumber);

                        // Resolve PMs of customer
                        var pmIds = (await connection.QueryAsync<int>(@"
                            SELECT PMId 
                            FROM dbo.CustomerPMMappings 
                            WHERE CustomerId = @CustomerId", new { CustomerId = customerId })).ToList();

                        // Resolve Super Managers
                        var superManagers = (await connection.QueryAsync<int>(@"
                            SELECT u.Id
                            FROM dbo.Users u
                            WHERE u.Role IN ('Super Admin', 'SuperManager')")).ToList();

                        // Notify Assignee
                        if (assigneeId.HasValue)
                        {
                            await notificationService.CreateNotificationAsync(assigneeId.Value, "Ticket Overdue", $"Ticket Overdue: Ticket #{ticketNumber} is overdue.", "Ticket", ticketId, $"/tickets/{ticketId}");
                        }
                        // Notify PMs
                        foreach (var pmId in pmIds)
                        {
                            await notificationService.CreateNotificationAsync(pmId, "Ticket Overdue", $"Ticket Overdue: Ticket #{ticketNumber} is overdue.", "Ticket", ticketId, $"/tickets/{ticketId}");
                        }
                        // Notify Super Managers
                        foreach (var smId in superManagers)
                        {
                            await notificationService.CreateNotificationAsync(smId, "Ticket Overdue", $"Ticket Overdue: Ticket #{ticketNumber} is overdue.", "Ticket", ticketId, $"/tickets/{ticketId}");
                        }
                    }
                }
            }
        }
    }
}
