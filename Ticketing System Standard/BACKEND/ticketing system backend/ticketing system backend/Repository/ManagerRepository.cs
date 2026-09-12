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
        string? status, string? priority, int? customerId, string? assignedToId,
        DateTime? dateFrom, DateTime? dateTo, int pageNumber, int pageSize, int? userId = null, string? userRole = null)
    {
      var parameters = new
      {
        Status = status,
        Priority = priority,
        CustomerId = customerId,
        AssignedToId = assignedToId,
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
            AND (@DateFrom IS NULL OR t.CreatedOn >= @DateFrom)
            AND (@DateTo IS NULL OR t.CreatedOn < DATEADD(day, 1, @DateTo))
            AND (
                @UserRole IS NULL 
                OR @UserRole IN ('SuperManager', 'Super Admin')
                OR (@UserRole = 'Assignee' AND t.AssignedTo = @UserId)
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
            AND (@DateFrom IS NULL OR t.CreatedOn >= @DateFrom)
            AND (@DateTo IS NULL OR t.CreatedOn < DATEADD(day, 1, @DateTo))
            AND (
                @UserRole IS NULL 
                OR @UserRole IN ('SuperManager', 'Super Admin')
                OR (@UserRole = 'Assignee' AND t.AssignedTo = @UserId)
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
        OFFSET (@PageNumber - 1) * @PageSize ROWS
        FETCH NEXT @PageSize ROWS ONLY;
      ";

      using var multi = await _connection.QueryMultipleAsync(sql, parameters);
      var totalCount = await multi.ReadSingleAsync<int>();
      var ticketsList = (await multi.ReadAsync<TicketDashboardView>()).ToList();

      if (ticketsList.Any())
      {
        try
        {
          var ticketIds = ticketsList.Select(t => t.Id).ToList();

          // 1. Fetch Attachments
          try
          {
            var attachmentsData = (await _connection.QueryAsync<dynamic>(@"
                SELECT TicketId, AttachmentId AS Id, FileName, FilePath
                FROM dbo.TicketAttachments
                WHERE TicketId IN @TicketIds", new { TicketIds = ticketIds })).ToList();

            var attachmentsGrouped = attachmentsData
                .GroupBy(a => Convert.ToInt32(a.TicketId))
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(att => new TicketAttachment
                    {
                        Id = Convert.ToInt32(att.Id),
                        TicketId = Convert.ToInt32(att.TicketId),
                        FileName = (string)(att.FileName ?? "Attachment"),
                        FilePath = (string)(att.FilePath ?? "")
                    }).ToList()
                );

            foreach (var t in ticketsList)
            {
              if (attachmentsGrouped.TryGetValue(t.Id, out var attList))
              {
                t.Attachments = attList;
                t.AttachmentCount = attList.Count;
                t.HasAttachment = attList.Count > 0;
              }
            }
          }
          catch (Exception ex)
          {
            _logger.LogWarning(ex, "Failed to load ticket attachments in GetAllTicketsAsync");
          }

          // 2. Fetch Ticket Details (Description, Category, UpdatedOn, Module, SubModule)
          try
          {
            var detailsData = (await _connection.QueryAsync<dynamic>(@"
                SELECT t.TicketId AS Id, 
                       t.Description, 
                       t.TicketType AS Category,
                       c.CategoryName AS CategoryText,
                       t.LastRepliedOn AS UpdatedOn
                FROM dbo.Tickets t
                LEFT JOIN dbo.IssueCategories c ON (CAST(c.Id AS VARCHAR(50)) = CAST(t.TicketType AS VARCHAR(50)) OR c.CategoryName = CAST(t.TicketType AS VARCHAR(255)))
                WHERE t.TicketId IN @TicketIds", new { TicketIds = ticketIds })).ToList();

            var detailsDict = detailsData.ToDictionary(x => Convert.ToInt32(x.Id), x => x);

            foreach (var t in ticketsList)
            {
              if (detailsDict.TryGetValue(t.Id, out var d))
              {
                string? desc = (string?)d.Description;
                if (!string.IsNullOrWhiteSpace(desc))
                {
                  t.Description = desc;
                }
                string? catText = (string?)(d.CategoryText ?? d.Category);
                if (!string.IsNullOrWhiteSpace(catText))
                {
                  t.Category = catText;
                }
                if (t.UpdatedOn == null && d.UpdatedOn != null)
                {
                  t.UpdatedOn = (DateTime?)d.UpdatedOn;
                }
              }

              t.Module = !string.IsNullOrWhiteSpace(t.Product) ? t.Product : "General";
              t.SubModule = !string.IsNullOrWhiteSpace(t.SubProduct) ? t.SubProduct : "General";
            }
          }
          catch (Exception ex)
          {
            _logger.LogWarning(ex, "Failed to load ticket details in GetAllTicketsAsync");
          }

          // 3. Fetch Customer Codes
          try
          {
            var customerCodesData = (await _connection.QueryAsync<dynamic>(@"
                SELECT Id, UserNumber
                FROM dbo.Users
                WHERE Role = 'Customer'", new { })).ToList();

            var customerCodesDict = new Dictionary<int, string>();
            foreach (var c in customerCodesData)
            {
              int cid = Convert.ToInt32(c.Id);
              string code = (string?)(c.UserNumber) ?? cid.ToString();
              customerCodesDict[cid] = code;
            }

            foreach (var t in ticketsList)
            {
              if (customerCodesDict.TryGetValue(t.CustomerId, out var code))
              {
                t.CustomerCode = code;
              }
            }
          }
          catch (Exception ex)
          {
            _logger.LogWarning(ex, "Failed to load customer codes in GetAllTicketsAsync");
          }
        }
        catch (Exception ex)
        {
          _logger.LogWarning(ex, "Failed to enrich tickets in GetAllTicketsAsync");
        }
      }

      return new TicketListResponse<TicketDashboardView> { TotalCount = totalCount, Tickets = ticketsList };
    }

    public async Task UpdateTicketByDeveloperAsync(int ticketId, DeveloperUpdateRequest request)
    {
      await _connection.ExecuteAsync(@"
          UPDATE dbo.Tickets
          SET Status = @Status,
              LastRepliedOn = DATEADD(MINUTE, 330, GETUTCDATE())
          WHERE TicketId = @TicketId",
          new { TicketId = ticketId, Status = request.Status });
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

      await _connection.ExecuteAsync(@"
          UPDATE dbo.Tickets
          SET Status = 'Pending PM Review',
              LastRepliedOn = DATEADD(MINUTE, 330, GETUTCDATE()),
              LastUpdatedByUserId = @UserId,
              LastUpdatedByUserRole = @UserRole
          WHERE TicketId = @TicketId",
          new { TicketId = ticketId, UserId = changedByUserId, UserRole = userRole });
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

        var allUsers = await _connection.QueryAsync<User>(@"
            SELECT FullName, Role FROM dbo.Users");

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
              Difficulty = request.Difficulty,
              AssignedToId = request.AssignedToId,
              Note = request.Note,
              DeadlineDate = request.DeadlineDate
          },
          commandType: CommandType.StoredProcedure);
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

      await _connection.ExecuteAsync(@"
          UPDATE dbo.Tickets
          SET Status = @Status,
              LastRepliedOn = DATEADD(MINUTE, 330, GETUTCDATE()),
              LastUpdatedByUserId = ISNULL(@UserId, LastUpdatedByUserId),
              LastUpdatedByUserRole = ISNULL(@UserRole, LastUpdatedByUserRole),
              LastUpdateNote = @Note
          WHERE TicketId = @TicketId",
          new { TicketId = ticketId, Status = status, UserId = userId, UserRole = userRole, Note = note });
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

    public async Task<IEnumerable<TicketDeadline>> GetTicketDeadlinesBatchAsync(IEnumerable<int> ticketIds)
    {
      if (ticketIds == null || !ticketIds.Any()) return Enumerable.Empty<TicketDeadline>();
      const string sql = "SELECT TicketId, DeadlineDate, SetByManagerId, SetOn FROM dbo.TicketDeadlines WHERE TicketId IN @TicketIds;";
      return await _connection.QueryAsync<TicketDeadline>(sql, new { TicketIds = ticketIds });
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
      return await _connection.QueryAsync<User>("usp_GetUsersByRole", new { Role = role }, commandType: CommandType.StoredProcedure);
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
        request.ManagerId,
        AssigneeIds = string.Join(",", request.AssigneeIds ?? Enumerable.Empty<int>())
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
        request.ManagerId,
        AssigneeIds = string.Join(",", request.AssigneeIds ?? Enumerable.Empty<int>()),
        request.Role,
        request.IsActive
      };
      await _connection.ExecuteAsync("usp_UpdateUser", parameters, commandType: CommandType.StoredProcedure);
    }

    public async Task UpdatePmAsync(int id, UpdatePmRequest request)
    {
      var parameters = new { Id = id, request.FullName, request.Email, request.Password, request.MobileNo, request.ManagerId };
      await _connection.ExecuteAsync("usp_UpdatePmDetails", parameters, commandType: CommandType.StoredProcedure);
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
      const string sql = @"
          SELECT PMId 
          FROM dbo.CustomerPMMappings 
          WHERE CustomerId = @CustomerId";
      return await _connection.QueryAsync<int>(sql, new { CustomerId = customerId });
    }

    public async Task<Assignee?> GetExecutiveByCustomerIdAsync(int customerId)
    {
      return await _connection.QuerySingleOrDefaultAsync<Assignee>(
          "usp_GetExecutiveByCustomerId",
          new { CustomerId = customerId },
          commandType: CommandType.StoredProcedure
      );
    }

    public async Task<IEnumerable<int>> GetCustomerIdsByAssigneeIdAsync(int assigneeId)
    {
      const string sql = @"
          SELECT DISTINCT CustomerId
          FROM (
              SELECT CustomerId
              FROM dbo.CustomerAssigneeMappings
              WHERE AssigneeId = @AssigneeId
              UNION
              SELECT CustomerId
              FROM dbo.Tickets
              WHERE AssignedTo = @AssigneeId AND CustomerId IS NOT NULL
          ) AS CombinedCustomers";
      return await _connection.QueryAsync<int>(sql, new { AssigneeId = assigneeId });
    }

    public async Task<IEnumerable<int>> GetCustomerIdsByPmIdAsync(int pmId)
    {
      const string sql = @"
          SELECT Id AS CustomerId
          FROM dbo.Users
          WHERE Role = 'Customer' 
            AND (
                ManagerId = @PmId
                OR Id IN (SELECT CustomerId FROM dbo.CustomerPMMappings WHERE PMId = @PmId)
                OR Id IN (
                    SELECT CustomerId 
                    FROM dbo.CustomerAssigneeMappings 
                    WHERE AssigneeId IN (SELECT Id FROM dbo.Users WHERE ManagerId = @PmId)
                )
            )";
      return await _connection.QueryAsync<int>(sql, new { PmId = pmId });
    }

    public async Task<User> GetUserByIdAsync(int id, string? role = null)
    {
      return await _connection.QuerySingleOrDefaultAsync<User>("usp_GetUserById", new { Id = id, Role = role }, commandType: CommandType.StoredProcedure);
    }

    public async Task DeleteUserAsync(int id)
    {
      await _connection.ExecuteAsync("usp_DeleteUser", new { Id = id }, commandType: CommandType.StoredProcedure);
    }

    public async Task<IEnumerable<int>> GetUserProductsAsync(int userId)
    {
      return await _connection.QueryAsync<int>("usp_GetUserProducts", new { UserId = userId }, commandType: CommandType.StoredProcedure);
    }

    public async Task<IEnumerable<int>> GetUserAssigneesAsync(int userId)
    {
      const string sql = "SELECT AssigneeId FROM dbo.CustomerAssigneeMappings WHERE CustomerId = @UserId";
      return await _connection.QueryAsync<int>(sql, new { UserId = userId });
    }

    public async Task<User> FindCustomerByNumberAsync(string customerNumber)
    {
      const string sql = @"
          SELECT Id, UserNumber, FullName, Email, Role, Password, ContactPerson, PhoneNo, MobileNo, ManagerId, DefaultAssigneeId
          FROM dbo.Users
          WHERE UserNumber = @UserNumber AND Role = 'Customer'";
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

    public async Task<IEnumerable<Assignee>> GetAllAssigneesAsync(int? pmId = null, bool includeInactive = false)
    {
      return await _connection.QueryAsync<Assignee>("usp_GetAllAssignees", new { PMId = pmId, IncludeInactive = includeInactive ? 1 : 0 }, commandType: CommandType.StoredProcedure);
    }

    public async Task<Assignee> GetAssigneeByIdAsync(int id)
    {
      return await _connection.QuerySingleOrDefaultAsync<Assignee>("usp_GetAssigneeById", new { Id = id }, commandType: CommandType.StoredProcedure);
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

           var attachmentId = await _connection.ExecuteScalarAsync<int>(
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
           attachment.Id = attachmentId;
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

    public async Task<CustomTicketStatus> CreateCustomStatusAsync(CustomStatusRequest request)
    {
      var existing = await _connection.QueryFirstOrDefaultAsync<CustomTicketStatus>(
          "SELECT * FROM CustomTicketStatuses WHERE StatusName = @StatusName",
          new { StatusName = request.StatusName });

      if (request.IsDefaultUnassigned)
      {
        await _connection.ExecuteAsync("UPDATE CustomTicketStatuses SET IsDefaultUnassigned = 0");
      }

      if (existing != null)
      {
        if (existing.IsActive)
        {
          throw new InvalidOperationException("Status already exists.");
        }
        else
        {
          await _connection.ExecuteAsync(
              "UPDATE CustomTicketStatuses SET IsActive = 1, NotifyCustomer = @NotifyCustomer, NotifyPM = @NotifyPM, NotifyAssignee = @NotifyAssignee, IsDefaultUnassigned = @IsDefaultUnassigned WHERE Id = @Id",
              new { Id = existing.Id, request.NotifyCustomer, request.NotifyPM, request.NotifyAssignee, request.IsDefaultUnassigned });
          existing.IsActive = true;
          existing.NotifyCustomer = request.NotifyCustomer;
          existing.NotifyPM = request.NotifyPM;
          existing.NotifyAssignee = request.NotifyAssignee;
          existing.IsDefaultUnassigned = request.IsDefaultUnassigned;
          return existing;
        }
      }

      var newId = await _connection.ExecuteScalarAsync<int>(
          "INSERT INTO CustomTicketStatuses (StatusName, NotifyCustomer, NotifyPM, NotifyAssignee, IsDefaultUnassigned) OUTPUT INSERTED.Id VALUES (@StatusName, @NotifyCustomer, @NotifyPM, @NotifyAssignee, @IsDefaultUnassigned)",
          new { request.StatusName, request.NotifyCustomer, request.NotifyPM, request.NotifyAssignee, request.IsDefaultUnassigned });
      return new CustomTicketStatus { Id = newId, StatusName = request.StatusName, IsActive = true, NotifyCustomer = request.NotifyCustomer, NotifyPM = request.NotifyPM, NotifyAssignee = request.NotifyAssignee, IsDefaultUnassigned = request.IsDefaultUnassigned };
    }

    public async Task UpdateCustomStatusAsync(int id, CustomStatusRequest request)
    {
      if (request.IsDefaultUnassigned)
      {
        await _connection.ExecuteAsync("UPDATE CustomTicketStatuses SET IsDefaultUnassigned = 0");
      }

       await _connection.ExecuteAsync(
          "UPDATE CustomTicketStatuses SET StatusName = @StatusName, NotifyCustomer = @NotifyCustomer, NotifyPM = @NotifyPM, NotifyAssignee = @NotifyAssignee, IsDefaultUnassigned = @IsDefaultUnassigned WHERE Id = @Id",
          new { Id = id, request.StatusName, request.NotifyCustomer, request.NotifyPM, request.NotifyAssignee, request.IsDefaultUnassigned });
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

    public async Task<PriorityMaster> CreatePriorityAsync(string priorityName, int tatHours)
    {
      var newId = await _connection.ExecuteScalarAsync<int>(
          "INSERT INTO Priorities (PriorityName, TATHours) OUTPUT INSERTED.Id VALUES (@PriorityName, @TATHours)",
          new { PriorityName = priorityName, TATHours = tatHours });
      return new PriorityMaster { Id = newId, PriorityName = priorityName, TATHours = tatHours, IsActive = true };
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
      
      var userRole = await _connection.QueryFirstOrDefaultAsync<string>(
          "SELECT Role FROM Users WHERE Id = @pmId AND Role = 'Customer'",
          new { pmId });

      if (string.IsNullOrEmpty(userRole))
      {
          var staffSql = @"
              SELECT Role FROM dbo.Users WHERE Id = @pmId";
          userRole = await _connection.QueryFirstOrDefaultAsync<string>(staffSql, new { pmId });
      }

      var query = @"
        SELECT 
            t.TicketId,
            t.TicketNumber,
            c.FullName as Customer,
            t.DocketNumber,
            t.Subject,
            t.Priority,
            t.Status,
            a.FullName as AssignedTo,
            t.CreatedOn,
            t.LastRepliedOn,
            pm.TATHours
        FROM Tickets t
        LEFT JOIN dbo.Users c ON t.CustomerId = c.Id
        LEFT JOIN dbo.Users a ON t.AssignedTo = a.Id
        LEFT JOIN Priorities pm ON t.Priority = pm.PriorityName
        WHERE 1=1 ";

      if (userRole == "Assignee") {
          query += " AND t.AssignedTo = @pmId ";
      } else if (relationshipFilter == "AssignedToMe") {
          query += " AND t.AssignedTo = @pmId ";
      } else if (relationshipFilter == "CreatedByMe") {
          query += " AND t.CustomerId = @pmId ";
      } else if (relationshipFilter == "AssignedToJuniors") {
          query += " AND a.ManagerId = @pmId ";
      } else {
          // If PMId is 118 (superadmin or similar), they can see everything, else they only see their juniors by default
          if (pmId == 118) {
              query += " AND (a.ManagerId = @pmId OR @pmId = 118 OR t.CustomerId IN (SELECT Id FROM dbo.Users WHERE ManagerId = @pmId)) ";
          } else {
              query += " AND (a.ManagerId = @pmId OR t.AssignedTo = @pmId OR t.CustomerId IN (SELECT Id FROM dbo.Users WHERE ManagerId = @pmId)) ";
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
      
      int totalActive = 0;
      int openTickets = 0;
      int inProgressTickets = 0;
      int slaMet = 0;
      int slaTotal = 0;

      foreach (var t in tickets)
      {
          string status = t.Status ?? "Open";
          int tatHours = t.TATHours != null ? (int)t.TATHours : 24; // default 24h
          DateTime createdOn = t.CreatedOn;
          string tktNum = t.TicketNumber != null ? t.TicketNumber : "TKT-" + t.TicketId.ToString("D3");
          
          string slaStatus = "On Track";
          bool isClosed = status.Equals("Closed", StringComparison.OrdinalIgnoreCase) || status.Equals("Resolved", StringComparison.OrdinalIgnoreCase) || status.Equals("Work Done", StringComparison.OrdinalIgnoreCase);
          
          TimeSpan elapsed = DateTime.Now - createdOn;
          if (isClosed)
          {
              // Using LastRepliedOn as a proxy for closed date.
              DateTime closedDate = t.LastRepliedOn != null ? (DateTime)t.LastRepliedOn : DateTime.Now;
              TimeSpan timeToClose = closedDate - createdOn;
              if (timeToClose.TotalHours > tatHours) {
                  slaStatus = "Breached";
              } else {
                  slaStatus = "On Track";
                  slaMet++;
              }
              slaTotal++;
          }
          else
          {
              totalActive++;
              if (status.Equals("Open", StringComparison.OrdinalIgnoreCase)) openTickets++;
              if (status.Equals("In Progress", StringComparison.OrdinalIgnoreCase)) inProgressTickets++;
              
              if (elapsed.TotalHours > tatHours) {
                  slaStatus = "Breached";
              } else if (elapsed.TotalHours > (tatHours * 0.75)) {
                  slaStatus = "At Risk";
                  slaMet++; // still not breached
              } else {
                  slaStatus = "On Track";
                  slaMet++;
              }
              slaTotal++;
          }

          stats.RecentTickets.Add(new TatTicket {
              TicketId = tktNum,
              Customer = t.Customer ?? "Unknown",
              DocketNumber = t.DocketNumber ?? "-",
              Subject = t.Subject ?? "No Subject",
              Priority = t.Priority ?? "Medium",
              Status = status,
              AssignedTo = t.AssignedTo ?? "Unassigned",
              SlaStatus = slaStatus
          });
      }

      stats.TotalActiveTickets = totalActive;
      stats.OpenTickets = openTickets;
      stats.InProgressTickets = inProgressTickets;
      stats.SlaCompliancePercent = slaTotal > 0 ? (int)Math.Round((double)slaMet / slaTotal * 100) : 100;

      return stats;
    }
  }
}
