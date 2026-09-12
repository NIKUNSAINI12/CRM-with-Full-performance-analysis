using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ticketing_system_backend.API.Interfaces;
using ticketing_system_backend.Helpers;
using ticketing_system_backend.Models;

namespace ticketing_system_backend.API.Repositories
{
  public class TicketRepository : ITicketRepository
  {
    private readonly IDbConnection _connection;
    private readonly string _fileUploadPath;
    private readonly string _cjDarclConnectionString;
    private readonly ILogger<TicketRepository> _logger;

    public TicketRepository(IDbConnection connection, IConfiguration configuration, ILogger<TicketRepository> logger)
    {
      _connection = connection;
      _fileUploadPath = configuration.GetValue<string>("FileUploadPath")
          ?? Path.Combine(Directory.GetCurrentDirectory(), "uploads");
      _cjDarclConnectionString = configuration.GetConnectionString("CJDarclConnection") ?? "";
      _logger = logger;

      // Ensure upload directory exists
      if (!Directory.Exists(_fileUploadPath))
      {
        Directory.CreateDirectory(_fileUploadPath);
      }
    }

    public async Task UpdateAsync(int ticketId, UpdateTicketRequest request)
    {
      var parameters = new
      {
        TicketId = ticketId,
        Status = request.Status,
        Priority = request.Priority,
        AssignedToId = request.AssignedToId,
      };

      await _connection.ExecuteAsync(
          "usp_UpdateTicketByPM",
          parameters,
          commandType: CommandType.StoredProcedure
      );

      string? note = !string.IsNullOrWhiteSpace(request.Note) ? request.Note : request.Remark;
      if (!string.IsNullOrWhiteSpace(note))
      {
        bool isClosed = request.Status?.Equals("Closed", StringComparison.OrdinalIgnoreCase) == true ||
                        request.Status?.Equals("Close", StringComparison.OrdinalIgnoreCase) == true ||
                        request.Status?.Equals("Work Done", StringComparison.OrdinalIgnoreCase) == true ||
                        request.Status?.Equals("Resolved", StringComparison.OrdinalIgnoreCase) == true;

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
                new { Note = note.Trim(), TicketId = ticketId }
            );
          }
          catch (Exception exNote)
          {
            _logger.LogWarning(exNote, "Could not update LastUpdateNote for ticket {TicketId}", ticketId);
          }
        }
      }
    }


    public async Task<Ticket> CreateAsync(CreateTicketRequest ticketRequest)
    {
      // Optional Docket Validation against MKFoods23 database
      if (!string.IsNullOrWhiteSpace(ticketRequest.DocketNumber))
      {
          if (string.IsNullOrEmpty(_cjDarclConnectionString))
          {
              throw new Exception("CJDarclConnection string is not configured.");
          }

          using (var mkConnection = new SqlConnection(_cjDarclConnectionString))
          {
              var isValidDocket = await mkConnection.ExecuteScalarAsync<bool>(
                  "SELECT CAST(COUNT(1) AS BIT) FROM cl_docket WHERE DocketNo = @DocketNumber", // Assuming column name is DocketNo
                  new { DocketNumber = ticketRequest.DocketNumber }
              );

              if (!isValidDocket)
              {
                  throw new Exception($"Invalid Docket Number: {ticketRequest.DocketNumber}. Not found in MKFoods database.");
              }
          }
      }

      if (_connection.State != ConnectionState.Open)
      {
        if (_connection is DbConnection dbConn)
        {
          await dbConn.OpenAsync();
        }
        else
        {
          _connection.Open();
        }
      }

      using var transaction = _connection.BeginTransaction();

      try
      {
        int? resolvedProductId = ticketRequest.ProductId;
        int? resolvedModuleId = ticketRequest.ModuleId;
        int? resolvedSubModuleId = ticketRequest.SubModuleId;

        if (!string.IsNullOrWhiteSpace(ticketRequest.ProductName))
        {
          var prodName = ticketRequest.ProductName.Trim();
          var existingProduct = await _connection.QueryFirstOrDefaultAsync<dynamic>(
              "SELECT Id, Name FROM dbo.Products WHERE Name = @Name", new { Name = prodName }, transaction);
          if (existingProduct != null)
          {
            resolvedProductId = (int)existingProduct.Id;
          }
          else
          {
            resolvedProductId = await _connection.ExecuteScalarAsync<int>(
                "INSERT INTO dbo.Products (Name) OUTPUT INSERTED.Id VALUES (@Name)", new { Name = prodName }, transaction);
          }

          // Ensure product is mapped to customer in UserProducts
          var isMapped = await _connection.ExecuteScalarAsync<bool>(
              "SELECT CAST(COUNT(1) AS BIT) FROM dbo.UserProducts WHERE UserId = @UserId AND ProductId = @ProductId",
              new { UserId = ticketRequest.CustomerId, ProductId = resolvedProductId }, transaction);
          if (!isMapped)
          {
            await _connection.ExecuteAsync(
                "INSERT INTO dbo.UserProducts (UserId, ProductId) VALUES (@UserId, @ProductId)",
                new { UserId = ticketRequest.CustomerId, ProductId = resolvedProductId }, transaction);
          }
        }

        if (resolvedProductId.HasValue && !string.IsNullOrWhiteSpace(ticketRequest.ModuleName))
        {
          var modName = ticketRequest.ModuleName.Trim();
          var existingModule = await _connection.QueryFirstOrDefaultAsync<dynamic>(
              "SELECT Id, Name FROM dbo.Modules WHERE Name = @Name AND ProductId = @ProductId",
              new { Name = modName, ProductId = resolvedProductId.Value }, transaction);
          if (existingModule != null)
          {
            resolvedModuleId = (int)existingModule.Id;
          }
          else
          {
            resolvedModuleId = await _connection.ExecuteScalarAsync<int>(
                "INSERT INTO dbo.Modules (Name, ProductId) OUTPUT INSERTED.Id VALUES (@Name, @ProductId)",
                new { Name = modName, ProductId = resolvedProductId.Value }, transaction);
          }
        }

        if (resolvedModuleId.HasValue && !string.IsNullOrWhiteSpace(ticketRequest.SubModuleName))
        {
          var subModName = ticketRequest.SubModuleName.Trim();
          var existingSubModule = await _connection.QueryFirstOrDefaultAsync<dynamic>(
              "SELECT Id, Name FROM dbo.SubModules WHERE Name = @Name AND ModuleId = @ModuleId",
              new { Name = subModName, ModuleId = resolvedModuleId.Value }, transaction);
          if (existingSubModule != null)
          {
            resolvedSubModuleId = (int)existingSubModule.Id;
          }
          else
          {
            resolvedSubModuleId = await _connection.ExecuteScalarAsync<int>(
                "INSERT INTO dbo.SubModules (Name, ModuleId) OUTPUT INSERTED.Id VALUES (@Name, @ModuleId)",
                new { Name = subModName, ModuleId = resolvedModuleId.Value }, transaction);
          }
        }

        var newTicket = await _connection.QuerySingleAsync<Ticket>(
            "usp_CreateTicket",
            new
            {
                ticketRequest.Subject,
                ticketRequest.Description,
                ticketRequest.Priority,
                ticketRequest.TicketType,
                ProductId = resolvedProductId?.ToString() ?? "",
                ModuleId = resolvedModuleId?.ToString() ?? "",
                SubModuleId = resolvedSubModuleId?.ToString() ?? "",
                ticketRequest.CustomerId,
                ticketRequest.DocketNumber,
                ticketRequest.TicketSource,
                ticketRequest.AssignedToId
            },
            transaction: transaction,
            commandType: CommandType.StoredProcedure
        );

        if (ticketRequest.DeadlineDate.HasValue)
        {
            var managerId = 118; // Default PM/Admin ID fallback
            var resolvedManagerId = await _connection.ExecuteScalarAsync<int?>(
                "SELECT ManagerId FROM dbo.Users WHERE Id = @CustomerId",
                new { CustomerId = ticketRequest.CustomerId },
                transaction
            );
            if (resolvedManagerId.HasValue && resolvedManagerId.Value > 0)
            {
                managerId = resolvedManagerId.Value;
            }

            await _connection.ExecuteAsync(@"
                INSERT INTO dbo.TicketDeadlines (TicketId, DeadlineDate, SetByManagerId, SetOn)
                VALUES (@TicketId, @DeadlineDate, @ManagerId, DATEADD(MINUTE, 330, GETUTCDATE()))",
                new { TicketId = newTicket.Id, DeadlineDate = ticketRequest.DeadlineDate.Value, ManagerId = managerId },
                transaction
            );
        }

        if (ticketRequest.Attachments != null && ticketRequest.Attachments.Any())
        {
            foreach (var attachmentFile in ticketRequest.Attachments)
            {
                if (attachmentFile.Length > 0)
                {
                    var savedAttachment = await SaveAttachmentAsync(attachmentFile, newTicket.Id, ticketRequest.CustomerId, _connection, transaction);
                    newTicket.Attachments.Add(savedAttachment);
                }
            }
        }

        // Persist IsCreatedByCustomer & update initial TicketHistory event to log creator type
        bool isCustomerCreated = ticketRequest.IsCreatedByCustomer ?? true;
        int? actualCreatorId = ticketRequest.CreatedByUserId;

        try
        {
          await _connection.ExecuteAsync(
              $"UPDATE [{DbConfig.TicketingDb}].dbo.Tickets SET IsCreatedByCustomer = @IsCustomerCreated WHERE TicketId = @TicketId;",
              new { IsCustomerCreated = isCustomerCreated ? 1 : 0, TicketId = newTicket.Id },
              transaction: transaction
          );

          if (actualCreatorId.HasValue && actualCreatorId.Value > 0)
          {
            await _connection.ExecuteAsync(
                $"UPDATE [{DbConfig.TicketingDb}].dbo.Tickets SET CreatedByUserId = @CreatedByUserId WHERE TicketId = @TicketId;",
                new { CreatedByUserId = actualCreatorId.Value, TicketId = newTicket.Id },
                transaction: transaction
            );
          }

          if (!string.IsNullOrWhiteSpace(ticketRequest.SourcePhone) || !string.IsNullOrWhiteSpace(ticketRequest.SourceEmail))
          {
            await _connection.ExecuteAsync(
                $"UPDATE [{DbConfig.TicketingDb}].dbo.Tickets SET SourcePhone = @SourcePhone, SourceEmail = @SourceEmail WHERE TicketId = @TicketId;",
                new { SourcePhone = ticketRequest.SourcePhone, SourceEmail = ticketRequest.SourceEmail, TicketId = newTicket.Id },
                transaction: transaction
            );
          }

          string creatorName = isCustomerCreated ? (newTicket.CustomerName ?? "Customer") : "Internal User";
          string creatorRole = isCustomerCreated ? "Customer" : "Internal Staff";

          if (!isCustomerCreated && actualCreatorId.HasValue && actualCreatorId.Value > 0)
          {
            var staffName = await _connection.ExecuteScalarAsync<string>(
                $"SELECT TOP 1 Name FROM [{DbConfig.CJDarclDb}].dbo.CL_Master_User WHERE UserId = @UserId",
                new { UserId = actualCreatorId.Value },
                transaction: transaction
            );
            if (!string.IsNullOrWhiteSpace(staffName))
            {
              creatorName = staffName;
            }
          }

          string creatorEventDesc = $"Ticket Created by {creatorName} ({creatorRole}).";

          // Include assignment in initial creation event if assigned (request or default)
          int? effectiveAssigneeId = ticketRequest.AssignedToId ?? (int.TryParse(newTicket.AssignedTo, out int aid) && aid > 0 ? aid : (int?)null);
          if (effectiveAssigneeId.HasValue && effectiveAssigneeId.Value > 0)
          {
            var assigneeName = await _connection.ExecuteScalarAsync<string>(
                $"SELECT TOP 1 Name FROM [{DbConfig.CJDarclDb}].dbo.CL_Master_User WHERE UserId = @UserId",
                new { UserId = effectiveAssigneeId.Value },
                transaction: transaction
            );
            string assignedNameStr = !string.IsNullOrWhiteSpace(assigneeName) ? assigneeName : $"User #{effectiveAssigneeId.Value}";
            creatorEventDesc += $" Assigned to {assignedNameStr}.";
          }

          // Include deadline in initial creation event if set (request or auto-calculated)
          DateTime? effectiveDeadline = ticketRequest.DeadlineDate ?? newTicket.LastDeadlineSet;
          if (effectiveDeadline.HasValue)
          {
            string deadlineFormatted = effectiveDeadline.Value.ToString("dd-MM-yyyy HH:mm");
            creatorEventDesc += $" Deadline set to {deadlineFormatted}.";
          }

          await _connection.ExecuteAsync(
              $"IF EXISTS (SELECT 1 FROM [{DbConfig.TicketingDb}].dbo.TicketHistory WHERE TicketId = @TicketId AND (EventDescription LIKE '%Created%' OR EventDescription LIKE '%created%')) " +
              $"  UPDATE [{DbConfig.TicketingDb}].dbo.TicketHistory SET ChangedByUserId = @CreatorId, ChangedByUserRole = @CreatorRole, EventDescription = @EventDesc WHERE TicketId = @TicketId AND (EventDescription LIKE '%Created%' OR EventDescription LIKE '%created%');" +
              "ELSE " +
              "  EXEC sp_InsertTicketHistory @TicketId = @TicketId, @ChangedByUserId = @CreatorId, @ChangedByUserRole = @CreatorRole, @EventDescription = @EventDesc, @ChangeDate = NULL;",
              new {
                  TicketId = newTicket.Id,
                  CreatorId = actualCreatorId ?? ticketRequest.CustomerId,
                  CreatorRole = creatorRole,
                  EventDesc = creatorEventDesc
              },
              transaction: transaction
          );

          newTicket.IsCreatedByCustomer = isCustomerCreated;
          newTicket.CreatedByName = creatorName;
          newTicket.CreatedByRole = creatorRole;
        }
        catch (Exception exHistory)
        {
          _logger.LogWarning(exHistory, "Could not update IsCreatedByCustomer or history for ticket {TicketId}", newTicket.Id);
        }

        if (transaction is DbTransaction dbTrans)
        {
          await dbTrans.CommitAsync();
        }
        else
        {
          transaction.Commit();
        }
        
        _logger.LogInformation("Ticket {TicketId} created successfully for customer {CustomerId} (IsCreatedByCustomer={IsCreatedByCustomer})", newTicket.Id, ticketRequest.CustomerId, isCustomerCreated);

        return newTicket;
      }
      catch (Exception ex)
      {
        if (transaction is DbTransaction dbTrans)
        {
          await dbTrans.RollbackAsync();
        }
        else
        {
          transaction.Rollback();
        }
        _logger.LogError(ex, "Failed to create ticket for customer {CustomerId}", ticketRequest.CustomerId);
        throw;
      }
    }

    public async Task<Ticket?> GetByIdAsync(int id)
    {
      try
      {
        string sql = $@"
            SELECT 
                t.TicketId AS Id,
                t.TicketNumber,
                t.Subject,
                t.Description,
                t.Status,
                t.Priority,
                COALESCE(pProd.Name, t.Product) AS Product,
                t.SubProduct,
                CONVERT(NVARCHAR(100), t.AssignedTo) AS AssignedTo,
                t.CreatedOn,
                t.LastRepliedOn,
                t.CustomerId,
                t.DocketNumber,
                t.TicketSource,
                t.SourcePhone,
                t.SourceEmail,
                t.Rating,
                t.IsCreatedByCustomer,
                t.CreatedByUserId,
                c.CustomerName AS CustomerName,
                uAssignee.Name AS AssignedToName,
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
                ) AS CreatedByRole,
                t.LastUpdateNote AS LastUpdateNote,
                t.LastUpdateNote AS ClosingRemark
            FROM [{DbConfig.TicketingDb}].dbo.Tickets t
            LEFT JOIN [{DbConfig.TicketingDb}].dbo.Products pProd ON TRY_CAST(t.Product AS INT) = pProd.Id
            LEFT JOIN [{DbConfig.CJDarclDb}].dbo.CL_Master_Customer c ON t.CustomerId = c.CustomerId
            LEFT JOIN [{DbConfig.CJDarclDb}].dbo.CL_Master_Customer_Detail cdCreator ON c.CustomerId = cdCreator.CustomerId
            LEFT JOIN [{DbConfig.CJDarclDb}].dbo.CL_Master_User uAssignee ON TRY_CAST(t.AssignedTo AS INT) = uAssignee.UserId
            LEFT JOIN [{DbConfig.CJDarclDb}].dbo.CL_Master_User uCreator ON t.CreatedByUserId = uCreator.UserId
            WHERE t.TicketId = @TicketId;

            SELECT 
                ta.AttachmentId AS Id,
                ta.TicketId,
                ta.FileName,
                ta.FilePath,
                ta.FileSize,
                ta.ContentType,
                ta.UploadedOn,
                ta.UploadedById,
                ta.Name AS UploadedBy
            FROM [{DbConfig.TicketingDb}].dbo.TicketAttachments ta
            WHERE ta.TicketId = @TicketId
            ORDER BY ta.UploadedOn DESC;";

        Ticket? ticket = null;
        using (var multi = await _connection.QueryMultipleAsync(sql, new { TicketId = id }))
        {
          ticket = await multi.ReadSingleOrDefaultAsync<Ticket>();
          if (ticket != null)
          {
            ticket.Attachments = (await multi.ReadAsync<TicketAttachment>()).ToList();
          }
        }

        return ticket;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to get ticket by id {TicketId}", id);
        throw;
      }
    }

    public async Task<IEnumerable<TicketHistory>> GetTicketHistoryAsync(int ticketId)
    {
      return await _connection.QueryAsync<TicketHistory>(
          "usp_GetTicketHistory",
          new { TicketId = ticketId },
          commandType: CommandType.StoredProcedure
      );
    }

    public async Task<IEnumerable<ProductData>> GetProductHierarchyAsync(int customerId)
    {
      var sql = @"
        SELECT p.Id, p.Name 
        FROM dbo.Products p
        INNER JOIN dbo.UserProducts up ON p.Id = up.ProductId
        WHERE up.UserId = @CustomerId;

        SELECT m.Id, m.Name, m.ProductId 
        FROM dbo.Modules m
        WHERE m.ProductId IN (SELECT ProductId FROM dbo.UserProducts WHERE UserId = @CustomerId);

        SELECT sm.Id, sm.Name, sm.ModuleId
        FROM dbo.SubModules sm
        WHERE sm.ModuleId IN (
            SELECT Id FROM dbo.Modules WHERE ProductId IN (
                SELECT ProductId FROM dbo.UserProducts WHERE UserId = @CustomerId
            )
        );";

      using var multi = await _connection.QueryMultipleAsync(sql, new { CustomerId = customerId });

      var products = (await multi.ReadAsync<ProductData>()).ToList();
      var modules = (await multi.ReadAsync<ModuleData>()).ToList();
      var subModules = (await multi.ReadAsync<SubModuleData>()).ToList();

      if (!products.Any())
      {
        var allSql = @"
          SELECT Id, Name FROM dbo.Products;
          SELECT Id, Name, ProductId FROM dbo.Modules;
          SELECT Id, Name, ModuleId FROM dbo.SubModules;";
        using var allMulti = await _connection.QueryMultipleAsync(allSql);
        products = (await allMulti.ReadAsync<ProductData>()).ToList();
        modules = (await allMulti.ReadAsync<ModuleData>()).ToList();
        subModules = (await allMulti.ReadAsync<SubModuleData>()).ToList();
      }

      foreach (var module in modules)
      {
        module.SubModules = subModules.Where(sm => sm.ModuleId == module.Id).ToList();
      }

      foreach (var product in products)
      {
        product.Modules = modules.Where(m => m.ProductId == product.Id).ToList();
      }

      return products;
    }

    public async Task<IEnumerable<Product>> GetProductsByCustomerIdAsync(int customerId)
    {
      return await _connection.QueryAsync<Product>(
          "usp_GetProductsByCustomerId",
          new { CustomerId = customerId },
          commandType: CommandType.StoredProcedure
      );
    }

    public async Task<TicketResponse> GetAllForCustomerAsync(
        int customerId,
        string? status = null,
        string? priority = null,
        string? product = null,
        string? subProduct = null,
        string? assignedTo = null,
        DateTime? dateFrom = null,
        DateTime? dateTo = null)
    {
      try
      {
        var parameters = new
        {
          CustomerId = customerId,
          Status = status,
          Priority = priority,
          Product = product,
          SubProduct = subProduct,
          AssignedTo = assignedTo,
          DateFrom = dateFrom,
          DateTo = dateTo
        };

        string sql = $@"
            SELECT 
                t.TicketId AS Id,
                t.Subject,
                t.TicketNumber,
                t.Description,
                t.Status,
                t.Priority,
                COALESCE(pProd.Name, t.Product) AS Product,
                t.SubProduct,
                CONVERT(NVARCHAR(100), t.AssignedTo) AS AssignedTo,
                t.CreatedOn,
                t.ClosedOn,
                t.LastRepliedOn,
                t.CustomerId,
                t.DocketNumber,
                t.IsCreatedByCustomer,
                t.CreatedByUserId,
                c.CustomerName AS CustomerName,
                uAssignee.Name AS AssignedToName,
                uAssignee.EmailId AS AssignedToEmail,
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
                ) AS CreatedByRole,
                t.LastUpdateNote AS LastUpdateNote,
                t.LastUpdateNote AS ClosingRemark
            FROM [{DbConfig.TicketingDb}].dbo.Tickets t
            LEFT JOIN [{DbConfig.TicketingDb}].dbo.Products pProd ON TRY_CAST(t.Product AS INT) = pProd.Id
            LEFT JOIN [{DbConfig.CJDarclDb}].dbo.CL_Master_Customer c ON t.CustomerId = c.CustomerId
            LEFT JOIN [{DbConfig.CJDarclDb}].dbo.CL_Master_Customer_Detail cdCreator ON c.CustomerId = cdCreator.CustomerId
            LEFT JOIN [{DbConfig.CJDarclDb}].dbo.CL_Master_User uAssignee ON TRY_CAST(t.AssignedTo AS INT) = uAssignee.UserId
            LEFT JOIN [{DbConfig.CJDarclDb}].dbo.CL_Master_User uCreator ON t.CreatedByUserId = uCreator.UserId
            WHERE t.CustomerId = @CustomerId
              AND (@Status IS NULL OR t.Status IN (SELECT value FROM STRING_SPLIT(@Status, ',')))
              AND (@Priority IS NULL OR t.Priority IN (SELECT value FROM STRING_SPLIT(@Priority, ',')))
              AND (@Product IS NULL OR t.Product IN (SELECT value FROM STRING_SPLIT(@Product, ',')))
              AND (@SubProduct IS NULL OR t.SubProduct IN (SELECT value FROM STRING_SPLIT(@SubProduct, ',')))
              AND (@AssignedTo IS NULL OR t.AssignedTo IN (SELECT value FROM STRING_SPLIT(@AssignedTo, ',')))
              AND (@DateFrom IS NULL OR t.CreatedOn >= @DateFrom)
              AND (@DateTo IS NULL OR t.CreatedOn < DATEADD(day, 1, @DateTo))
            ORDER BY t.CreatedOn DESC;

            SELECT 
                ta.AttachmentId AS Id,
                ta.TicketId,
                ta.FileName,
                ta.FilePath,
                ta.FileSize,
                ta.ContentType AS FileType,
                ta.UploadedOn,
                ta.UploadedById,
                ta.Name AS UploadedBy
            FROM [{DbConfig.TicketingDb}].dbo.TicketAttachments ta
            WHERE ta.TicketId IN (
                SELECT TicketId FROM [{DbConfig.TicketingDb}].dbo.Tickets WHERE CustomerId = @CustomerId
            );";

        using var multi = await _connection.QueryMultipleAsync(sql, parameters);

        var tickets = (await multi.ReadAsync<Ticket>()).ToList();
        var attachments = (await multi.ReadAsync<TicketAttachment>()).ToList();

        var attachmentGroups = attachments.GroupBy(a => a.TicketId);
        foreach (var group in attachmentGroups)
        {
          var ticket = tickets.FirstOrDefault(t => t.Id == group.Key);
          if (ticket != null)
          {
            ticket.Attachments = group.ToList();
          }
        }

        return new TicketResponse
        {
          Tickets = tickets,
          TotalCount = tickets.Count
        };
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to get tickets for customer {CustomerId}", customerId);
        throw;
      }
    }



    public async Task<TicketResponse> GetByStatusAsync(string status)
    {
      try
      {
        using var multi = await _connection.QueryMultipleAsync(
            "usp_GetTicketsByStatus",
            new { Status = status },
            commandType: CommandType.StoredProcedure
        );

        var tickets = (await multi.ReadAsync<Ticket>()).ToList();
        var attachments = (await multi.ReadAsync<TicketAttachment>()).ToList();

        var attachmentGroups = attachments.GroupBy(a => a.TicketId);
        foreach (var group in attachmentGroups)
        {
          var ticket = tickets.FirstOrDefault(t => t.Id == group.Key);
          if (ticket != null)
          {
            ticket.Attachments = group.ToList();
          }
        }

        return new TicketResponse
        {
          Tickets = tickets,
          TotalCount = tickets.Count
        };
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to get tickets by status {Status}", status);
        throw;
      }
    }

    public async Task<TicketAttachment?> GetAttachmentAsync(int ticketId, int attachmentId)
    {
      try
      {
        return await _connection.QuerySingleOrDefaultAsync<TicketAttachment>(
            "usp_GetTicketAttachment",
            new { TicketId = ticketId, AttachmentId = attachmentId },
            commandType: CommandType.StoredProcedure
        );
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to get attachment {AttachmentId} for ticket {TicketId}", attachmentId, ticketId);
        throw;
      }
    }

    public async Task<TicketAttachment> SaveAttachmentAsync(
        IFormFile file,
        int ticketId,
        int uploadedById,
        IDbConnection connection,
        IDbTransaction transaction)
    {
      var uniqueFileName = $"{Guid.NewGuid()}_{file.FileName}";
      var filePath = Path.Combine(_fileUploadPath, uniqueFileName);

      using (var stream = new FileStream(filePath, FileMode.Create))
      {
        await file.CopyToAsync(stream);
      }

      var attachment = await connection.QuerySingleAsync<TicketAttachment>(
          "usp_AddTicketAttachment",
          new
          {
            TicketId = ticketId,
            FileName = file.FileName,
            FilePath = filePath,
            FileSize = file.Length,
            ContentType = file.ContentType,
            UploadedById = uploadedById
          },
          transaction: transaction,
          commandType: CommandType.StoredProcedure
      );

      return attachment;
    }

    public async Task<IEnumerable<dynamic>> GetDocketsAsync(string? search = null, int? customerId = null)
    {
        if (string.IsNullOrEmpty(_cjDarclConnectionString))
        {
            throw new Exception("CJDarclConnection string is not configured.");
        }

        using (var cjConnection = new SqlConnection(_cjDarclConnectionString))
        {
            string sql;
            object parameters;

            if (customerId.HasValue)
            {
                if (string.IsNullOrWhiteSpace(search))
                {
                    sql = @"SELECT TOP 20 d.* 
                            FROM cl_docket d 
                            INNER JOIN CL_Docket_Charge c ON d.DocketId = c.DocketId 
                            WHERE c.ContractCustomerId = @CustomerId 
                            ORDER BY d.DocketDate DESC";
                    parameters = new { CustomerId = customerId.Value };
                }
                else
                {
                    sql = @"SELECT TOP 20 d.* 
                            FROM cl_docket d 
                            INNER JOIN CL_Docket_Charge c ON d.DocketId = c.DocketId 
                            WHERE c.ContractCustomerId = @CustomerId AND d.DocketNo LIKE @Search 
                            ORDER BY d.DocketDate DESC";
                    parameters = new { CustomerId = customerId.Value, Search = $"%{search}%" };
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(search))
                {
                    sql = "SELECT TOP 20 * FROM cl_docket ORDER BY DocketDate DESC";
                    parameters = new { };
                }
                else
                {
                    sql = "SELECT TOP 20 * FROM cl_docket WHERE DocketNo LIKE @Search ORDER BY DocketDate DESC";
                    parameters = new { Search = $"%{search}%" };
                }
            }

            var dockets = await cjConnection.QueryAsync(sql, parameters);
            return dockets;
        }
    }

    public async Task<object?> GetDocketDetailsAsync(string docketNo)
    {
        if (string.IsNullOrEmpty(_cjDarclConnectionString))
        {
            throw new Exception("CJDarclConnection string is not configured.");
        }

        using (var cjConnection = new SqlConnection(_cjDarclConnectionString))
        {
            var docketIdVal = await cjConnection.QueryFirstOrDefaultAsync<long?>(
                "SELECT DocketId FROM dbo.CL_Docket WITH (NOLOCK) WHERE DocketNo = @DocketNo",
                new { DocketNo = docketNo });
            
            if (docketIdVal == null)
            {
                return null;
            }
            long docketId = docketIdVal.Value;

            using (var multi = await cjConnection.QueryMultipleAsync(
                "Usp_Api_GetDocketStatus",
                new { DocketNo = docketNo },
                commandType: CommandType.StoredProcedure))
            {
                var summary = await multi.ReadFirstOrDefaultAsync<dynamic>();
                if (summary == null)
                {
                    return null;
                }

                var pkgWeight = await multi.ReadFirstOrDefaultAsync<dynamic>();
                var historyRaw = (await multi.ReadAsync<dynamic>()).ToList();
                var podsRaw = (await multi.ReadAsync<dynamic>()).ToList();

                // Fetch additional metadata fields not returned by the USP (BillingParty, PaymentType, TransportMode, Remarks)
                var additionalSql = @"
                    SELECT  paymentType.CodeDescription PaymentType,
                            transportMode.CodeDescription TransportMode,
                            billingParty.CustomerCode + ' : ' + billingParty.CustomerName BillingParty,
                            CASE WHEN docketDetail.Remarks = '' THEN 'No Remarks'
                                 ELSE docketDetail.Remarks
                            END Remarks
                    FROM dbo.CL_Docket docket WITH ( NOLOCK )
                    LEFT JOIN dbo.CL_Docket_Detail docketDetail WITH ( NOLOCK ) ON docket.DocketId = docketDetail.DocketId
                    LEFT JOIN dbo.CL_Docket_Charge C WITH ( NOLOCK ) ON C.DocketId = docket.DocketId
                    LEFT JOIN dbo.CL_Master_Customer billingParty WITH ( NOLOCK ) ON billingParty.CustomerId = C.ContractCustomerId
                    LEFT JOIN dbo.CL_Master_General paymentType ON paymentType.CodeTypeId = 14
                                                                  AND paymentType.CodeId = docket.PaybasId
                    LEFT JOIN dbo.CL_Master_General transportMode ON transportMode.CodeTypeId = 15
                                                                  AND transportMode.CodeId = docket.TransportModeId
                    WHERE docket.DocketId = @docketId";
                var additional = await cjConnection.QueryFirstOrDefaultAsync<dynamic>(additionalSql, new { docketId });

                var details = new DocketDetails
                {
                    DocketId = docketId,
                    DocketNo = summary.DocketNo,
                    Status = summary.DocketStatus,
                    DocketDate = summary.DocketDateTime,
                    Edd = summary.EDD,
                    BillingParty = additional?.BillingParty,
                    DeliveryDate = summary.DeliveryDateTime,
                    PaymentType = additional?.PaymentType,
                    CurrentLocation = summary.CurrentLocation,
                    FromCity = summary.Origin,
                    ToCity = summary.Destination,
                    Remarks = additional?.Remarks,
                    Packages = pkgWeight?.TotalPackages != null ? Convert.ToInt32(pkgWeight.TotalPackages) : 0,
                    TransportMode = additional?.TransportMode,
                    Consignor = summary.ConsignorName,
                    Consignee = summary.ConsigneeName,
                    PodDocumentName = podsRaw.FirstOrDefault()?.DocumentName
                };

                var history = historyRaw.Select(h => new DocketHistory
                {
                    ActivityAtLocation = h.Status,
                    ActivityDateTime = h.StatusDate,
                    TransitLocation = h.Location,
                    Remark = h.Remark,
                    StatusPhotoName = h.StatusPhotoName,
                    StatusPhotoType = h.StatusPhotoType
                }).ToList();

                return new { details, history, pods = podsRaw };
            }
        }
    }

    public async Task<bool> UpdateRatingAsync(int ticketId, int rating)
    {
      var rowsAffected = await _connection.ExecuteAsync(
          "UPDATE dbo.Tickets SET Rating = @Rating WHERE TicketId = @TicketId",
          new { Rating = rating, TicketId = ticketId }
      );
      return rowsAffected > 0;
    }

    public async Task<IEnumerable<dynamic>> SearchGlobalTicketsAsync(string query, string searchType = "auto")
    {
      string type = (searchType ?? "auto").ToLower();
      string sql = $@"
          SELECT TOP 50 
              t.TicketId AS id,
              t.TicketNumber AS ticketNumber,
              t.DocketNumber AS docketNumber,
              t.Subject AS subject,
              t.Status AS status,
              t.Priority AS priority,
              t.CustomerId AS customerId,
              t.CreatedOn AS createdAt,
              ISNULL(c.CustomerName, 'N/A') AS customerName
          FROM [{DbConfig.TicketingDb}].dbo.Tickets t
          LEFT JOIN [{DbConfig.CJDarclDb}].dbo.CL_Master_Customer c ON t.CustomerId = c.CustomerId
          WHERE 
              (@Type = 'ticket' AND (
                  t.TicketNumber LIKE @LikeQuery OR 
                  CAST(t.TicketId AS NVARCHAR) = @ExactQuery OR 
                  t.Subject LIKE @LikeQuery OR 
                  t.Description LIKE @LikeQuery OR 
                  c.CustomerName LIKE @LikeQuery
              ))
              OR
              (@Type = 'docket' AND (
                  t.DocketNumber LIKE @LikeQuery OR 
                  t.DocketNumber = @ExactQuery
              ))
              OR
              (@Type = 'auto' AND (
                  t.TicketNumber LIKE @LikeQuery OR 
                  CAST(t.TicketId AS NVARCHAR) = @ExactQuery OR 
                  t.DocketNumber LIKE @LikeQuery OR 
                  t.DocketNumber = @ExactQuery OR
                  t.Subject LIKE @LikeQuery OR
                  t.Description LIKE @LikeQuery OR
                  c.CustomerName LIKE @LikeQuery OR
                  t.Product LIKE @LikeQuery OR
                  t.SubProduct LIKE @LikeQuery
              ))
          ORDER BY t.TicketId DESC";

      var parameters = new
      {
        Type = type,
        ExactQuery = query,
        LikeQuery = $"%{query}%"
      };

      return await _connection.QueryAsync<dynamic>(sql, parameters);
    }
  }
}
