using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ticketing_system_backend.Interface;
using ticketing_system_backend.Models;
using ticketing_system_backend.Helpers;

namespace ticketing_system_backend.API.Repositories
{
  public class ManagerRepository : IManager
  {
    private readonly IDbConnection _connection;
    private readonly ILogger<ManagerRepository> _logger;

    public ManagerRepository(IDbConnection connection, ILogger<ManagerRepository> logger)
    {
      _connection = connection;
      _logger = logger;
    }

    // --- Product Methods ---
    public async Task<IEnumerable<Product>> GetAllProductsAsync()
    {
      return await _connection.QueryAsync<Product>("usp_GetAllProducts", commandType: CommandType.StoredProcedure);
    }

    public async Task<Product> GetProductByIdAsync(int id)
    {
      return await _connection.QuerySingleOrDefaultAsync<Product>("usp_GetProductById", new { Id = id }, commandType: CommandType.StoredProcedure);
    }

    public async Task<Product> CreateProductAsync(Product product)
    {
      return await _connection.QuerySingleAsync<Product>("usp_CreateProduct", new { Name = product.Name }, commandType: CommandType.StoredProcedure);
    }

    public async Task UpdateProductAsync(int id, Product product)
    {
      await _connection.ExecuteAsync("usp_UpdateProduct", new { Id = id, Name = product.Name }, commandType: CommandType.StoredProcedure);
    }

    public async Task DeleteProductAsync(int id)
    {
      await _connection.ExecuteAsync("usp_DeleteProduct", new { Id = id }, commandType: CommandType.StoredProcedure);
    }

    // --- Ticket Methods ---
    public async Task<TicketListResponse<TicketDashboardView>> GetAllTicketsAsync(
        string? status, string? priority, int? customerId, string? assignedToId, int? createdByUserId,
        DateTime? dateFrom, DateTime? dateTo, int pageNumber, int pageSize, int? userId = null, string? userRole = null)
    {
      var parameters = new
      {
        Status = status,
        Priority = priority,
        CustomerId = customerId,
        AssignedToId = assignedToId,
        CreatedByUserId = createdByUserId,
        DateFrom = dateFrom,
        DateTo = dateTo,
        PageNumber = pageNumber,
        PageSize = pageSize,
        UserId = userId,
        UserRole = userRole
      };

      string sql = $@"
        SELECT COUNT(t.TicketId) AS TotalCount
        FROM [{DbConfig.TicketingDb}].dbo.Tickets t
        LEFT JOIN [{DbConfig.CJDarclDb}].dbo.CL_Master_User a ON t.AssignedTo = a.UserId
        WHERE 
            (@Status IS NULL OR t.Status IN (SELECT value FROM STRING_SPLIT(@Status, ',')))
            AND (@Priority IS NULL OR t.Priority IN (SELECT value FROM STRING_SPLIT(@Priority, ',')))
            AND (@CustomerId IS NULL OR t.CustomerId = @CustomerId)
            AND (@AssignedToId IS NULL OR a.UserName = @AssignedToId)
            AND (@CreatedByUserId IS NULL OR t.CreatedByUserId = @CreatedByUserId)
            AND (@DateFrom IS NULL OR t.CreatedOn >= @DateFrom)
            AND (@DateTo IS NULL OR t.CreatedOn < DATEADD(day, 1, @DateTo))
            AND (
                @UserRole IS NULL 
                OR REPLACE(LOWER(ISNULL(@UserRole, '')), ' ', '') IN ('supermanager', 'superadmin', 'admin', 'super_admin')
                OR (@UserRole = 'Assignee' AND (t.AssignedTo = @UserId OR t.CreatedByUserId = @UserId))
                OR (
                    @UserRole IN ('Manager', 'PM', 'Project Manager') 
                    AND (
                        t.CustomerId IN (
                            SELECT cd.CustomerId 
                            FROM [{DbConfig.CJDarclDb}].dbo.CL_Master_Customer_Detail cd
                            INNER JOIN [{DbConfig.CJDarclDb}].dbo.CL_Master_User mu ON cd.ExecutiveId = mu.UserId
                            WHERE mu.ManagerId = @UserId
                        )
                        OR t.AssignedTo IN (SELECT UserId FROM [{DbConfig.CJDarclDb}].dbo.CL_Master_User WHERE ManagerId = @UserId)
                        OR t.AssignedTo = @UserId
                    )
                )
                OR (@UserRole = 'HOD' AND t.AssignedTo IN (SELECT ExecutiveId FROM [{DbConfig.TicketingDb}].dbo.HODExecutiveMappings WHERE HODId = @UserId))
            );

        SELECT
            t.TicketId AS Id,
            t.Subject,
            t.Description,
            t.Status,
            t.Priority,
            t.CreatedOn,
            c.CustomerId AS CustomerId,
            c.CustomerName AS CustomerName,
            a.Name AS AssignedToName,
            a.UserName AS AssignedToNumber, 
            t.TicketNumber,
            t.LastRepliedOn,
            COALESCE(pProd.Name, t.Product) AS Product,
            t.SubProduct,
            t.LastUpdateNote AS LastUpdateNote,
            t.LastUpdateNote AS ClosingRemark,
            t.CreatedByUserId,
            t.IsCreatedByCustomer,
            COALESCE(
                uCreator.Name,
                cdCreator.DecisionMakerName,
                c.CustomerName,
                'Customer'
            ) AS CreatedByName,
            COALESCE(
                CASE 
                    WHEN t.IsCreatedByCustomer = 0 THEN 'Internal Staff'
                    WHEN t.IsCreatedByCustomer = 1 THEN 'Customer'
                END,
                CASE WHEN uCreator.UserId IS NOT NULL THEN 'Internal Staff' ELSE 'Customer' END
            ) AS CreatedByRole
        FROM [{DbConfig.TicketingDb}].dbo.Tickets t
        LEFT JOIN [{DbConfig.TicketingDb}].dbo.Products pProd ON TRY_CAST(t.Product AS INT) = pProd.Id
        LEFT JOIN [{DbConfig.CJDarclDb}].dbo.CL_Master_Customer c ON t.CustomerId = c.CustomerId
        LEFT JOIN [{DbConfig.CJDarclDb}].dbo.CL_Master_Customer_Detail cdCreator ON c.CustomerId = cdCreator.CustomerId
        LEFT JOIN [{DbConfig.CJDarclDb}].dbo.CL_Master_User a ON t.AssignedTo = a.UserId
        LEFT JOIN [{DbConfig.CJDarclDb}].dbo.CL_Master_User uCreator ON t.CreatedByUserId = uCreator.UserId
        WHERE 
            (@Status IS NULL OR t.Status IN (SELECT value FROM STRING_SPLIT(@Status, ',')))
            AND (@Priority IS NULL OR t.Priority IN (SELECT value FROM STRING_SPLIT(@Priority, ',')))
            AND (@CustomerId IS NULL OR t.CustomerId = @CustomerId)
            AND (@AssignedToId IS NULL OR a.UserName = @AssignedToId)
            AND (@CreatedByUserId IS NULL OR t.CreatedByUserId = @CreatedByUserId)
            AND (@DateFrom IS NULL OR t.CreatedOn >= @DateFrom)
            AND (@DateTo IS NULL OR t.CreatedOn < DATEADD(day, 1, @DateTo))
            AND (
                @UserRole IS NULL 
                OR REPLACE(LOWER(ISNULL(@UserRole, '')), ' ', '') IN ('supermanager', 'superadmin', 'admin', 'super_admin')
                OR (@UserRole = 'Assignee' AND (t.AssignedTo = @UserId OR t.CreatedByUserId = @UserId))
                OR (
                    @UserRole IN ('Manager', 'PM', 'Project Manager') 
                    AND (
                        t.CustomerId IN (
                            SELECT cd.CustomerId 
                            FROM [{DbConfig.CJDarclDb}].dbo.CL_Master_Customer_Detail cd
                            INNER JOIN [{DbConfig.CJDarclDb}].dbo.CL_Master_User mu ON cd.ExecutiveId = mu.UserId
                            WHERE mu.ManagerId = @UserId
                        )
                        OR t.AssignedTo IN (SELECT UserId FROM [{DbConfig.CJDarclDb}].dbo.CL_Master_User WHERE ManagerId = @UserId)
                        OR t.AssignedTo = @UserId
                    )
                )
                OR (@UserRole = 'HOD' AND t.AssignedTo IN (SELECT ExecutiveId FROM [{DbConfig.TicketingDb}].dbo.HODExecutiveMappings WHERE HODId = @UserId))
            )
        ORDER BY t.CreatedOn DESC
        OFFSET CASE WHEN @PageSize >= 100000 OR @PageSize <= 0 THEN 0 ELSE (@PageNumber - 1) * @PageSize END ROWS
        FETCH NEXT CASE WHEN @PageSize >= 100000 OR @PageSize <= 0 THEN 1000000 ELSE @PageSize END ROWS ONLY;
      ";

      using var multi = await _connection.QueryMultipleAsync(sql, parameters);
      var totalCount = await multi.ReadSingleAsync<int>();
      var tickets = await multi.ReadAsync<TicketDashboardView>();
      return new TicketListResponse<TicketDashboardView> { TotalCount = totalCount, Tickets = tickets };
    }

    public async Task UpdateTicketByDeveloperAsync(int ticketId, DeveloperUpdateRequest request)
    {
      var parameters = new { TicketId = ticketId, Status = request.Status };
      await _connection.ExecuteAsync("usp_UpdateTicketByDeveloper", parameters, commandType: CommandType.StoredProcedure);
    }

