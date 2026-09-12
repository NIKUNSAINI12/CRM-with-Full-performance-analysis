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
using ticketing_system_backend.Models;

namespace ticketing_system_backend.API.Repositories
{
  public class TicketRepository : ITicketRepository
  {
    private readonly IDbConnection _connection;
    private readonly string _fileUploadPath;
    private readonly ILogger<TicketRepository> _logger;

    public TicketRepository(IDbConnection connection, IConfiguration configuration, ILogger<TicketRepository> logger)
    {
      _connection = connection;
      _fileUploadPath = configuration.GetValue<string>("FileUploadPath")
          ?? Path.Combine(Directory.GetCurrentDirectory(), "uploads");
      _logger = logger;

      // Ensure upload directory exists
      if (!Directory.Exists(_fileUploadPath))
      {
        Directory.CreateDirectory(_fileUploadPath);
      }
    }

    public async Task UpdateAsync(int ticketId, UpdateTicketRequest request, int? pmId = null)
    {
      var parameters = new
      {
        TicketId = ticketId,
        PmId = pmId,
        Status = request.Status,
        Priority = request.Priority,
        AssignedToId = request.AssignedToId,
      };

      await _connection.ExecuteAsync(
          "usp_UpdateTicketByPM",
          parameters,
          commandType: CommandType.StoredProcedure
      );
    }


    public async Task<Ticket> CreateAsync(CreateTicketRequest ticketRequest)
    {


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
                ticketRequest.AssignedToId,
                CreatedByUserId = ticketRequest.CreatedByUserId,
                CreatedByUserRole = ticketRequest.CreatedByUserRole
            },
            transaction: transaction,
            commandType: CommandType.StoredProcedure
        );

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

        if (transaction is DbTransaction dbTrans)
        {
          await dbTrans.CommitAsync();
        }
        else
        {
          transaction.Commit();
        }
        
        _logger.LogInformation("Ticket {TicketId} created successfully for customer {CustomerId}", newTicket.Id, ticketRequest.CustomerId);



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

        using var multi = await _connection.QueryMultipleAsync(
            "usp_GetTicketsByCustomerId",
            parameters,
            commandType: CommandType.StoredProcedure
        );

        var ticketsDynamic = (await multi.ReadAsync<dynamic>()).ToList();
        var attachments = (await multi.ReadAsync<TicketAttachment>()).ToList();

        var tickets = ticketsDynamic.Select(t => new Ticket
        {
          Id = t.Id,
          Subject = t.Subject,
          Status = t.Status,
          Priority = t.Priority,
          CreatedOn = t.CreatedOn,
          LastRepliedOn = t.LastRepliedOn,
          CustomerId = t.CustomerId,
          CustomerName = t.CustomerName,
          AssignedToName = t.AssignedToName,
          TicketNumber = t.TicketNumber,
          Product = t.Product?.ToString(),
          SubProduct = t.SubProduct?.ToString(),
          AssignedTo = t.AssignedTo?.ToString(),
          DocketNumber = t.DocketNumber?.ToString()
        }).ToList();

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

    public async Task<Ticket?> GetByIdAsync(int id)
    {
      try
      {
        using var multi = await _connection.QueryMultipleAsync(
            "usp_GetTicketDetailsById",
            new { TicketId = id },
            commandType: CommandType.StoredProcedure
        );

        var ticket = await multi.ReadSingleOrDefaultAsync<Ticket>();
        if (ticket != null)
        {
          var attachments = (await multi.ReadAsync<TicketAttachment>()).ToList();
          ticket.Attachments = attachments;
        }

        return ticket;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to get ticket {TicketId}", id);
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


  }
}
// Trigger rebuild