    public async Task SubmitForReviewAsync(int ticketId, int changedByUserId, string userRole)
    {
      var activeChildren = (await _connection.QueryAsync<dynamic>(@"
          SELECT t.TicketId, t.TicketNumber
          FROM   Tickets t
          INNER  JOIN TicketLinks tl ON tl.TargetTicketId = t.TicketId
          WHERE  tl.SourceTicketId = @TicketId
            AND  tl.LinkType       = 'ParentChild'
            AND  t.Status         NOT IN ('Closed', 'On Hold')
      ", new { TicketId = ticketId })).ToList();

      if (activeChildren.Any())
      {
          var blockingNums = string.Join(", ", activeChildren.Select(c => (string)(c.TicketNumber ?? $"#{c.TicketId}")));
          throw new InvalidOperationException(
              $"Cannot submit for review — {activeChildren.Count} child ticket(s) (checkpoints) are still active (not Closed or On Hold): {blockingNums}. " +
              "Resolve or hold all child tickets first.");
      }

      await _connection.ExecuteAsync("usp_SubmitTicketForReview",
          new { TicketId = ticketId, UserId = changedByUserId, UserRole = userRole },
          commandType: CommandType.StoredProcedure);
    }

    public async Task<TicketTimelineAnalysis> GetTicketTimelineAnalysisAsync(int ticketId)
    {
        var ticket = await _connection.QuerySingleOrDefaultAsync<Ticket>(
            "usp_GetTicketDetailsById", 
            new { TicketId = ticketId }, 
            commandType: CommandType.StoredProcedure
        );

        if (ticket == null) return new TicketTimelineAnalysis { Summary = new TicketTimelineMetrics(), Breakdown = new List<TicketStateDuration>() };

        var history = (await _connection.QueryAsync<TicketHistory>(
             "usp_GetTicketHistory",
             new { TicketId = ticketId },
             commandType: CommandType.StoredProcedure
         )).OrderBy(h => h.ChangeDate).ToList();

        var createdOnLocal = ticket.CreatedOn;
        var currentStateOrAssignee = "Open";
        var istOffset = TimeSpan.FromMinutes(330); // 5 hours 30 mins
        
        if (history.Any())
        {
            var firstEvent = history.First();
            var rawDiff = firstEvent.ChangeDate - ticket.CreatedOn;
            var localOffset = TimeSpan.FromMinutes(330);

            if (rawDiff.TotalHours > 5.0 && rawDiff.TotalHours < 6.0) 
            {
                 if (Math.Abs((rawDiff - localOffset).TotalHours) < 1.0)
                    createdOnLocal = ticket.CreatedOn.Add(localOffset);
                 else
                    createdOnLocal = ticket.CreatedOn.Add(istOffset);
            }
        }

        var summary = new TicketTimelineMetrics
        {
            TicketCreatedDate = ticket.CreatedOn, 
            TicketClosedDate = ticket.Status == "Closed" ? (DateTime?)ticket.LastRepliedOn : null
        };

        Func<DateTime, DateTime> alignEventTime = (dt) => 
        {
            var diff = dt - createdOnLocal;
            if (diff.TotalHours > -6.0 && diff.TotalHours < -5.0)
            {
                 return dt.Add(istOffset);
            }
            return h_ToLocal(dt); 
        };

        var firstResponseEvent = history.FirstOrDefault(h => 
            h.ChangedByUserRole != "Customer" || 
            (h.EventDescription.Contains("Status changed") && !h.EventDescription.Contains("Open"))
        );

        if (firstResponseEvent != null)
        {
            var evtDate = alignEventTime(firstResponseEvent.ChangeDate);
            summary.TimeToFirstResponseMinutes = (int)(evtDate - createdOnLocal).TotalMinutes;
        }

        if (string.Equals(ticket.Status, "Closed", StringComparison.OrdinalIgnoreCase))
        {
            var closeEvent = history.LastOrDefault(h => 
                h.EventDescription.IndexOf("Status changed", StringComparison.OrdinalIgnoreCase) >= 0 &&
                (h.EventDescription.IndexOf("to 'Closed'", StringComparison.OrdinalIgnoreCase) >= 0 || 
                 h.EventDescription.IndexOf("to Closed", StringComparison.OrdinalIgnoreCase) >= 0));
            var closeDateRaw = closeEvent?.ChangeDate ?? ticket.LastRepliedOn; // Fallback
            
            if (closeDateRaw.HasValue)
            {
                var closeDateLocal = h_ToLocal(closeDateRaw.Value);
                summary.TicketClosedDate = closeDateRaw; 
                summary.TimeToResolutionHours = (int)(closeDateLocal - createdOnLocal).TotalHours;
            }
        }

        var allUsers = await _connection.QueryAsync<User>($@"
            SELECT CustomerName AS FullName, 'Customer' AS Role FROM {DbConfig.CJDarclDb}.dbo.CL_Master_Customer WHERE IsActive = 1
            UNION ALL
            SELECT 
                Name AS FullName,
                CASE 
                    WHEN RoleId = 1 THEN 'Super Admin'
                    WHEN UserId IN (
                        SELECT DISTINCT m.ManagerId 
                        FROM {DbConfig.CJDarclDb}.dbo.CL_Master_User m
                        WHERE m.ManagerId IS NOT NULL
                          AND m.UserId IN (SELECT DISTINCT m2.ManagerId FROM {DbConfig.CJDarclDb}.dbo.CL_Master_User m2 WHERE m2.ManagerId IS NOT NULL)
                    ) THEN 'SuperManager'
                    WHEN u.UserId IN (
                        SELECT DISTINCT m.ManagerId 
                        FROM {DbConfig.CJDarclDb}.dbo.CL_Master_User m
                        WHERE m.ManagerId IS NOT NULL
                    ) THEN 'PM'
                    ELSE 'Assignee'
                END AS Role
            FROM {DbConfig.CJDarclDb}.dbo.CL_Master_User u WHERE u.IsActive = 1");

        var roleMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach(var u in allUsers) 
        {
            if (!string.IsNullOrEmpty(u.FullName)) roleMap[u.FullName] = u.Role;
        }

        var breakdownList = new List<TicketStateDuration>();
        string currentAssigneeName = null;
        var currentStatus = "Open";

        // Find the first assignment event to determine the initial assignee
        var firstAssignEvent = history.FirstOrDefault(h => 
            h.EventDescription.Contains("Assigned:") || 
            h.EventDescription.Contains("Assigned to") || 
            h.EventDescription.Contains("assigned from")
        );
        
        if (firstAssignEvent != null)
        {
            if (firstAssignEvent.EventDescription.Contains("Assigned:"))
            {
                var idx = firstAssignEvent.EventDescription.IndexOf("Assigned:", StringComparison.OrdinalIgnoreCase);
                var assignedPart = firstAssignEvent.EventDescription.Substring(idx + "Assigned:".Length);
                var arrowIdx = assignedPart.IndexOf("->");
                if (arrowIdx != -1)
                {
                    var oldName = assignedPart.Substring(0, arrowIdx).Trim();
                    currentAssigneeName = oldName;
                }
            }
            else if (firstAssignEvent.EventDescription.Contains("assigned from"))
            {
                var idxFrom = firstAssignEvent.EventDescription.IndexOf("assigned from", StringComparison.OrdinalIgnoreCase);
                var idxTo = firstAssignEvent.EventDescription.IndexOf(" to ", idxFrom);
                if (idxFrom != -1 && idxTo != -1)
                {
                    var oldName = firstAssignEvent.EventDescription.Substring(idxFrom + "assigned from".Length, idxTo - (idxFrom + "assigned from".Length)).Trim();
                    currentAssigneeName = oldName.Trim('\'', ' ');
                }
            }
            else if (firstAssignEvent.EventDescription.Contains("Assigned to"))
            {
                currentAssigneeName = "Unassigned";
            }
        }
        else
        {
            currentAssigneeName = ticket.AssignedToName;
        }

        var lastDevName = !string.IsNullOrEmpty(currentAssigneeName) && !string.Equals(currentAssigneeName, "Unassigned", StringComparison.OrdinalIgnoreCase) ? currentAssigneeName : "";
        var lastCheckTime = createdOnLocal;

        Func<string, string, string> getStateOwner = (status, assignee) =>
        {
            if (string.Equals(status, "Closed", StringComparison.OrdinalIgnoreCase))
            {
                return "Closed";
            }
            if (string.Equals(status, "On Hold", StringComparison.OrdinalIgnoreCase))
            {
                return "On Hold";
            }
            if (status.Contains("Pending PM Review"))
            {
                return "PM (Reviewing)";
            }
            if (string.IsNullOrEmpty(assignee) || string.Equals(assignee, "Unassigned", StringComparison.OrdinalIgnoreCase))
            {
                return "PM";
            }
            
            var role = roleMap.ContainsKey(assignee) ? roleMap[assignee] : "Developer";
            if (role == "Assignee") role = "Developer";
            
            if (status.Contains("Rework"))
            {
                return $"{role}: {assignee} (Rework)";
            }
            return $"{role}: {assignee}";
        };

        var currentStateOwner = getStateOwner(currentStatus, currentAssigneeName);

        foreach (var entry in history)
        {
            var entryDateLocal = alignEventTime(entry.ChangeDate);
            var durationMinutes = (int)(entryDateLocal - lastCheckTime).TotalMinutes;
            
            if (durationMinutes > 0)
            {
                var existing = breakdownList.FirstOrDefault(b => b.StateOrAssigneeName == currentStateOwner);
                if (existing == null)
                {
                    breakdownList.Add(new TicketStateDuration { StateOrAssigneeName = currentStateOwner, TotalDurationMinutes = durationMinutes });
                }
                else
                {
                    existing.TotalDurationMinutes += durationMinutes;
                }
            }
            
            // 1. Check for Assignment Changes
            if (entry.EventDescription.Contains("Assigned:") || entry.EventDescription.Contains("Assigned to") || entry.EventDescription.Contains("assigned from"))
            {
                string name = "";
                if (entry.EventDescription.Contains("Assigned:"))
                {
                    var idx = entry.EventDescription.IndexOf("Assigned:", StringComparison.OrdinalIgnoreCase);
                    var assignedPart = entry.EventDescription.Substring(idx + "Assigned:".Length);
                    var arrowIdx = assignedPart.IndexOf("->");
                    if (arrowIdx != -1)
                    {
                        var newNamePart = assignedPart.Substring(arrowIdx + 2).Trim();
                        var dotIndex = newNamePart.IndexOf('.');
                        if (dotIndex != -1)
                        {
                            newNamePart = newNamePart.Substring(0, dotIndex).Trim();
                        }
                        name = newNamePart.Trim('\'', ' ');
                    }
                }
                else if (entry.EventDescription.Contains("Assigned to"))
                {
                    var split = entry.EventDescription.Split(new[] { "Assigned to" }, StringSplitOptions.None);
                    if (split.Length > 1) name = split[1].Trim();
                }
                else if (entry.EventDescription.Contains("assigned from"))
                {
                    var lastTo = entry.EventDescription.LastIndexOf(" to ");
                    if (lastTo != -1)
                    {
                        name = entry.EventDescription.Substring(lastTo + 4).Trim();
                        name = name.TrimEnd('.').Replace("'", ""); 
                    }
                }
                
                if (!string.IsNullOrEmpty(name))
                {
                    currentAssigneeName = name;
                    if (!string.Equals(name, "Unassigned", StringComparison.OrdinalIgnoreCase))
                    {
                        lastDevName = name;
                    }
                }
            }
            
            // 2. Check for Status Changes
            if (entry.EventDescription.Contains("Status changed") || entry.EventDescription.Contains("Status:") || entry.EventDescription.Contains("Reopen"))
            {
                string status = "";
                if (entry.EventDescription.Contains("Status:"))
                {
                    var statusIdx = entry.EventDescription.IndexOf("Status:", StringComparison.OrdinalIgnoreCase);
                    if (statusIdx != -1)
                    {
                        var statusPart = entry.EventDescription.Substring(statusIdx + "Status:".Length);
                        var arrowIdx = statusPart.IndexOf("->");
                        if (arrowIdx != -1)
                        {
                            var newStatusPart = statusPart.Substring(arrowIdx + 2).Trim();
                            var dotIndex = newStatusPart.IndexOf('.');
                            if (dotIndex != -1)
                            {
                                newStatusPart = newStatusPart.Substring(0, dotIndex).Trim();
                            }
                            status = newStatusPart.Trim('\'', ' ');
                        }
                    }
                }
                else if (entry.EventDescription.Contains("Reopen"))
                {
                    status = "Open";
                }
                else
                {
                    var lastTo = entry.EventDescription.LastIndexOf(" to ");
                    if (lastTo != -1)
                    {
                        status = entry.EventDescription.Substring(lastTo + 4).Trim();
                        status = status.TrimEnd('.').Replace("'", ""); 
                    }
                }
                
                if (!string.IsNullOrEmpty(status))
                {
                    currentStatus = status;
                }
            }
            
            currentStateOwner = getStateOwner(currentStatus, currentAssigneeName);
            lastCheckTime = entryDateLocal;
        }

        if (!string.Equals(ticket.Status, "Closed", StringComparison.OrdinalIgnoreCase))
        {
            var timeNow = DateTime.UtcNow.AddMinutes(330); 
            var durationMinutes = (int)(timeNow - lastCheckTime).TotalMinutes;
            if (durationMinutes > 0)
            {
                var existing = breakdownList.FirstOrDefault(b => b.StateOrAssigneeName == currentStateOwner);
                if (existing == null)
                {
                    breakdownList.Add(new TicketStateDuration { StateOrAssigneeName = currentStateOwner, TotalDurationMinutes = durationMinutes });
                }
                else
                {
                    existing.TotalDurationMinutes += durationMinutes;
                }
            }
        }

        return new TicketTimelineAnalysis 
        { 
            Summary = summary, 
            Breakdown = breakdownList.OrderByDescending(b => b.TotalDurationMinutes).ToList() 
        };
    }

    private DateTime h_ToLocal(DateTime dt)
    {
        if (dt.Kind == DateTimeKind.Utc)
        {
            return TimeZoneInfo.ConvertTimeFromUtc(dt, TimeZoneInfo.Local);
        }
        return dt;
    }

    public async Task UpdateAssignmentAsync(int ticketId, int? assignedToId, int userId, string userRole)
    {
      await _connection.ExecuteAsync("usp_UpdateTicketAssignment",
          new { TicketId = ticketId, AssignedToId = assignedToId, UserId = userId, UserRole = userRole },
          commandType: CommandType.StoredProcedure);
    }

    public async Task UpdateTicketByPmAsync(int ticketId, int pmId, PmTicketUpdateRequest request)
    {
      if (!string.IsNullOrEmpty(request.Status) &&
          string.Equals(request.Status, "Closed", StringComparison.OrdinalIgnoreCase))
      {
          var activeChildren = (await _connection.QueryAsync<dynamic>(@"
              SELECT t.TicketId, t.TicketNumber
              FROM   Tickets t
              INNER  JOIN TicketLinks tl ON tl.TargetTicketId = t.TicketId
              WHERE  tl.SourceTicketId = @TicketId
                AND  tl.LinkType       = 'ParentChild'
                AND  t.Status         NOT IN ('Closed', 'On Hold')
          ", new { TicketId = ticketId })).ToList();

          if (activeChildren.Any())
          {
              var blockingNums = string.Join(", ", activeChildren.Select(c => (string)(c.TicketNumber ?? $"#{c.TicketId}")));
              throw new InvalidOperationException(
                  $"Cannot close this ticket — {activeChildren.Count} child ticket(s) are still active (not Closed or On Hold): {blockingNums}. " +
                  "Resolve or hold all child tickets first.");
          }
      }

      if (!string.IsNullOrEmpty(request.Status))
      {
          var closedParent = await _connection.QueryFirstOrDefaultAsync<dynamic>(@"
              SELECT t.TicketId, t.TicketNumber, t.Status
              FROM   Tickets t
              INNER  JOIN TicketLinks tl ON tl.SourceTicketId = t.TicketId
              WHERE  tl.TargetTicketId = @TicketId
                AND  tl.LinkType      = 'ParentChild'
                AND  t.Status         = 'Closed'
          ", new { TicketId = ticketId });

          if (closedParent != null)
          {
              var parentNum = (string)(closedParent.TicketNumber ?? $"#{closedParent.TicketId}");
              throw new InvalidOperationException(
                  $"Cannot change status of this ticket — its parent ticket {parentNum} is already Closed. " +
                  "Reopen the parent ticket first if further work is needed.");
          }
      }

      await _connection.ExecuteAsync("usp_UpdateTicketByPM",
          new { 
              TicketId = ticketId, 
              PmId = pmId, 
              Status = request.Status, 
              Priority = request.Priority, 
              AssignedToId = request.AssignedToId,
              Note = request.Note,
              DeadlineDate = request.DeadlineDate
          },
          commandType: CommandType.StoredProcedure);

      string? noteText = request.Note?.Trim();
      if (!string.IsNullOrWhiteSpace(noteText))
      {
        bool isClosed = string.Equals(request.Status, "Closed", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(request.Status, "Close", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(request.Status, "Work Done", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(request.Status, "Resolved", StringComparison.OrdinalIgnoreCase);

        if (!isClosed)
        {
          var currentStatus = await _connection.ExecuteScalarAsync<string>(
              $"SELECT Status FROM [{DbConfig.TicketingDb}].dbo.Tickets WHERE TicketId = @TicketId",
              new { TicketId = ticketId }
          );
          isClosed = string.Equals(currentStatus, "Closed", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(currentStatus, "Close", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(currentStatus, "Work Done", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(currentStatus, "Resolved", StringComparison.OrdinalIgnoreCase);
        }

        if (isClosed)
        {
          try
          {
            await _connection.ExecuteAsync($@"
                IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Tickets' AND COLUMN_NAME = 'LastUpdateNote')
                    UPDATE [{DbConfig.TicketingDb}].dbo.Tickets SET LastUpdateNote = @Note WHERE TicketId = @TicketId;
                IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Tickets' AND COLUMN_NAME = 'LastUpdatedNote')
                    UPDATE [{DbConfig.TicketingDb}].dbo.Tickets SET LastUpdatedNote = @Note WHERE TicketId = @TicketId;",
                new { Note = noteText, TicketId = ticketId }
            );
          }
          catch (Exception exNote)
          {
            _logger.LogWarning(exNote, "Could not update LastUpdateNote in UpdateTicketByPmAsync for ticket {TicketId}", ticketId);
          }
        }
      }
    }

    public async Task UpdateStatusAsync(int ticketId, string status, int? userId = null, string? userRole = null, string? note = null)
    {
      if (!string.IsNullOrEmpty(status) &&
          (string.Equals(status, "Closed", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(status, "Close", StringComparison.OrdinalIgnoreCase)))
      {
          var activeChildren = (await _connection.QueryAsync<dynamic>(@"
               SELECT t.TicketId, t.TicketNumber
               FROM   Tickets t
               INNER  JOIN TicketLinks tl ON tl.TargetTicketId = t.TicketId
               WHERE  tl.SourceTicketId = @TicketId
                 AND  tl.LinkType       = 'ParentChild'
                 AND  t.Status         NOT IN ('Closed', 'Close', 'On Hold')
          ", new { TicketId = ticketId })).ToList();

          if (activeChildren.Any())
          {
              var blockingNums = string.Join(", ", activeChildren.Select(c => (string)(c.TicketNumber ?? $"#{c.TicketId}")));
              throw new InvalidOperationException(
                  $"Cannot close this ticket — {activeChildren.Count} child ticket(s) are still active (not Closed or On Hold): {blockingNums}. " +
                  "Resolve or hold all child tickets first.");
          }
      }

      if (!string.IsNullOrEmpty(status))
      {
          var closedParent = await _connection.QueryFirstOrDefaultAsync<dynamic>(@"
               SELECT t.TicketId, t.TicketNumber, t.Status
               FROM   Tickets t
               INNER  JOIN TicketLinks tl ON tl.SourceTicketId = t.TicketId
               WHERE  tl.TargetTicketId = @TicketId
                 AND  tl.LinkType      = 'ParentChild'
                 AND  t.Status         IN ('Closed', 'Close')
          ", new { TicketId = ticketId });

          if (closedParent != null)
          {
              var parentNum = (string)(closedParent.TicketNumber ?? $"#{closedParent.TicketId}");
              throw new InvalidOperationException(
                  $"Cannot change status of this ticket — its parent ticket {parentNum} is already Closed. " +
                  "Reopen the parent ticket first if further work is needed.");
          }
      }

      await _connection.ExecuteAsync("usp_UpdateTicketStatus",
          new { TicketId = ticketId, Status = status, UserId = userId, UserRole = userRole, Note = note }, commandType: CommandType.StoredProcedure);

      string? noteStatusText = note?.Trim();
      if (!string.IsNullOrWhiteSpace(noteStatusText))
      {
        bool isClosed = string.Equals(status, "Closed", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(status, "Close", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(status, "Work Done", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(status, "Resolved", StringComparison.OrdinalIgnoreCase);

        if (!isClosed)
        {
          var currentStatus = await _connection.ExecuteScalarAsync<string>(
              $"SELECT Status FROM [{DbConfig.TicketingDb}].dbo.Tickets WHERE TicketId = @TicketId",
              new { TicketId = ticketId }
          );
          isClosed = string.Equals(currentStatus, "Closed", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(currentStatus, "Close", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(currentStatus, "Work Done", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(currentStatus, "Resolved", StringComparison.OrdinalIgnoreCase);
        }

        if (isClosed)
        {
          try
          {
            await _connection.ExecuteAsync($@"
                IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Tickets' AND COLUMN_NAME = 'LastUpdateNote')
                    UPDATE [{DbConfig.TicketingDb}].dbo.Tickets SET LastUpdateNote = @Note WHERE TicketId = @TicketId;
                IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Tickets' AND COLUMN_NAME = 'LastUpdatedNote')
                    UPDATE [{DbConfig.TicketingDb}].dbo.Tickets SET LastUpdatedNote = @Note WHERE TicketId = @TicketId;",
                new { Note = noteStatusText, TicketId = ticketId }
            );
          }
          catch (Exception exNote)
          {
            _logger.LogWarning(exNote, "Could not update LastUpdateNote in UpdateStatusAsync for ticket {TicketId}", ticketId);
          }
        }
      }
    }


    public async Task<DashboardStats> GetDeveloperDashboardStatsAsync(int assignedToId)
    {
      return await _connection.QuerySingleAsync<DashboardStats>("usp_GetDeveloperDashboardStats",
          new { AssignedToId = assignedToId }, commandType: CommandType.StoredProcedure);
    }

    public async Task<DashboardStats> GetDashboardStatsAsync(int? pmId = null)
    {
      return await _connection.QuerySingleAsync<DashboardStats>("usp_GetDashboardStats", new { PMId = pmId }, commandType: CommandType.StoredProcedure);
    }

    public async Task UpdateTicketByCustomerAsync(int ticketId, int customerId, CustomerUpdateTicketRequest request)
    {
      if (!string.IsNullOrEmpty(request.Status) &&
          string.Equals(request.Status, "Closed", StringComparison.OrdinalIgnoreCase))
      {
          var activeChildren = (await _connection.QueryAsync<dynamic>(@"
              SELECT t.TicketId, t.TicketNumber
              FROM   Tickets t
              INNER  JOIN TicketLinks tl ON tl.TargetTicketId = t.TicketId
              WHERE  tl.SourceTicketId = @TicketId
                AND  tl.LinkType       = 'ParentChild'
                AND  t.Status         NOT IN ('Closed', 'On Hold')
          ", new { TicketId = ticketId })).ToList();

          if (activeChildren.Any())
          {
              var blockingNums = string.Join(", ", activeChildren.Select(c => (string)(c.TicketNumber ?? $"#{c.TicketId}")));
              throw new InvalidOperationException(
                  $"Cannot close this ticket — {activeChildren.Count} child ticket(s) are still active (not Closed or On Hold): {blockingNums}. " +
                  "Resolve or hold all child tickets first.");
          }
      }

      if (!string.IsNullOrEmpty(request.Status))
      {
          var closedParent = await _connection.QueryFirstOrDefaultAsync<dynamic>(@"
              SELECT t.TicketId, t.TicketNumber, t.Status
              FROM   Tickets t
              INNER  JOIN TicketLinks tl ON tl.SourceTicketId = t.TicketId
              WHERE  tl.TargetTicketId = @TicketId
                AND  tl.LinkType      = 'ParentChild'
                AND  t.Status         = 'Closed'
          ", new { TicketId = ticketId });

          if (closedParent != null)
          {
              var parentNum = (string)(closedParent.TicketNumber ?? $"#{closedParent.TicketId}");
              throw new InvalidOperationException(
                  $"Cannot change status of this ticket — its parent ticket {parentNum} is already Closed. " +
                  "Reopen the parent ticket first if further work is needed.");
          }
      }

      var parameters = new { TicketId = ticketId, CustomerId = customerId, Status = request.Status, Priority = request.Priority };
      await _connection.ExecuteAsync("usp_UpdateTicketByCustomer", parameters, commandType: CommandType.StoredProcedure);
    }

    public async Task SetTicketDeadlineAsync(int ticketId, SetDeadlineRequest request)
    {
      var parameters = new { TicketId = ticketId, DeadlineDate = request.DeadlineDate, ManagerId = request.ManagerId };
      await _connection.ExecuteAsync("dbo.usp_SetTicketDeadline", parameters, commandType: CommandType.StoredProcedure);
    }

    public async Task<TicketDeadline> GetTicketDeadlineAsync(int ticketId)
    {
      const string sql = "SELECT TicketId, DeadlineDate, SetByManagerId, SetOn FROM dbo.TicketDeadlines WHERE TicketId = @TicketId;";
      return await _connection.QuerySingleOrDefaultAsync<TicketDeadline>(sql, new { TicketId = ticketId });
    }

    public async Task<TicketListResponse<TicketDashboardView>> GetDelayedTicketsForDeveloperAsync(int developerId, int pageNumber, int pageSize)
    {
      var parameters = new { AssignedToId = developerId, PageNumber = pageNumber, PageSize = pageSize };
      using var multi = await _connection.QueryMultipleAsync("dbo.usp_GetDelayedTicketsForDeveloper", parameters, commandType: CommandType.StoredProcedure);
      var tickets = await multi.ReadAsync<TicketDashboardView>();
      var totalCount = await multi.ReadSingleAsync<int>();
      return new TicketListResponse<TicketDashboardView> { Tickets = tickets, TotalCount = totalCount };
    }

    public async Task<TicketListResponse<TicketDashboardView>> GetDelayedTicketsAsync(int pageNumber, int pageSize, int? pmId = null)
    {
      var parameters = new { PageNumber = pageNumber, PageSize = pageSize, PMId = pmId };
      using var multi = await _connection.QueryMultipleAsync("dbo.usp_GetDelayedTickets", parameters, commandType: CommandType.StoredProcedure);
      var tickets = await multi.ReadAsync<TicketDashboardView>();
      var totalCount = await multi.ReadSingleAsync<int>();
      return new TicketListResponse<TicketDashboardView> { Tickets = tickets, TotalCount = totalCount };
    }

    // --- User Methods ---
    public async Task<IEnumerable<User>> GetUsersByRoleAsync(string role)
    {
      if (role == "Customer")
      {
        string sql = $@"
            SELECT 
                c.CustomerId AS Id,
                c.CustomerCode AS UserNumber,
                c.CustomerName AS FullName,
                cd.EmailId AS Email,
                'Customer' AS Role,
                cd.DecisionMakerName AS ContactPerson,
                CAST(NULL AS NVARCHAR(50)) AS PhoneNo,
                cd.MobileNo,
                cd.ExecutiveId AS DefaultAssigneeId,
                (SELECT u.ManagerId FROM {DbConfig.CJDarclDb}.dbo.CL_Master_User u WHERE u.UserId = cd.ExecutiveId) AS ManagerId
            FROM {DbConfig.CJDarclDb}.dbo.CL_Master_Customer c
            INNER JOIN {DbConfig.CJDarclDb}.dbo.CL_Master_Customer_Detail cd ON c.CustomerId = cd.CustomerId
            WHERE c.IsActive = 1 AND c.GroupCode = 'C0001'
            ORDER BY c.CustomerName";
        return await _connection.QueryAsync<User>(sql);
      }
      else
      {
        string sql = $@"
            ;WITH MappedUsers AS (
                SELECT 
                    u.UserId AS Id,
                    u.UserName AS UserNumber,
                    u.Name AS FullName,
                    u.EmailId AS Email,
                    CASE 
                        WHEN u.RoleId = 1 THEN 'Super Admin'
                        WHEN u.UserId IN (
                            SELECT DISTINCT m.ManagerId 
                            FROM {DbConfig.CJDarclDb}.dbo.CL_Master_User m
                            WHERE m.ManagerId IS NOT NULL
                              AND m.UserId IN (SELECT DISTINCT m2.ManagerId FROM {DbConfig.CJDarclDb}.dbo.CL_Master_User m2 WHERE m2.ManagerId IS NOT NULL)
                        ) THEN 'SuperManager'
                        WHEN u.UserId IN (
                            SELECT DISTINCT m.ManagerId 
                            FROM {DbConfig.CJDarclDb}.dbo.CL_Master_User m
                            WHERE m.ManagerId IS NOT NULL
                        ) THEN 'PM'
                        ELSE 'Assignee'
                    END AS Role,
                    CAST(NULL AS NVARCHAR(100)) AS ContactPerson,
                    CAST(NULL AS NVARCHAR(50)) AS PhoneNo,
                    u.MobileNo,
                    CAST(NULL AS INT) AS DefaultAssigneeId,
                    u.ManagerId
                FROM {DbConfig.CJDarclDb}.dbo.CL_Master_User u
                WHERE u.IsActive = 1
            )
            SELECT * 
            FROM MappedUsers
            WHERE Role = @Role
            ORDER BY FullName";
        return await _connection.QueryAsync<User>(sql, new { Role = role });
      }
    }

    public async Task<User> CreateUserAsync(CreateUserRequest request)
    {
      var parameters = new
      {
        request.FullName,
        request.Password,
        request.Email,
        request.Role,
        request.ContactPerson,
        request.PhoneNo,
        request.MobileNo,
        ProductIds = string.Join(",", request.ProductIds ?? Enumerable.Empty<int>()),
        request.DefaultAssigneeId,
        request.ManagerId
      };
      return await _connection.QuerySingleAsync<User>("usp_CreateUser", parameters, commandType: CommandType.StoredProcedure);
    }

    public async Task UpdateUserAsync(int id, UpdateUserRequest request)
    {
      var parameters = new
      {
        Id = id,
        request.FullName,
        request.Email,
        request.Password,
        request.ContactPerson,
        request.PhoneNo,
        ProductIds = string.Join(",", request.ProductIds ?? Enumerable.Empty<int>()),
        request.DefaultAssigneeId,
        request.ManagerId
      };

      string sql = $@"
          IF EXISTS (SELECT 1 FROM {DbConfig.CJDarclDb}.dbo.CL_Master_Customer WHERE CustomerId = @Id)
          BEGIN
              UPDATE {DbConfig.CJDarclDb}.dbo.CL_Master_Customer 
              SET CustomerName = @FullName 
              WHERE CustomerId = @Id;

              UPDATE {DbConfig.CJDarclDb}.dbo.CL_Master_Customer_Detail 
              SET ExecutiveId = @DefaultAssigneeId, 
                  EmailId = @Email, 
                  DecisionMakerName = @ContactPerson 
              WHERE CustomerId = @Id;
          END
          ELSE
          BEGIN
              UPDATE dbo.Users 
              SET FullName = @FullName, 
                  Email = @Email, 
                  Password = COALESCE(NULLIF(@Password, ''), Password), 
                  ContactPerson = @ContactPerson, 
                  PhoneNo = @PhoneNo, 
                  DefaultAssigneeId = @DefaultAssigneeId, 
                  ManagerId = @ManagerId 
              WHERE Id = @Id;

              IF EXISTS (SELECT 1 FROM {DbConfig.CJDarclDb}.dbo.CL_Master_User WHERE UserId = @Id)
              BEGIN
                  UPDATE {DbConfig.CJDarclDb}.dbo.CL_Master_User 
                  SET Name = @FullName, 
                      EmailId = @Email, 
                      MobileNo = COALESCE(@PhoneNo, MobileNo), 
                      Password = COALESCE(NULLIF(@Password, ''), Password), 
                      ManagerId = @ManagerId 
                  WHERE UserId = @Id;
              END
          END

          DELETE FROM dbo.UserProducts WHERE UserId = @Id;

          IF @ProductIds IS NOT NULL AND LTRIM(RTRIM(@ProductIds)) <> ''
          BEGIN
              INSERT INTO dbo.UserProducts (UserId, ProductId)
              SELECT @Id, CAST(value AS INT)
              FROM STRING_SPLIT(@ProductIds, ',')
              WHERE TRY_CAST(value AS INT) IS NOT NULL;
          END

          DELETE FROM dbo.CustomerPMMappings WHERE CustomerId = @Id;

          IF @ManagerId IS NOT NULL
          BEGIN
              INSERT INTO dbo.CustomerPMMappings (CustomerId, PMId)
              VALUES (@Id, @ManagerId);
          END";

      await _connection.ExecuteAsync(sql, parameters);
    }

    public async Task UpdatePmAsync(int id, UpdatePmRequest request)
    {
      var parameters = new { Id = id, request.FullName, request.Email, request.Password, request.MobileNo, request.ManagerId };

      string sql = $@"
          IF EXISTS (SELECT 1 FROM dbo.Users WHERE Email = @Email AND Id <> @Id)
          BEGIN
              THROW 50000, 'This email address is already in use by another user.', 1;
          END

          UPDATE dbo.Users 
          SET FullName = @FullName, 
              Email = @Email, 
              MobileNo = @MobileNo, 
              ManagerId = @ManagerId, 
              Password = CASE WHEN @Password IS NOT NULL AND LTRIM(RTRIM(@Password)) <> '' THEN @Password ELSE Password END 
          WHERE Id = @Id AND Role IN ('PM', 'SuperManager', 'Super Admin');

          IF EXISTS (SELECT 1 FROM {DbConfig.CJDarclDb}.dbo.CL_Master_User WHERE UserId = @Id)
          BEGIN
              UPDATE {DbConfig.CJDarclDb}.dbo.CL_Master_User 
              SET Name = @FullName, 
                  EmailId = @Email, 
                  MobileNo = @MobileNo, 
                  Password = COALESCE(NULLIF(@Password, ''), Password), 
                  ManagerId = @ManagerId 
              WHERE UserId = @Id;
          END";

      await _connection.ExecuteAsync(sql, parameters);
    }

    public async Task<CommunicationAttachment?> GetCommunicationAttachmentByIdAsync(int communicationId, int attachmentId)
    {
      var sql = "SELECT * FROM CommunicationAttachments WHERE CommunicationId = @CommunicationId AND Id = @Id";
      return await _connection.QuerySingleOrDefaultAsync<CommunicationAttachment>(
          sql,
          new { CommunicationId = communicationId, Id = attachmentId }
      );
    }

    public async Task<IEnumerable<int>> GetPmIdsForCustomerAsync(int customerId)
    {
      string sql = $@"
          SELECT u.ManagerId 
          FROM {DbConfig.CJDarclDb}.dbo.CL_Master_Customer_Detail cd 
          INNER JOIN {DbConfig.CJDarclDb}.dbo.CL_Master_User u ON cd.ExecutiveId = u.UserId 
          WHERE cd.CustomerId = @CustomerId AND u.ManagerId IS NOT NULL";
      return await _connection.QueryAsync<int>(sql, new { CustomerId = customerId });
    }

    public async Task<Assignee?> GetExecutiveByCustomerIdAsync(int customerId)
    {
      string sql = $@"
          SELECT u.UserId AS Id, u.UserName AS AssigneeNumber, u.Name AS FullName, u.EmailId AS Email, u.MobileNo, u.ManagerId 
          FROM {DbConfig.CJDarclDb}.dbo.CL_Master_Customer_Detail cd
          INNER JOIN {DbConfig.CJDarclDb}.dbo.CL_Master_User u ON cd.ExecutiveId = u.UserId
          WHERE cd.CustomerId = @CustomerId;";
      return await _connection.QuerySingleOrDefaultAsync<Assignee>(sql, new { CustomerId = customerId });
    }

    public async Task<IEnumerable<int>> GetCustomerIdsByAssigneeIdAsync(int assigneeId)
    {
      string sql = $@"
          SELECT DISTINCT cd.CustomerId
          FROM {DbConfig.CJDarclDb}.dbo.CL_Master_Customer_Detail cd
          INNER JOIN {DbConfig.CJDarclDb}.dbo.CL_Master_Customer c ON c.CustomerId = cd.CustomerId
          WHERE cd.ExecutiveId = @AssigneeId AND c.IsActive = 1";
      return await _connection.QueryAsync<int>(sql, new { AssigneeId = assigneeId });
    }

    public async Task<IEnumerable<User>> GetCustomersByAssigneeIdAsync(int assigneeId)
    {
      string sql = $@"
          SELECT 
              c.CustomerId AS Id,
              c.CustomerCode AS UserNumber,
              c.CustomerName AS FullName,
              cd.EmailId AS Email,
              'Customer' AS Role,
              cd.DecisionMakerName AS ContactPerson,
              CAST(NULL AS NVARCHAR(50)) AS PhoneNo,
              cd.MobileNo,
              cd.ExecutiveId AS DefaultAssigneeId,
              (SELECT u.ManagerId FROM {DbConfig.CJDarclDb}.dbo.CL_Master_User u WHERE u.UserId = cd.ExecutiveId) AS ManagerId
          FROM {DbConfig.CJDarclDb}.dbo.CL_Master_Customer c
          INNER JOIN {DbConfig.CJDarclDb}.dbo.CL_Master_Customer_Detail cd ON c.CustomerId = cd.CustomerId
          WHERE cd.ExecutiveId = @AssigneeId AND c.IsActive = 1";
      return await _connection.QueryAsync<User>(sql, new { AssigneeId = assigneeId });
    }

    public async Task<IEnumerable<int>> GetCustomerIdsByPmIdAsync(int pmId)
    {
      string sql = $@"
          SELECT DISTINCT cd.CustomerId
          FROM {DbConfig.CJDarclDb}.dbo.CL_Master_Customer_Detail cd
          INNER JOIN {DbConfig.CJDarclDb}.dbo.CL_Master_Customer c ON c.CustomerId = cd.CustomerId
          INNER JOIN {DbConfig.CJDarclDb}.dbo.CL_Master_User u ON u.UserId = cd.ExecutiveId
          LEFT JOIN {DbConfig.CJDarclDb}.dbo.CL_Master_User mgr ON u.ManagerId = mgr.UserId
          WHERE (u.UserId = @PmId OR u.ManagerId = @PmId OR mgr.ManagerId = @PmId) AND c.IsActive = 1 AND c.GroupCode = 'C0001'";
      return await _connection.QueryAsync<int>(sql, new { PmId = pmId });
    }

    public async Task<User> GetUserByIdAsync(int id, string? role = null)
    {
      var parameters = new { Id = id, Role = role };
      string sql = "";

      if (role == "Customer")
      {
        sql = $@"
            SELECT 
                c.CustomerId AS Id,
                c.CustomerCode AS UserNumber,
                c.CustomerName AS FullName,
                cd.EmailId AS Email,
                'Customer' AS Role,
                cd.DecisionMakerName AS ContactPerson,
                CAST(NULL AS NVARCHAR(50)) AS PhoneNo,
                cd.MobileNo,
                cd.ExecutiveId AS DefaultAssigneeId,
                (SELECT u.ManagerId FROM {DbConfig.CJDarclDb}.dbo.CL_Master_User u WHERE u.UserId = cd.ExecutiveId) AS ManagerId
            FROM {DbConfig.CJDarclDb}.dbo.CL_Master_Customer c
            INNER JOIN {DbConfig.CJDarclDb}.dbo.CL_Master_Customer_Detail cd ON c.CustomerId = cd.CustomerId
            WHERE c.CustomerId = @Id;";
      }
      else
      {
        sql = $@"
            SELECT 
                u.UserId AS Id,
                u.UserName AS UserNumber,
                u.Name AS FullName,
                u.EmailId AS Email,
                CASE 
                    WHEN u.RoleId = 1 THEN 'Super Admin'
                    WHEN u.UserId IN (
                        SELECT DISTINCT m.ManagerId 
                        FROM {DbConfig.CJDarclDb}.dbo.CL_Master_User m
                        WHERE m.ManagerId IS NOT NULL
                          AND m.UserId IN (SELECT DISTINCT m2.ManagerId FROM {DbConfig.CJDarclDb}.dbo.CL_Master_User m2 WHERE m2.ManagerId IS NOT NULL)
                    ) THEN 'SuperManager'
                    WHEN u.UserId IN (
                        SELECT DISTINCT m.ManagerId 
                        FROM {DbConfig.CJDarclDb}.dbo.CL_Master_User m
                        WHERE m.ManagerId IS NOT NULL
                    ) THEN 'PM'
                    ELSE 'Assignee'
                END AS Role,
                CAST(NULL AS NVARCHAR(100)) AS ContactPerson,
                CAST(NULL AS NVARCHAR(50)) AS PhoneNo,
                u.MobileNo,
                CAST(NULL AS INT) AS DefaultAssigneeId,
                u.ManagerId
            FROM {DbConfig.CJDarclDb}.dbo.CL_Master_User u
            WHERE u.UserId = @Id;";
      }

      return await _connection.QuerySingleOrDefaultAsync<User>(sql, parameters);
    }

    public async Task DeleteUserAsync(int id)
    {
      await _connection.ExecuteAsync("usp_DeleteUser", new { Id = id }, commandType: CommandType.StoredProcedure);
    }

    public async Task<IEnumerable<int>> GetUserProductsAsync(int userId)
    {
      return await _connection.QueryAsync<int>("usp_GetUserProducts", new { UserId = userId }, commandType: CommandType.StoredProcedure);
    }

    public async Task<User> FindCustomerByNumberAsync(string customerNumber)
    {
      string sql = $@"
          SELECT 
              c.CustomerId AS Id,
              c.CustomerCode AS UserNumber,
              ISNULL(u.FullName, c.CustomerName) AS FullName,
              ISNULL(u.Email, cd.EmailId) AS Email,
              'Customer' AS Role,
              ISNULL(u.Password, cd.Password) AS Password,
              ISNULL(u.ContactPerson, cd.DecisionMakerName) AS ContactPerson,
              CAST(NULL AS NVARCHAR(50)) AS PhoneNo,
              ISNULL(u.MobileNo, cd.MobileNo) AS MobileNo,
              ISNULL(u.ManagerId, (SELECT mu.ManagerId FROM {DbConfig.CJDarclDb}.dbo.CL_Master_User mu WHERE mu.UserId = cd.ExecutiveId)) AS ManagerId,
              ISNULL(u.DefaultAssigneeId, cd.ExecutiveId) AS DefaultAssigneeId
          FROM {DbConfig.CJDarclDb}.dbo.CL_Master_Customer c
          INNER JOIN {DbConfig.CJDarclDb}.dbo.CL_Master_Customer_Detail cd ON c.CustomerId = cd.CustomerId
          LEFT JOIN dbo.Users u ON c.CustomerId = u.Id AND u.Role = 'Customer'
          WHERE c.CustomerCode = @UserNumber AND c.IsActive = 1;";
      return await _connection.QuerySingleOrDefaultAsync<User>(sql, new { UserNumber = customerNumber });
    }

    // --- Assignee Methods ---
    public async Task<Assignee> CreateAssigneeAsync(CreateAssigneeRequest request)
    {
      var parameters = new { request.FullName, request.Email, request.Password, request.MobileNo, request.ManagerId };
      return await _connection.QuerySingleAsync<Assignee>("usp_CreateAssignee", parameters, commandType: CommandType.StoredProcedure);
    }

    public async Task UpdateAssigneeAsync(int id, UpdateAssigneeRequest request)
    {
      var parameters = new { Id = id, request.FullName, request.Email, request.Password, request.MobileNo, request.ManagerId };
      await _connection.ExecuteAsync("usp_UpdateAssignee", parameters, commandType: CommandType.StoredProcedure);
    }

    public async Task<IEnumerable<Assignee>> GetAllAssigneesAsync(int? pmId = null)
    {
      string sql = $@"
          SELECT 
              u.UserId AS Id, 
              u.UserName AS AssigneeNumber, 
              u.Name AS FullName, 
              u.EmailId AS Email, 
              u.MobileNo, 
              u.ManagerId, 
              CASE 
                  WHEN u.UserTypeId = 4 THEN 'Customer' 
                  WHEN u.RoleId = 1 THEN 'Super Admin' 
                  WHEN u.UserId IN (
                      SELECT DISTINCT m.ManagerId 
                      FROM {DbConfig.CJDarclDb}.dbo.CL_Master_User m 
                      WHERE m.ManagerId IS NOT NULL 
                        AND m.UserId IN (SELECT DISTINCT m2.ManagerId FROM {DbConfig.CJDarclDb}.dbo.CL_Master_User m2 WHERE m2.ManagerId IS NOT NULL)
                  ) THEN 'SuperManager' 
                  WHEN u.UserId IN (
                      SELECT DISTINCT m.ManagerId 
                      FROM {DbConfig.CJDarclDb}.dbo.CL_Master_User m 
                      WHERE m.ManagerId IS NOT NULL
                  ) THEN 'PM' 
                  ELSE 'Assignee' 
              END AS Role 
          FROM {DbConfig.CJDarclDb}.dbo.CL_Master_User u 
          WHERE u.IsActive = 1 AND u.UserTypeId <> 4 AND (@PMId IS NULL OR u.ManagerId = @PMId OR u.UserId = @PMId)";
      return await _connection.QueryAsync<Assignee>(sql, new { PMId = pmId });
    }

    public async Task<Assignee> GetAssigneeByIdAsync(int id)
    {
      string sql = $@"
          SELECT 
              u.UserId AS Id, 
              u.UserName AS AssigneeNumber, 
              u.Name AS FullName, 
              u.EmailId AS Email, 
              u.MobileNo, 
              u.ManagerId 
          FROM {DbConfig.CJDarclDb}.dbo.CL_Master_User u
          WHERE u.UserId = @Id";
      return await _connection.QuerySingleOrDefaultAsync<Assignee>(sql, new { Id = id });
    }

    // --- Product Hierarchy Methods ---
    public async Task<IEnumerable<ProductData>> GetProductHierarchyAsync()
    {
      using var multi = await _connection.QueryMultipleAsync("usp_GetProductHierarchy", commandType: CommandType.StoredProcedure);
      var products = (await multi.ReadAsync<ProductData>()).ToList();
      var modules = (await multi.ReadAsync<ModuleData>()).ToList();
      var subModules = (await multi.ReadAsync<SubModuleData>()).ToList();
      foreach (var module in modules) { module.SubModules = subModules.Where(sm => sm.ModuleId == module.Id).ToList(); }
      foreach (var product in products) { product.Modules = modules.Where(m => m.ProductId == product.Id).ToList(); }
      return products;
    }

    public async Task<int> CreateOrUpdateProductAsync(ProductData product)
    {
      if (string.IsNullOrWhiteSpace(product.Name)) throw new ArgumentException("Product name cannot be null or empty");
      if (product.Modules?.Any(m => string.IsNullOrWhiteSpace(m.Name)) == true) throw new ArgumentException("All modules must have a valid name");
      foreach (var module in product.Modules ?? new List<ModuleData>()) { if (module.SubModules?.Any(sm => string.IsNullOrWhiteSpace(sm.Name)) == true) throw new ArgumentException($"All sub-modules in module '{module.Name}' must have a valid name"); }
      var serializerOptions = new JsonSerializerOptions { PropertyNamingPolicy = null };
      var parameters = new { Id = product.Id, Name = product.Name, ModulesJSON = JsonSerializer.Serialize(product.Modules, serializerOptions) };
      return await _connection.ExecuteScalarAsync<int>("usp_CreateOrUpdateProduct", parameters, commandType: CommandType.StoredProcedure);
    }

    public async Task<int> CreateProductSimpleAsync(SimpleProductRequest product)
    {
      var json = JsonSerializer.Serialize(product.Modules, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
      var parameters = new { Name = product.Name, ModulesJSON = json };
      return await _connection.ExecuteScalarAsync<int>("usp_CreateProductSimple", parameters, commandType: CommandType.StoredProcedure);
    }

    // --- Rework & Performance Methods ---
    public async Task<IEnumerable<TicketReworkInfo>> GetTicketReworkCountByUserAsync(int ticketId)
    {
      return await _connection.QueryAsync<TicketReworkInfo>("dbo.usp_GetTicketReworkCountByUser",
          new { TicketId = ticketId }, commandType: CommandType.StoredProcedure);
    }

    public async Task<IEnumerable<AssigneePerformanceStat>> GetAssigneePerformanceStatsAsync(int? filterMonth = null, int? filterYear = null, int? pmId = null)
    {
      var parameters = new DynamicParameters();
      parameters.Add("@FilterMonth", filterMonth, DbType.Int32);
      parameters.Add("@FilterYear", filterYear, DbType.Int32);
      parameters.Add("@PMId", pmId, DbType.Int32);

      return await _connection.QueryAsync<AssigneePerformanceStat>(
          "dbo.usp_GetAssigneePerformanceStats",
          parameters,
          commandType: CommandType.StoredProcedure);
    }

    public async Task<TotalReworkCounts> GetTotalReworkCountForAssigneeAsync(int assigneeId)
    {
      var result = await _connection.QuerySingleOrDefaultAsync<TotalReworkCounts>("dbo.usp_GetTotalReworkCountForAssignee",
          new { AssignedToId = assigneeId }, commandType: CommandType.StoredProcedure);
      return result ?? new TotalReworkCounts { TotalReworkCount = 0 };
    }
    
    // --- Communication Methods ---
    public async Task<PmCommunicationResponse> GetPmCommunicationsAsync(int ticketId)
    {
      var spName = "sp_GetTicketCommunications";
      var commentDictionary = new Dictionary<int, TicketCommunication>();

      await _connection.QueryAsync<TicketCommunication, CommunicationAttachment, TicketCommunication>(
          spName,
          (comment, attachment) =>
          {
            if (!commentDictionary.TryGetValue(comment.Id, out var commentEntry))
            {
              commentEntry = comment;
              commentDictionary.Add(commentEntry.Id, commentEntry);
            }
            if (attachment != null)
            {
              commentEntry.Attachments.Add(attachment);
            }
            return commentEntry;
          },
          new { TicketId = ticketId },
          splitOn: "Id",
          commandType: CommandType.StoredProcedure
      );

      var allComments = commentDictionary.Values.ToList();

      var response = new PmCommunicationResponse
      {
        CustomerChannel = allComments.Where(c => c.Channel == "Customer").ToList(),
        DeveloperChannel = allComments.Where(c => c.Channel == "Developer").ToList(),
        UpdateNotesCustomer = allComments.Where(c => c.Channel == "Customer(Note)").ToList(),
        UpdateNotesDeveloper = allComments.Where(c => c.Channel == "Developer(Note)").ToList(),
        GlobalNotes = allComments.Where(c => c.Channel == "GlobalNote" || c.Channel == "GlobalNotes").ToList()
      };
      return response;
    }

    public async Task<List<TicketCommunication>> GetChannelCommunicationsAsync(int ticketId, string channel)
    {
      var spName = "sp_GetTicketCommunications";
      var commentDictionary = new Dictionary<int, TicketCommunication>();

      await _connection.QueryAsync<TicketCommunication, CommunicationAttachment, TicketCommunication>(
          spName,
          (comment, attachment) =>
          {
            if (!commentDictionary.TryGetValue(comment.Id, out var commentEntry))
            {
              commentEntry = comment;
              commentDictionary.Add(commentEntry.Id, commentEntry);
            }
            if (attachment != null)
            {
              commentEntry.Attachments.Add(attachment);
            }
            return commentEntry;
          },
          new { TicketId = ticketId },
          splitOn: "Id",
          commandType: CommandType.StoredProcedure
      );

      var allComments = commentDictionary.Values.ToList();
      return allComments.Where(c => c.Channel.Equals(channel, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public async Task<TicketCommunication> AddCommunicationAsync(int ticketId, NewCommentRequest newComment, List<IFormFile> files)
    {
      var communication = new TicketCommunication
      {
        TicketId = ticketId,
        CommentText = newComment.CommentText,
        PostedOn = DateTime.UtcNow.AddMinutes(330),
        PostedByUserId = newComment.PostedByUserId,
        PostedByName = newComment.PostedByName,
        PostedByUserRole = newComment.PostedByUserRole,
        Channel = newComment.Channel
      };

      var newCommunicationId = await _connection.ExecuteScalarAsync<int>(
        "sp_InsertTicketCommunication",
        new
        {
          communication.TicketId,
          communication.CommentText,
          communication.PostedOn,
          communication.PostedByUserId,
          communication.PostedByName,
          communication.PostedByUserRole,
          communication.Channel
        },
        commandType: CommandType.StoredProcedure
      );
      
      communication.Id = newCommunicationId;

      if (files != null)
      {
        foreach (var file in files)
        {
          var fileUrl = await SaveFileToDiskAsync(ticketId, newCommunicationId, file);

          var attachment = new CommunicationAttachment
          {
            CommunicationId = newCommunicationId,
            FileName = file.FileName,
            FileUrl = fileUrl,
            FileSize = file.Length
          };

          await _connection.ExecuteAsync(
             "sp_InsertCommunicationAttachment",
             new
             {
               attachment.CommunicationId,
               attachment.FileName,
               attachment.FileUrl,
               attachment.FileSize
             },
             commandType: CommandType.StoredProcedure
          );
          communication.Attachments.Add(attachment);
        }
      }

      if (communication.Channel == null || !communication.Channel.EndsWith("(Note)", StringComparison.OrdinalIgnoreCase))
      {
        var historyEvent = $"Posted a comment to the {communication.Channel} channel.";
        await CreateHistoryEntryAsync(ticketId, communication.PostedByUserId, communication.PostedByName, communication.PostedByUserRole, historyEvent);
      }

      return communication;
    }

    private async Task<string> SaveFileToDiskAsync(int ticketId, int communicationId, IFormFile file)
    {
      var relativePath = Path.Combine("Uploads", "Tickets", ticketId.ToString(), "Comments", communicationId.ToString());
      var absolutePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relativePath);
      Directory.CreateDirectory(absolutePath);
      var uniqueFileName = $"{Guid.NewGuid()}_{file.FileName}";
      var fullFilePath = Path.Combine(absolutePath, uniqueFileName);
      await using (var stream = new FileStream(fullFilePath, FileMode.Create))
      {
        await file.CopyToAsync(stream);
      }
      return $"/{relativePath.Replace("\\", "/")}/{uniqueFileName}";
    }

    public async Task CreateHistoryEntryAsync(int ticketId, int userId, string userName, string userRole, string eventDescription)
    {
      await _connection.ExecuteAsync(
          "sp_InsertTicketHistory",
          new
          {
            TicketId = ticketId,
            ChangedByUserId = userId,
            ChangedByUserRole = userRole,
            EventDescription = eventDescription,
            ChangeDate = DateTime.UtcNow.AddMinutes(330)
          },
          commandType: CommandType.StoredProcedure
      );
      _logger.LogInformation("History created for ticket {TicketId}: {Event}", ticketId, eventDescription);
    }

    // --- Parent-Child Relations ---
    public async Task<TicketRelationsResponse> GetTicketRelationsAsync(int ticketId)
    {
      using var multi = await _connection.QueryMultipleAsync(
          "usp_GetTicketRelations",
          new { TicketId = ticketId },
          commandType: CommandType.StoredProcedure);

      var parent   = (await multi.ReadAsync<LinkedTicketSummary>()).FirstOrDefault();
      var children = (await multi.ReadAsync<LinkedTicketSummary>()).ToList();
      var tree     = (await multi.ReadAsync<LinkedTicketSummary>()).ToList();

      return new TicketRelationsResponse { Parent = parent, Children = children, Tree = tree };
    }

    public async Task SetTicketParentAsync(int childTicketId, int? parentTicketId)
    {
      await _connection.ExecuteAsync(
          "usp_SetTicketParent",
          new { ChildId = childTicketId, ParentId = parentTicketId },
          commandType: CommandType.StoredProcedure);
    }

    // --- Custom Ticket Statuses ---
    public async Task<IEnumerable<CustomTicketStatus>> GetCustomStatusesAsync()
    {
      return await _connection.QueryAsync<CustomTicketStatus>("SELECT * FROM CustomTicketStatuses WHERE IsActive = 1 ORDER BY Id");
    }

    public async Task<CustomTicketStatus> CreateCustomStatusAsync(string statusName)
    {
      var existing = await _connection.QueryFirstOrDefaultAsync<CustomTicketStatus>(
          "SELECT * FROM CustomTicketStatuses WHERE StatusName = @StatusName",
          new { StatusName = statusName });

      if (existing != null)
      {
        if (existing.IsActive)
        {
          throw new InvalidOperationException("Status already exists.");
        }
        else
        {
          await _connection.ExecuteAsync(
              "UPDATE CustomTicketStatuses SET IsActive = 1 WHERE Id = @Id",
              new { Id = existing.Id });
          existing.IsActive = true;
          return existing;
        }
      }

      var newId = await _connection.ExecuteScalarAsync<int>(
          "INSERT INTO CustomTicketStatuses (StatusName) OUTPUT INSERTED.Id VALUES (@StatusName)",
          new { StatusName = statusName });
      return new CustomTicketStatus { Id = newId, StatusName = statusName, IsActive = true };
    }

    public async Task DeleteCustomStatusAsync(int id)
    {
      await _connection.ExecuteAsync("UPDATE CustomTicketStatuses SET IsActive = 0 WHERE Id = @Id", new { Id = id });
    }

    // --- Custom Developer Statuses ---
    public async Task<IEnumerable<CustomTicketStatus>> GetCustomDeveloperStatusesAsync()
    {
      return await _connection.QueryAsync<CustomTicketStatus>("SELECT * FROM CustomDeveloperStatuses WHERE IsActive = 1 ORDER BY Id");
    }

    public async Task<CustomTicketStatus> CreateCustomDeveloperStatusAsync(string statusName)
    {
      var existing = await _connection.QueryFirstOrDefaultAsync<CustomTicketStatus>(
          "SELECT * FROM CustomDeveloperStatuses WHERE StatusName = @StatusName",
          new { StatusName = statusName });

      if (existing != null)
      {
        if (existing.IsActive)
        {
          throw new InvalidOperationException("Status already exists.");
        }
        else
        {
          await _connection.ExecuteAsync(
              "UPDATE CustomDeveloperStatuses SET IsActive = 1 WHERE Id = @Id",
              new { Id = existing.Id });
          existing.IsActive = true;
          return existing;
        }
      }

      var newId = await _connection.ExecuteScalarAsync<int>(
          "INSERT INTO CustomDeveloperStatuses (StatusName) OUTPUT INSERTED.Id VALUES (@StatusName)",
          new { StatusName = statusName });
      return new CustomTicketStatus { Id = newId, StatusName = statusName, IsActive = true };
    }

    public async Task DeleteCustomDeveloperStatusAsync(int id)
    {
      await _connection.ExecuteAsync("UPDATE CustomDeveloperStatuses SET IsActive = 0 WHERE Id = @Id", new { Id = id });
    }

    // --- Priorities ---
    public async Task<IEnumerable<PriorityMaster>> GetPrioritiesAsync()
    {
      return await _connection.QueryAsync<PriorityMaster>("SELECT * FROM Priorities WHERE IsActive = 1 ORDER BY Id");
    }

    public async Task<PriorityMaster> CreatePriorityAsync(string priorityName, int tatHours, int? responseSLA)
    {
      var existing = await _connection.QueryFirstOrDefaultAsync<PriorityMaster>(
          "SELECT * FROM Priorities WHERE PriorityName = @PriorityName",
          new { PriorityName = priorityName });

      if (existing != null)
      {
        await _connection.ExecuteAsync(
            "UPDATE Priorities SET IsActive = 1, TATHours = @TATHours, ResponseSLA = @ResponseSLA WHERE Id = @Id",
            new { Id = existing.Id, TATHours = tatHours, ResponseSLA = responseSLA });
        
        return new PriorityMaster
        {
          Id = existing.Id,
          PriorityName = priorityName,
          TATHours = tatHours,
          ResponseSLA = responseSLA,
          IsActive = true
        };
      }
      else
      {
        var newId = await _connection.ExecuteScalarAsync<int>(
            "INSERT INTO Priorities (PriorityName, TATHours, ResponseSLA, IsActive) OUTPUT INSERTED.Id VALUES (@PriorityName, @TATHours, @ResponseSLA, 1)",
            new { PriorityName = priorityName, TATHours = tatHours, ResponseSLA = responseSLA });
            
        return new PriorityMaster
        {
          Id = newId,
          PriorityName = priorityName,
          TATHours = tatHours,
          ResponseSLA = responseSLA,
          IsActive = true
        };
      }
    }

    public async Task DeletePriorityAsync(int id)
    {
      await _connection.ExecuteAsync("UPDATE Priorities SET IsActive = 0 WHERE Id = @Id", new { Id = id });
    }

    // --- Issue Categories ---
    public async Task<IEnumerable<IssueCategoryMaster>> GetIssueCategoriesAsync()
    {
      return await _connection.QueryAsync<IssueCategoryMaster>("SELECT * FROM IssueCategories WHERE IsActive = 1 ORDER BY Id");
    }

    public async Task<IssueCategoryMaster> CreateIssueCategoryAsync(string categoryName)
    {
      var newId = await _connection.ExecuteScalarAsync<int>(
          "INSERT INTO IssueCategories (CategoryName) OUTPUT INSERTED.Id VALUES (@CategoryName)",
          new { CategoryName = categoryName });
      return new IssueCategoryMaster { Id = newId, CategoryName = categoryName, IsActive = true };
    }

    public async Task DeleteIssueCategoryAsync(int id)
    {
      await _connection.ExecuteAsync("UPDATE IssueCategories SET IsActive = 0 WHERE Id = @Id", new { Id = id });
    }

    // --- Ticket Sources ---
    public async Task<IEnumerable<dynamic>> GetTicketSourcesAsync()
    {
      return await _connection.QueryAsync("SELECT * FROM TicketSources ORDER BY Id");
    }

    public async Task<int> AddTicketSourceAsync(string sourceName)
    {
      return await _connection.ExecuteScalarAsync<int>(
          "INSERT INTO TicketSources (SourceName) OUTPUT INSERTED.Id VALUES (@SourceName)",
          new { SourceName = sourceName });
    }

    public async Task DeleteTicketSourceAsync(int id)
    {
      await _connection.ExecuteAsync("DELETE FROM TicketSources WHERE Id = @Id", new { Id = id });
    }

    public async Task<TatDashboardStats> GetTatDashboardStatsAsync(int pmId, string? relationshipFilter = null, DateTime? fromDate = null, DateTime? toDate = null, string? statusFilter = null)
    {
      var stats = new TatDashboardStats();
      
      // Check CJDarcl first — it's the authoritative source for all CRM staff
      var staffSql = $@"
          SELECT 
              CASE 
                  WHEN u.RoleId = 1 THEN 'Super Admin'
                  WHEN u.UserId IN (
                      SELECT DISTINCT m.ManagerId 
                      FROM {DbConfig.CJDarclDb}.dbo.CL_Master_User m
                      WHERE m.ManagerId IS NOT NULL
                        AND m.UserId IN (SELECT DISTINCT m2.ManagerId FROM {DbConfig.CJDarclDb}.dbo.CL_Master_User m2 WHERE m2.ManagerId IS NOT NULL)
                  ) THEN 'SuperManager'
                  WHEN u.UserId IN (
                      SELECT DISTINCT m.ManagerId 
                      FROM {DbConfig.CJDarclDb}.dbo.CL_Master_User m
                      WHERE m.ManagerId IS NOT NULL
                  ) THEN 'PM'
                  ELSE 'Assignee'
              END AS Role
          FROM {DbConfig.CJDarclDb}.dbo.CL_Master_User u
          WHERE u.UserId = @pmId AND u.IsActive = 1";
      var userRole = await _connection.QueryFirstOrDefaultAsync<string>(staffSql, new { pmId });

      // Fallback to local Users table only if user is not in CJDarcl
      if (string.IsNullOrEmpty(userRole))
      {
          userRole = await _connection.QueryFirstOrDefaultAsync<string>(
              "SELECT Role FROM Users WHERE Id = @pmId",
              new { pmId });
      }

      var query = $@"
        SELECT 
            t.TicketId,
            t.TicketNumber,
            t.CustomerId,
            c.CustomerName as Customer,
            t.DocketNumber,
            t.Subject,
            t.Priority,
            t.Status,
            a.Name as AssignedTo,
            t.CreatedOn,
            t.ClosedOn,
            t.LastRepliedOn,
            t.LastDeadlineSet,
            pm.TATHours,
            COALESCE(prod.Name, t.Product) as CategoryName,
            creator.Name as CreatorName,
            t.IsCreatedByCustomer
        FROM Tickets t
        LEFT JOIN {DbConfig.CJDarclDb}.dbo.CL_Master_Customer c ON t.CustomerId = c.CustomerId
        LEFT JOIN {DbConfig.CJDarclDb}.dbo.CL_Master_User a ON t.AssignedTo = a.UserId
        LEFT JOIN {DbConfig.CJDarclDb}.dbo.CL_Master_User creator ON t.CreatedByUserId = creator.UserId
        LEFT JOIN Priorities pm ON t.Priority = pm.PriorityName
        LEFT JOIN Products prod ON (TRY_CAST(t.Product AS INT) = prod.Id OR t.Product = prod.Name)
        WHERE 1=1 ";

      if (relationshipFilter == "AssignedToMe") {
          query += " AND t.AssignedTo = @pmId ";
      } else if (relationshipFilter == "CreatedByMe") {
          query += " AND (t.CreatedByUserId = @pmId OR t.CustomerId = @pmId) ";
      } else if (relationshipFilter == "AssignedToJuniors") {
          query += " AND a.ManagerId = @pmId ";
      } else if (userRole == "SuperManager" || userRole == "Super Admin" || pmId == 118) {
          // Bypass organizational filters - SuperManager can see all tickets
      } else if (userRole == "Assignee") {
          query += " AND t.AssignedTo = @pmId ";
      } else {
          // If PMId is 118 (superadmin or similar), they can see everything, else they only see their juniors by default
          if (pmId == 118) {
              query += $" AND (a.ManagerId = @pmId OR @pmId = 118 OR t.CustomerId IN (SELECT cd.CustomerId FROM {DbConfig.CJDarclDb}.dbo.CL_Master_Customer_Detail cd JOIN {DbConfig.CJDarclDb}.dbo.CL_Master_User u ON cd.ExecutiveId = u.UserId WHERE u.ManagerId = @pmId)) ";
          } else {
              query += $" AND (a.ManagerId = @pmId OR t.AssignedTo = @pmId OR t.CreatedByUserId = @pmId OR t.CustomerId IN (SELECT cd.CustomerId FROM {DbConfig.CJDarclDb}.dbo.CL_Master_Customer_Detail cd JOIN {DbConfig.CJDarclDb}.dbo.CL_Master_User u ON cd.ExecutiveId = u.UserId WHERE u.ManagerId = @pmId)) ";
          }
      }

      if (fromDate.HasValue) {
          query += " AND CAST(t.CreatedOn AS DATE) >= CAST(@fromDate AS DATE) ";
      }
      if (toDate.HasValue) {
          query += " AND CAST(t.CreatedOn AS DATE) <= CAST(@toDate AS DATE) ";
      }
      if (!string.IsNullOrEmpty(statusFilter)) {
          query += " AND t.Status = @statusFilter ";
      }

      query += " ORDER BY t.CreatedOn DESC ";

      var parameters = new { pmId, fromDate = fromDate?.Date, toDate = toDate?.Date, statusFilter };
      var tickets = await _connection.QueryAsync<dynamic>(query, parameters);
      
      DateTime nowIst = DateTime.UtcNow.AddMinutes(330);

      int totalActive = 0;
      int openTickets = 0;
      int inProgressTickets = 0;
      int closedTickets = 0;
      int slaMet = 0;
      int slaTotal = 0;
      double totalResolutionWorkingHours = 0;
      int resolvedTicketsCount = 0;

      double totalRating = 0;
      int ratedTicketsCount = 0;

      var ratingsDict = new Dictionary<int, int> { {1, 0}, {2, 0}, {3, 0}, {4, 0}, {5, 0} };
      var categoriesDict = new Dictionary<string, int>();
      var customerTrackers = new Dictionary<int, (string Name, int Total, int Closed, int SlaMet, int SlaTotal, double TotalResTime, int ResCount, double TotalRating, int RatingCount)>();

      // Fetch first staff responses for all tickets
      var firstResponses = (await _connection.QueryAsync<(int TicketId, DateTime FirstStaffResponse)>(@"
          SELECT TicketId, MIN(PostedOn) AS FirstStaffResponse
          FROM TicketCommunications
          WHERE PostedByUserRole <> 'Customer'
          GROUP BY TicketId"))
          .ToDictionary(x => x.TicketId, x => x.FirstStaffResponse);

      foreach (var t in tickets)
      {
          int ticketId = t.TicketId;
          string status = t.Status ?? "Open";
          string priority = t.Priority ?? "Medium";
          DateTime createdOn = t.CreatedOn;
          string tktNum = t.TicketNumber != null ? t.TicketNumber : "TKT-" + t.TicketId.ToString("D3");
          
          bool isClosed = status.Equals("Closed", StringComparison.OrdinalIgnoreCase) || 
                           status.Equals("Resolved", StringComparison.OrdinalIgnoreCase) || 
                           status.Equals("Work Done", StringComparison.OrdinalIgnoreCase);
          
          // Determine deadline date
          DateTime effectiveDeadline;
          if (t.LastDeadlineSet != null)
          {
              effectiveDeadline = (DateTime)t.LastDeadlineSet;
          }
          else
          {
              double resolveSlaLimit = t.TATHours != null ? (double)(int)t.TATHours : 24.0;
              effectiveDeadline = createdOn.AddHours(resolveSlaLimit);
          }

          // Use ClosedOn if available for closed tickets, else LastRepliedOn, else nowIst
          DateTime resolveEndTime = nowIst;
          if (isClosed)
          {
              if (t.ClosedOn != null) resolveEndTime = (DateTime)t.ClosedOn;
              else if (t.LastRepliedOn != null) resolveEndTime = (DateTime)t.LastRepliedOn;
          }

          // Calculate Resolve SLA Breach: if close time (or current time for open tickets) > deadline -> Breached
          bool resolveBreached = isClosed ? (resolveEndTime > effectiveDeadline) : (nowIst > effectiveDeadline);
          double workingHoursToResolve = GetWorkingHoursElapsed(createdOn, resolveEndTime);

          // Determine final SLA Status
          string slaStatus = "On Track";
          if (resolveBreached)
          {
              slaStatus = "Breached";
          }
          else if (!isClosed)
          {
              TimeSpan totalWindow = effectiveDeadline - createdOn;
              if (totalWindow.TotalMinutes > 0)
              {
                  double elapsedRatio = (nowIst - createdOn).TotalMinutes / totalWindow.TotalMinutes;
                  if (elapsedRatio > 0.75)
                  {
                      slaStatus = "At Risk";
                  }
              }
          }

          // Count stats
          if (isClosed)
          {
              closedTickets++;
              totalResolutionWorkingHours += workingHoursToResolve;
              resolvedTicketsCount++;
          }
          else
          {
              totalActive++;
              if (status.Equals("Open", StringComparison.OrdinalIgnoreCase)) openTickets++;
              if (status.Equals("In Progress", StringComparison.OrdinalIgnoreCase)) inProgressTickets++;
          }

          // Rating processing
          if (t.Rating != null)
          {
              int ratingVal = (int)t.Rating;
              if (ratingVal >= 1 && ratingVal <= 5)
              {
                  ratingsDict[ratingVal]++;
                  totalRating += ratingVal;
                  ratedTicketsCount++;
              }
          }

          // Category/Product processing
          string catName = t.CategoryName ?? "General Support";
          if (categoriesDict.ContainsKey(catName))
          {
              categoriesDict[catName]++;
          }
          else
          {
              categoriesDict[catName] = 1;
          }

          // Customer tracking aggregation
          int customerId = t.CustomerId != null ? (int)t.CustomerId : 0;
          string customerName = t.Customer ?? "Unknown";

          if (customerId > 0)
          {
              if (!customerTrackers.ContainsKey(customerId))
              {
                  customerTrackers[customerId] = (customerName, 0, 0, 0, 0, 0.0, 0, 0.0, 0);
              }
              
              var cTracker = customerTrackers[customerId];
              int cTotal = cTracker.Total + 1;
              int cClosed = cTracker.Closed + (isClosed ? 1 : 0);
              int cSlaMet = cTracker.SlaMet + (slaStatus != "Breached" ? 1 : 0);
              int cSlaTotal = cTracker.SlaTotal + 1;
              double cTotalResTime = cTracker.TotalResTime + (isClosed ? workingHoursToResolve : 0);
              int cResCount = cTracker.ResCount + (isClosed ? 1 : 0);
              double cTotalRating = cTracker.TotalRating + (t.Rating != null ? (int)t.Rating : 0);
              int cRatingCount = cTracker.RatingCount + (t.Rating != null ? 1 : 0);

              customerTrackers[customerId] = (customerName, cTotal, cClosed, cSlaMet, cSlaTotal, cTotalResTime, cResCount, cTotalRating, cRatingCount);
          }

          if (slaStatus != "Breached")
          {
              slaMet++;
          }
          slaTotal++;

          bool isCustCreated = t.IsCreatedByCustomer != null && Convert.ToBoolean(t.IsCreatedByCustomer);
          string createdByStr = (!isCustCreated && !string.IsNullOrWhiteSpace((string)t.CreatorName))
              ? (string)t.CreatorName 
              : (t.Customer ?? "Customer");

          stats.RecentTickets.Add(new TatTicket {
              Id = t.TicketId,
              TicketId = tktNum,
              Customer = t.Customer ?? "Unknown",
              DocketNumber = t.DocketNumber ?? "-",
              Subject = t.Subject ?? "No Subject",
              Priority = priority,
              Status = status,
              AssignedTo = t.AssignedTo ?? "Unassigned",
              SlaStatus = slaStatus,
              CreatedOn = createdOn,
              CreatedBy = createdByStr,
              Rating = t.Rating,
              CategoryName = catName
          });
      }

      stats.TotalActiveTickets = totalActive;
      stats.OpenTickets = openTickets;
      stats.InProgressTickets = inProgressTickets;
      stats.ClosedTickets = closedTickets;
      stats.SlaCompliancePercent = slaTotal > 0 ? (int)Math.Round((double)slaMet / slaTotal * 100) : 100;
      stats.AverageResolutionTime = resolvedTicketsCount > 0 
          ? Math.Round(totalResolutionWorkingHours / resolvedTicketsCount, 1) 
          : 0.0;
      
      stats.AverageSatisfactionScore = ratedTicketsCount > 0
          ? Math.Round(totalRating / ratedTicketsCount, 1)
          : 0.0;

      foreach (var kvp in ratingsDict)
      {
          stats.RatingSummary.Add(new RatingCount { Rating = kvp.Key, Count = kvp.Value });
      }

      foreach (var kvp in categoriesDict)
      {
          stats.CategoryTrend.Add(new CategoryCount { CategoryName = kvp.Key, Count = kvp.Value });
      }

      foreach (var kvp in customerTrackers)
      {
          var tracker = kvp.Value;
          stats.CustomerStats.Add(new CustomerTatStat
          {
              CustomerId = kvp.Key,
              CustomerName = tracker.Name,
              TotalTickets = tracker.Total,
              ClosedTickets = tracker.Closed,
              SlaCompliancePercent = tracker.SlaTotal > 0 ? (int)Math.Round((double)tracker.SlaMet / tracker.SlaTotal * 100) : 100,
              AverageResolutionTime = tracker.ResCount > 0 ? Math.Round(tracker.TotalResTime / tracker.ResCount, 1) : 0.0,
              AverageSatisfactionScore = tracker.RatingCount > 0 ? Math.Round(tracker.TotalRating / tracker.RatingCount, 1) : 0.0
          });
      }

      return stats;
    }

    private static double GetWorkingHoursElapsed(DateTime start, DateTime end)
    {
      if (start >= end) return 0;

      double totalHours = 0;
      DateTime current = start;

      while (current.Date <= end.Date)
      {
          if (current.DayOfWeek == DayOfWeek.Saturday || current.DayOfWeek == DayOfWeek.Sunday)
          {
              current = current.Date.AddDays(1);
              continue;
          }

          DateTime workStart = current.Date.AddHours(9);
          DateTime workEnd = current.Date.AddHours(19);

          DateTime overlapStart = current > workStart ? current : workStart;
          DateTime overlapEnd = end < workEnd ? end : workEnd;

          if (overlapStart < overlapEnd)
          {
              totalHours += (overlapEnd - overlapStart).TotalHours;
          }

          current = current.Date.AddDays(1);
      }

      return totalHours;
    }
  }
}
