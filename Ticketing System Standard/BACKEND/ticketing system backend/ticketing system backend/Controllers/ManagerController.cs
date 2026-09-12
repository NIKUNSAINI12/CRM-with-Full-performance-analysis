using System;
using System.Collections.Generic;
using Dapper;
using System.Data;
using System.IO; // Trigger dotnet watch rebuild
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using ticketing_system_backend.API.Interfaces;
using System.Security.Claims;
using ticketing_system_backend.Interface;
using ticketing_system_backend.Models;
using ticketing_system_backend.Helpers;
using OfficeOpenXml;

namespace ticketing_system_backend.Controllers
{
  [ApiController]
  [Route("api/[controller]")]
  [Authorize]   // All manager endpoints require authentication
  public class ManagerController : ControllerBase
  {
    private readonly IManager _managerRepo;
    private readonly ILogger<ManagerController> _logger;
    private readonly IEmailService _emailService;
    private readonly IWebHostEnvironment _webHostEnvironment;
    private readonly ITicketRepository _ticketRepository; 
    private readonly INotificationService _notificationService;
    private readonly string _connectionString;
    private readonly IRolesRepository _rolesRepository;

    public ManagerController(ILogger<ManagerController> logger, IManager managerRepo, ITicketRepository ticketRepository, IEmailService emailService, IWebHostEnvironment webHostEnvironment, INotificationService notificationService, Microsoft.Extensions.Configuration.IConfiguration configuration, IRolesRepository rolesRepository)
    {
      _logger = logger;
      _managerRepo = managerRepo;
      _ticketRepository = ticketRepository; 
      _emailService = emailService;
      _webHostEnvironment = webHostEnvironment;
      _notificationService = notificationService;
      _connectionString = configuration.GetConnectionString("DefaultConnection") ?? "";
      _rolesRepository = rolesRepository;
    }

    // --- CUSTOMER FILTERING ---
    [HttpGet("all-customers")]
    public async Task<IActionResult> GetAllCustomers()
    {
      var allCustomers = await _managerRepo.GetUsersByRoleAsync("Customer");
      return Ok(allCustomers);
    }

    [HttpGet("my-customers")]
    public async Task<IActionResult> GetMyCustomers()
    {
      var uidClaim = User.FindFirst("uid")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
      var roleClaim = User.FindFirst("role")?.Value ?? User.FindFirst(ClaimTypes.Role)?.Value;

      if (!int.TryParse(uidClaim, out var userId))
        return Unauthorized();

      if (roleClaim == "SuperManager" || roleClaim == "Super Admin")
      {
        var allCustomers = await _managerRepo.GetUsersByRoleAsync("Customer");
        return Ok(allCustomers);
      }

      IEnumerable<int> customerIds;
      if (roleClaim == "Assignee" || roleClaim == "Developer")
      {
        customerIds = await _managerRepo.GetCustomerIdsByAssigneeIdAsync(userId);
      }
      else if (roleClaim == "PM" || roleClaim == "Project Manager" || roleClaim == "Manager" || roleClaim == "TL")
      {
        customerIds = await _managerRepo.GetCustomerIdsByPmIdAsync(userId);
      }
      else
      {
        var allCust = await _managerRepo.GetUsersByRoleAsync("Customer");
        return Ok(allCust);
      }

      var allCustomersList = await _managerRepo.GetUsersByRoleAsync("Customer");
      var filtered = allCustomersList.Where(c => customerIds.Contains(c.Id)).ToList();
      if (!filtered.Any())
      {
        return Ok(allCustomersList);
      }
      return Ok(filtered);
    }

    // --- PRODUCT MANAGEMENT ---
    [Authorize]
    [HttpGet("products")]
    public async Task<IActionResult> GetAllProducts()
    {
      return Ok(await _managerRepo.GetAllProductsAsync());
    }

    [Authorize(Roles = "SuperManager,Super Admin")]
    [HttpGet("products/{id}")]
    public async Task<IActionResult> GetProductById(int id)
    {
      var product = await _managerRepo.GetProductByIdAsync(id);
      return product == null ? NotFound() : Ok(product);
    }

    [Authorize(Roles = "SuperManager,Super Admin")]
    [HttpPost("products")]
    public async Task<IActionResult> CreateProduct([FromBody] Product product)
    {
      var newProduct = await _managerRepo.CreateProductAsync(product);
      return CreatedAtAction(nameof(GetProductById), new { id = newProduct.Id }, newProduct);
    }

    [Authorize(Roles = "SuperManager,Super Admin")]
    [HttpPut("products/{id}")]
    public async Task<IActionResult> UpdateProduct(int id, [FromBody] Product product)
    {
      await _managerRepo.UpdateProductAsync(id, product);
      return NoContent();
    }

    [Authorize(Roles = "SuperManager,Super Admin")]
    [HttpDelete("products/{id}")]
    public async Task<IActionResult> DeleteProduct(int id)
    {
      await _managerRepo.DeleteProductAsync(id);
      return NoContent();
    }

    [Authorize(Roles = "SuperManager,Super Admin")]
    [HttpGet("products/hierarchy")]
    public async Task<IActionResult> GetProductHierarchy()
    {
      var hierarchy = await _managerRepo.GetProductHierarchyAsync();
      return Ok(hierarchy);
    }

    [Authorize(Roles = "SuperManager,Super Admin")]
    [HttpPost("products/createupdate")]
    public async Task<IActionResult> CreateOrUpdateProduct([FromBody] ProductData product)
    {
      var productId = await _managerRepo.CreateOrUpdateProductAsync(product);
      if (product.Id == 0) 
      {
        return Ok(new { id = productId, message = "Product created successfully." });
      }
      else 
      {
        return Ok(new { id = productId, message = "Product updated successfully." }); 
      }
    }

    [Authorize(Roles = "SuperManager,Super Admin")]
    [HttpPost("products/simple")]
    public async Task<IActionResult> CreateProductSimple([FromBody] SimpleProductRequest product)
    {
      var newProductId = await _managerRepo.CreateProductSimpleAsync(product);
      return Ok(new { id = newProductId });
    }

    // --- TICKET MANAGEMENT ---
    [Authorize(Roles = "SuperManager,Manager,PM,Super Admin,HOD,Assignee")]
    [HttpGet("tickets")]
    public async Task<IActionResult> GetAllTickets(
       [FromQuery] string? status = null,
       [FromQuery] string? priority = null,
       [FromQuery] int? customerId = null,
       [FromQuery] string? assignedToId = null,
       [FromQuery] DateTime? dateFrom = null,
       [FromQuery] DateTime? dateTo = null,
       [FromQuery] int pageNumber = 1,
       [FromQuery] int pageSize = 20)
    {
      try
      {
        var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        int? userId = string.IsNullOrEmpty(userIdStr) ? null : int.Parse(userIdStr);

        var response = await _managerRepo.GetAllTicketsAsync(
            status, priority, customerId, assignedToId, dateFrom, dateTo, pageNumber, pageSize, userId, userRole);
        return Ok(response);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "An error occurred in the GetAllTickets endpoint.");
        return StatusCode(500, "An internal server error occurred.");
      }
    }

    [Authorize(Roles = "SuperManager,Manager,Super Admin,HOD,PM,Project Manager,Assignee,Customer")]
    [HttpGet("tickets/{ticketId}/timeline-analysis")]
    public async Task<IActionResult> GetTicketTimelineAnalysis(int ticketId)
    {
      try
      {
        var analysis = await _managerRepo.GetTicketTimelineAnalysisAsync(ticketId);
        if (analysis?.Summary == null) 
        {
          _logger.LogWarning("Timeline analysis not found for Ticket ID {TicketId}", ticketId);
          return Ok(new TicketTimelineAnalysis()); 
        }
        return Ok(analysis);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error fetching timeline analysis for Ticket ID {TicketId}", ticketId);
        return StatusCode(500, "An internal server error occurred.");
      }
    }

    // --- TICKET UPDATES ---
    [Authorize(Roles = "SuperManager,Manager,Super Admin,HOD")]
    [HttpPut("tickets/{id}/assignment")] 
    public async Task<IActionResult> UpdateAssignment(int id, [FromBody] UpdateAssignmentRequest request)
    {
      if (request.UpdatedByUserId <= 0 || string.IsNullOrWhiteSpace(request.UpdatedByUserRole))
      {
        return BadRequest("Updater User ID and Role are required.");
      }

      try
      {
        var ticket = await _ticketRepository.GetByIdAsync(id);
        if (ticket == null)
        {
          return NotFound($"Ticket with ID {id} not found.");
        }

        int? oldAssigneeId = null;
        if (!string.IsNullOrEmpty(ticket.AssignedTo) && int.TryParse(ticket.AssignedTo, out int parsedId))
        {
          oldAssigneeId = parsedId;
        }

        await _managerRepo.UpdateAssignmentAsync(
            id,
            request.AssignedToId,
            request.UpdatedByUserId,
            request.UpdatedByUserRole
        );

        try
        {
          await _notificationService.TriggerTicketNotificationAsync(id, "Updated", request.UpdatedByUserId, oldAssigneeId: oldAssigneeId);
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Failed to trigger assignment update notification for ticket {TicketId}", id);
        }

        if (request.AssignedToId.HasValue)
        {
          var assignedToId = request.AssignedToId.Value; 
          var updatedByUserRole = request.UpdatedByUserRole;
          var ticketNumber = ticket.TicketNumber;
          var ticketSubject = ticket.Subject;
          var customerId = ticket.CustomerId;

          var assignee = await _managerRepo.GetAssigneeByIdAsync(assignedToId);
          var customer = await _managerRepo.GetUserByIdAsync(customerId, "Customer");
          
          string? assigneeFullName = assignee?.FullName;
          string? assigneeEmail = assignee?.Email;
          string? customerFullName = customer?.FullName;
          string? customerEmail = customer?.Email;

          _ = Task.Run(async () =>
          {
            try
            {
              var threadId = EmailTemplates.GetThreadId(ticketNumber);
              if (assigneeEmail != null)
              {
                var emailContent = EmailTemplates.TicketAssigned(assigneeFullName!, ticketNumber, updatedByUserRole, ticketSubject);
                await _emailService.SendEmailAsync(assigneeEmail, emailContent.Subject, emailContent.Body, threadId);

                if (customerEmail != null)
                {
                  var custEmailContent = EmailTemplates.TicketAssignedToCustomer(customerFullName!, ticketNumber, assigneeFullName!, ticketSubject);
                  await _emailService.SendEmailAsync(customerEmail, custEmailContent.Subject, custEmailContent.Body, threadId);
                }
              }
              else
              {
                _logger.LogWarning("Assignee with ID {AssigneeId} not found for email notification.", assignedToId);
              }
            }
            catch (Exception ex)
            {
              _logger.LogError(ex, "Background email failed for UpdateAssignment ticket {TicketId}", id);
            }
          });
        }

        return NoContent();
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error updating assignment for ticket {TicketId}", id);
        return StatusCode(500, "Internal server error");
      }
    }

    [HttpPut("pms/{pmId}/tickets/{ticketId}")] 
    public async Task<IActionResult> UpdateTicketByPm(int ticketId, int pmId, [FromBody] PmTicketUpdateRequest request)
    {
      try 
      {
        var ticketBeforeUpdate = await _ticketRepository.GetByIdAsync(ticketId); 
        if (ticketBeforeUpdate == null)
        {
          return NotFound($"Ticket with ID {ticketId} not found.");
        }

        var oldStatus = ticketBeforeUpdate.Status;
        var oldPriority = ticketBeforeUpdate.Priority;

        await _managerRepo.UpdateTicketByPmAsync(ticketId, pmId, request);

        var newAssignedToId = request.AssignedToId;
        var newStatus = request.Status;
        
        int? oldAssigneeId = null;
        if (!string.IsNullOrEmpty(ticketBeforeUpdate.AssignedTo) && int.TryParse(ticketBeforeUpdate.AssignedTo, out int parsedId)) 
        {
            oldAssigneeId = parsedId;
        }

        try
        {
          await _notificationService.TriggerTicketNotificationAsync(ticketId, "Updated", pmId, oldStatus: oldStatus, oldPriority: oldPriority, oldAssigneeId: oldAssigneeId);
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Failed to trigger PM ticket update notification in ManagerController for ticket {TicketId}", ticketId);
        }

        string? newAssigneeFullName = null;
        string? newAssigneeEmail = null;
        if (newAssignedToId.HasValue && newAssignedToId != oldAssigneeId)
        {
           var newAssignee = await _managerRepo.GetAssigneeByIdAsync(newAssignedToId.Value);
           if (newAssignee != null)
           {
               newAssigneeFullName = newAssignee.FullName;
               newAssigneeEmail = newAssignee.Email;
           }
        }

        var customer = await _managerRepo.GetUserByIdAsync(ticketBeforeUpdate.CustomerId, "Customer");
        var pm = await _managerRepo.GetUserByIdAsync(pmId, "PM");

        var customerFullName = customer?.FullName;
        var customerEmail = customer?.Email;
        var pmFullName = pm?.FullName ?? "Project Manager";
        
        var ticketNumberStr = ticketBeforeUpdate.TicketNumber;
        var ticketSubjectStr = ticketBeforeUpdate.Subject;

        _ = Task.Run(async () =>
        {
          try
          {
            var threadId = EmailTemplates.GetThreadId(ticketNumberStr);

            if (newAssigneeEmail != null)
            {
              try
              {
                  var emailContent = EmailTemplates.TicketAssigned(newAssigneeFullName!, ticketNumberStr, pmFullName, ticketSubjectStr);
                  await _emailService.SendEmailAsync(newAssigneeEmail, emailContent.Subject, emailContent.Body, threadId);

                  if (customerEmail != null)
                  {
                    var custEmailContent = EmailTemplates.TicketAssignedToCustomer(customerFullName!, ticketNumberStr, newAssigneeFullName!, ticketSubjectStr);
                    await _emailService.SendEmailAsync(customerEmail, custEmailContent.Subject, custEmailContent.Body, threadId);
                  }
              }
              catch (Exception ex)
              {
                 _logger.LogError(ex, "Background email error (Assignment) for ticket {TicketId}", ticketId);
              }
            }

            if (!string.IsNullOrEmpty(newStatus) &&
                !newStatus.Equals(ticketBeforeUpdate.Status ?? "", StringComparison.OrdinalIgnoreCase))
            {
              try
              {
                var customStatuses = await _managerRepo.GetCustomStatusesAsync();
                var statusConfig = customStatuses.FirstOrDefault(s => s.StatusName.Equals(newStatus, StringComparison.OrdinalIgnoreCase));
                
                bool notifyCustomer = statusConfig?.NotifyCustomer ?? false;
                bool notifyPM = statusConfig?.NotifyPM ?? false;
                bool notifyAssignee = statusConfig?.NotifyAssignee ?? false;

                if (notifyCustomer && customerEmail != null)
                {
                  var emailContent = EmailTemplates.TicketStatusUpdate(customerFullName!, ticketNumberStr, newStatus, pmFullName, ticketSubjectStr, isCustomer: true);
                  await _emailService.SendEmailAsync(customerEmail, emailContent.Subject, emailContent.Body, threadId);
                }
                
                if (notifyPM && pm != null && pm.Email != null)
                {
                  var emailContent = EmailTemplates.TicketStatusUpdate(pmFullName, ticketNumberStr, newStatus, pmFullName, ticketSubjectStr, isCustomer: false);
                  await _emailService.SendEmailAsync(pm.Email, emailContent.Subject, emailContent.Body, threadId);
                }

                if (notifyAssignee)
                {
                  var assigneeEmail = newAssigneeEmail;
                  if (assigneeEmail == null && oldAssigneeId.HasValue)
                  {
                      var currentAssignee = await _managerRepo.GetAssigneeByIdAsync(oldAssigneeId.Value);
                      assigneeEmail = currentAssignee?.Email;
                  }
                  if (assigneeEmail != null)
                  {
                      var emailContent = EmailTemplates.TicketStatusUpdate("Assignee", ticketNumberStr, newStatus, pmFullName, ticketSubjectStr, isCustomer: false);
                      await _emailService.SendEmailAsync(assigneeEmail, emailContent.Subject, emailContent.Body, threadId);
                  }
                }
              }
              catch (Exception ex)
              {
                 _logger.LogError(ex, "Background email error (Status Update) for ticket {TicketId}", ticketId);
                 System.IO.File.WriteAllText(@"C:\Users\USER\Desktop\email_error_pm.txt", ex.ToString());
              }
            }
          }
           catch (Exception ex)
          {
             _logger.LogError(ex, "Background email task failed for ticket {TicketId}", ticketId);
          }
        });

        return NoContent();
      }
      catch (InvalidOperationException ex)
      {
          _logger.LogWarning(ex, "Business rule block: ticket {TicketId} PM {PmId}", ticketId, pmId);
          return BadRequest(new { error = ex.Message });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error updating ticket {TicketId} by PM {PmId}", ticketId, pmId);
        return StatusCode(500, "An internal server error occurred.");
      }
    }

    [Authorize(Roles = "SuperManager,Manager,Super Admin,HOD")]
    [HttpPut("tickets/{ticketId}/customer/{customerId}/update")] 
    public async Task<IActionResult> UpdateTicketByCustomer(int ticketId, int customerId, [FromBody] CustomerUpdateTicketRequest request)
    {
      try
      {
        var ticketBeforeUpdate = await _ticketRepository.GetByIdAsync(ticketId);
        if (ticketBeforeUpdate == null) return NotFound($"Ticket with ID {ticketId} not found.");

        if (ticketBeforeUpdate.CustomerId != customerId) return BadRequest("Customer ID mismatch.");

        await _managerRepo.UpdateTicketByCustomerAsync(ticketId, customerId, request);

        var newStatus = request.Status;
        var newPriority = request.Priority;
        var ticketNumber = ticketBeforeUpdate.TicketNumber;
        var ticketPriority = ticketBeforeUpdate.Priority;
        var ticketStatus = ticketBeforeUpdate.Status;

        try
        {
          await _notificationService.TriggerTicketNotificationAsync(ticketId, "Updated", customerId, oldStatus: ticketStatus, oldPriority: ticketPriority);
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Failed to trigger Customer ticket update notification for ticket {TicketId}", ticketId);
        }

        // Fetch PMs and customer synchronously to avoid disposed background DbContext issues
        var pmsList = (await _managerRepo.GetUsersByRoleAsync("PM"))
            .Select(pm => new { pm.FullName, pm.Email })
            .ToList();
        var customerObj = await _managerRepo.GetUserByIdAsync(customerId, "Customer");
        var customerFullNameStr = customerObj?.FullName ?? "the customer";

        _ = Task.Run(async () =>
        {
          try
          {
            if (!string.IsNullOrEmpty(newStatus) && newStatus != ticketStatus)
            {
               foreach (var pm in pmsList)
               {
                 try
                 {
                   var emailContent = EmailTemplates.TicketStatusUpdate(pm.FullName, ticketNumber, newStatus, customerFullNameStr, ticketBeforeUpdate.Subject);
                   await _emailService.SendEmailAsync(pm.Email, emailContent.Subject, emailContent.Body);
                 }
                 catch(Exception ex) { _logger.LogError(ex, "Failed to send Status update email to PM {PmEmail}", pm.Email); }
               }
            }
            if (!string.IsNullOrEmpty(newPriority) && newPriority != ticketPriority)
            {
               foreach (var pm in pmsList)
               {
                 try
                 {
                   var emailContent = EmailTemplates.TicketPriorityUpdate(pm.FullName, ticketNumber, ticketPriority, newPriority, customerFullNameStr, ticketBeforeUpdate.Subject);
                   await _emailService.SendEmailAsync(pm.Email, emailContent.Subject, emailContent.Body);
                 }
                 catch(Exception ex) { _logger.LogError(ex, "Failed to send Priority update email to PM {PmEmail}", pm.Email); }
               }
            }
          }
          catch (Exception ex)
          {
            _logger.LogError(ex, "Background email task failed for Customer update on ticket {TicketId}", ticketId);
          }
        });

        return NoContent();
      }
      catch (InvalidOperationException ex)
      {
        _logger.LogWarning(ex, "Business rule validation failed for customer update on ticket {TicketId}", ticketId);
        return BadRequest(new { error = ex.Message });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error updating ticket {TicketId} by customer {CustomerId}", ticketId, customerId);
        return StatusCode(500, "An internal server error occurred.");
      }
    }

    [Authorize(Roles = "Assignee,SuperManager,Manager,Super Admin,HOD")]
    [HttpPut("developer/{developerId}/tickets/{id}/submit-for-review")]
    public async Task<IActionResult> SubmitForReview(int id, int developerId)
    {
      try
      {
        var ticket = await _ticketRepository.GetByIdAsync(id);
        if (ticket == null) return NotFound($"Ticket with ID {id} not found.");

        var oldStatus = ticket.Status;

        await _managerRepo.SubmitForReviewAsync(id, developerId, "Developer");

        try
        {
          await _notificationService.TriggerTicketNotificationAsync(id, "Updated", developerId, oldStatus: oldStatus);
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Failed to trigger submit for review notification for ticket {TicketId}", id);
        }

        var ticketNumber = ticket.TicketNumber;
        var ticketSubject = ticket.Subject;

        _ = Task.Run(async () =>
        {
          try
          {
            var pms = await _managerRepo.GetUsersByRoleAsync("PM");
            var developer = await _managerRepo.GetAssigneeByIdAsync(developerId); 
            var developerName = developer?.FullName ?? $"Developer ID {developerId}";
            foreach (var pm in pms)
            {
              try
              {
                 var emailContent = EmailTemplates.TicketReviewRequest(pm.FullName, developerName, ticketNumber, ticketSubject);
                 await _emailService.SendEmailAsync(pm.Email, emailContent.Subject, emailContent.Body);
              }
              catch (Exception ex) { _logger.LogError(ex, "Failed to send Review email to PM {PmEmail}", pm.Email); }
            }
          }
          catch (Exception ex)
          {
             _logger.LogError(ex, "Background email task failed for Review submission ticket {TicketId}", id);
          }
        });

        return NoContent();
      }
      catch (Exception ex)
      {
_logger.LogError(ex, "Error submitting ticket {TicketId} for review by developer {DeveloperId}.", id, developerId);
        return StatusCode(500, "An internal server error occurred.");
      }
    }

    [Authorize(Roles = "SuperManager,Manager,Super Admin,HOD,Developer,Assignee")]
    [HttpPut("tickets/{id}/status")] 
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateStatusRequest request)
    {
      try
      {
        if (request == null || string.IsNullOrWhiteSpace(request.Status))
        {
            return BadRequest(new { error = "Status is required." });
        }

        var ticketBeforeUpdate = await _ticketRepository.GetByIdAsync(id);

        var actorUserIdClaim = User.FindFirst("uid")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        int? actorUserId = null;
        if (int.TryParse(actorUserIdClaim, out var parsedId))
        {
            actorUserId = parsedId;
        }
        var actorUserRole = User.FindFirst(ClaimTypes.Role)?.Value ?? "Developer";

        await _managerRepo.UpdateStatusAsync(id, request.Status, actorUserId, actorUserRole, request.Note);

        if (actorUserId.HasValue)
        {
          try
          {
            await _notificationService.TriggerTicketNotificationAsync(id, "Updated", actorUserId.Value, oldStatus: ticketBeforeUpdate?.Status);
          }
          catch (Exception ex)
          {
            _logger.LogError(ex, "Failed to trigger status update notification for ticket {TicketId}", id);
          }
        }

        if (ticketBeforeUpdate != null && !string.Equals(request.Status, ticketBeforeUpdate.Status ?? "", StringComparison.OrdinalIgnoreCase))
        {
             var customer = await _managerRepo.GetUserByIdAsync(ticketBeforeUpdate.CustomerId, "Customer");
             var customerFullName = customer?.FullName;
             var customerEmail = customer?.Email;
             var ticketNumberStr = ticketBeforeUpdate.TicketNumber;
             var ticketSubjectStr = ticketBeforeUpdate.Subject;
             var newStatus = request.Status;
             var oldStatus = ticketBeforeUpdate.Status ?? "";

             // Fetch configs synchronously to avoid background thread DI scope/connection errors
             (string emailSubject, string emailBody)? customerEmailContent = null;
             (string emailSubject, string emailBody)? pmEmailContent = null;
             string? pmEmail = null;
             (string emailSubject, string emailBody)? assigneeEmailContent = null;
             string? assigneeEmail = null;

             try
             {
                 var customStatuses = await _managerRepo.GetCustomStatusesAsync();
                 var statusConfig = customStatuses.FirstOrDefault(s => s.StatusName.Equals(newStatus, StringComparison.OrdinalIgnoreCase));
                 bool notifyCustomer = statusConfig?.NotifyCustomer ?? false;
                 bool notifyPM = statusConfig?.NotifyPM ?? false;
                 bool notifyAssignee = statusConfig?.NotifyAssignee ?? false;

                 // 1. Prepare Customer Email Content
                 if (customerEmail != null)
                 {
                     var workflows = await _rolesRepository.GetStatusWorkflowsAsync();
                     var customerRule = workflows.FirstOrDefault(w => 
                         w.RoleName.Equals("Customer", StringComparison.OrdinalIgnoreCase) && 
                         w.IsActive &&
                         (w.CurrentStatus == "*" || w.CurrentStatus.Equals(newStatus, StringComparison.OrdinalIgnoreCase))
                     );

                     if (customerRule != null)
                     {
                         if (customerRule.SendEmail)
                         {
                             customerEmailContent = EmailTemplates.TicketStatusUpdateWithButtons(
                                 customerFullName!, 
                                 ticketNumberStr, 
                                 newStatus, 
                                 "The Support Team", 
                                 ticketSubjectStr, 
                                 id, 
                                 customerRule.NextStatuses
                             );
                         }
                     }
                     else if (notifyCustomer)
                     {
                         customerEmailContent = EmailTemplates.TicketStatusUpdate(customerFullName!, ticketNumberStr, newStatus, "The Support Team", ticketSubjectStr, isCustomer: true);
                     }
                 }

                 // 2. Prepare PM Email Content
                 if (notifyPM)
                 {
                     var pms = await _managerRepo.GetUsersByRoleAsync("PM");
                     var assignedPM = pms.FirstOrDefault();
                     if (assignedPM != null && assignedPM.Email != null)
                     {
                         pmEmail = assignedPM.Email;
                         pmEmailContent = EmailTemplates.TicketStatusUpdate(assignedPM.FullName, ticketNumberStr, newStatus, "The Support Team", ticketSubjectStr, isCustomer: false);
                     }
                 }

                 // 3. Prepare Assignee Email Content
                 if (notifyAssignee)
                 {
                     var oldAssigneeIdStr = ticketBeforeUpdate.AssignedTo;
                     if (!string.IsNullOrEmpty(oldAssigneeIdStr) && int.TryParse(oldAssigneeIdStr, out int assigneeId))
                     {
                         var assignee = await _managerRepo.GetAssigneeByIdAsync(assigneeId);
                         if (assignee != null && assignee.Email != null)
                         {
                             assigneeEmail = assignee.Email;
                             assigneeEmailContent = EmailTemplates.TicketStatusUpdate(assignee.FullName, ticketNumberStr, newStatus, "The Support Team", ticketSubjectStr, isCustomer: false);
                         }
                     }
                 }
             }
             catch (Exception ex)
             {
                 _logger.LogError(ex, "Error preparing status update emails for ticket {TicketId}", id);
             }

             // Dispatch emails in background (safe because _emailService is a Singleton and has no DbConnection dependency)
             _ = Task.Run(async () =>
             {
                 try
                 {
                     var threadId = EmailTemplates.GetThreadId(ticketNumberStr);

                     if (customerEmailContent != null && customerEmail != null)
                     {
                         try
                         {
                             await _emailService.SendEmailAsync(customerEmail, customerEmailContent.Value.emailSubject, customerEmailContent.Value.emailBody, threadId);
                         }
                         catch (Exception ex)
                         {
                             _logger.LogError(ex, "Failed to send status update email to customer for ticket {TicketId}", id);
                         }
                     }

                     if (pmEmailContent != null && pmEmail != null)
                     {
                         try
                         {
                             await _emailService.SendEmailAsync(pmEmail, pmEmailContent.Value.emailSubject, pmEmailContent.Value.emailBody, threadId);
                         }
                         catch (Exception ex)
                         {
                             _logger.LogError(ex, "Failed to send status update email to PM for ticket {TicketId}", id);
                         }
                     }

                     if (assigneeEmailContent != null && assigneeEmail != null)
                     {
                         try
                         {
                             await _emailService.SendEmailAsync(assigneeEmail, assigneeEmailContent.Value.emailSubject, assigneeEmailContent.Value.emailBody, threadId);
                         }
                         catch (Exception ex)
                         {
                             _logger.LogError(ex, "Failed to send status update email to assignee for ticket {TicketId}", id);
                         }
                     }
                 }
                 catch (Exception ex)
                 {
                     _logger.LogError(ex, "Error in background email task for ticket {TicketId}", id);
                 }
             });
        }

        return NoContent();
      }
      catch (InvalidOperationException ex)
      {
        _logger.LogWarning(ex, "Business rule validation failed for status update on ticket {TicketId}", id);
        return BadRequest(new { error = ex.Message });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error updating status for ticket {TicketId}", id);
        return StatusCode(500, "Internal server error");
      }
    }

    [Authorize(Roles = "SuperManager,Manager,Super Admin,HOD,PM,Project Manager")]
    [HttpPut("tickets/{ticketId}/deadline")]
    public async Task<IActionResult> SetTicketDeadline(int ticketId, [FromBody] SetDeadlineRequest request)
    {
      if (request.ManagerId <= 0) return BadRequest("A valid ManagerId is required.");
      try
      {
        await _managerRepo.SetTicketDeadlineAsync(ticketId, request);
        return NoContent();
      }
      catch (KeyNotFoundException ex) 
      {
        return NotFound(new { message = ex.Message });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error setting deadline for Ticket ID {TicketId}", ticketId);
        return StatusCode(500, "An internal server error occurred.");
      }
    }

    [Authorize(Roles = "SuperManager,Manager,Super Admin,HOD,Assignee,Customer,PM,Project Manager")]
    [HttpGet("tickets/{ticketId}/deadline")]
    public async Task<IActionResult> GetTicketDeadline(int ticketId)
    {
      try
      {
        var deadline = await _managerRepo.GetTicketDeadlineAsync(ticketId);
        if (deadline == null)
        {
          return Ok(null); // Return 200 OK with null instead of 404 to avoid console errors
        }
        return Ok(deadline);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error getting deadline for Ticket ID {TicketId}", ticketId);
        return StatusCode(500, "An internal server error occurred.");
      }
    }

    // Batch endpoint: fetch deadlines for multiple tickets in ONE call
    [HttpPost("tickets/deadlines/batch")]
    public async Task<IActionResult> GetTicketDeadlinesBatch([FromBody] List<int> ticketIds)
    {
      try
      {
        if (ticketIds == null || ticketIds.Count == 0)
          return Ok(new List<object>());

        var deadlines = await _managerRepo.GetTicketDeadlinesBatchAsync(ticketIds);
        return Ok(deadlines);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error getting batch deadlines for {Count} tickets", ticketIds?.Count);
        return StatusCode(500, "An internal server error occurred.");
      }
    }

    [Authorize(Roles = "SuperManager,Manager,Super Admin,HOD,PM,Project Manager")]
    [HttpGet("tickets/delayed")]
    public async Task<IActionResult> GetDelayedTickets(
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
    {
      try
      {
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        int? pmId = null;
        if (role == "Manager" || role == "PM" || role == "Project Manager")
        {
            var userIdClaim = User.FindFirst("UserId")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out int parsedId))
            {
                pmId = parsedId;
            }
        }
        var response = await _managerRepo.GetDelayedTicketsAsync(pageNumber, pageSize, pmId);
        return Ok(response);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error fetching delayed tickets.");
        return StatusCode(500, "An internal server error occurred.");
      }
    }

    [Authorize(Roles = "Assignee,SuperManager,Manager,Super Admin,HOD")]
    [HttpGet("developer/{developerId}/tickets/delayed")]
    public async Task<IActionResult> GetDelayedTicketsForDeveloper(
        int developerId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
    {
      try
      {
        var response = await _managerRepo.GetDelayedTicketsForDeveloperAsync(developerId, pageNumber, pageSize);
        return Ok(response);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error fetching delayed tickets for developer {DeveloperId}", developerId);
        return StatusCode(500, "An internal server error occurred.");
      }
    }

    // --- USER MANAGEMENT ---
    [Authorize(Roles = "SuperManager,Super Admin,Manager,PM,Project Manager,HOD")]
    [HttpPost("users")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
      using (var connection = new SqlConnection(_connectionString))
      {
          var systemRoles = await connection.QueryAsync<string>("SELECT RoleName FROM dbo.SystemRoles");
          var validRoles = new HashSet<string>(systemRoles, StringComparer.OrdinalIgnoreCase)
              { "Customer", "Super Admin", "PM", "Assignee" };
          if (!validRoles.Contains(request.Role)) return BadRequest($"Invalid user role: {request.Role}");
      }

      // Normalize legacy role names to match SystemRoles
      if (request.Role.Equals("Super Admin", StringComparison.OrdinalIgnoreCase)) request.Role = "SuperManager";
      if (request.Role.Equals("PM", StringComparison.OrdinalIgnoreCase)) request.Role = "Manager";
      if (request.Role.Equals("Assignee", StringComparison.OrdinalIgnoreCase)) request.Role = "Developer";

      if (string.IsNullOrWhiteSpace(request.Password))
      {
        request.Password = "Default@123";
      }

      try 
      {
        var newUser = await _managerRepo.CreateUserAsync(request);
        try 
        {
          var emailContent = newUser.Role == "Customer"
            ? EmailTemplates.CustomerAccountCreated(newUser.FullName, newUser.ContactPerson)
            : EmailTemplates.UserCreation(newUser.FullName, newUser.UserNumber, request.Password, newUser.Role);
          await _emailService.SendEmailAsync(newUser.Email, emailContent.Subject, emailContent.Body);
        }
        catch (Exception emailEx)
        {
          _logger.LogWarning(emailEx, "User created successfully, but failed to send welcome email.");
          // Optionally, add a flag to the response indicating email failure
        }

        if (newUser != null && newUser.Role == "Customer")
        {
          int? newManagerId = null;
          int? primaryAssigneeId = request.DefaultAssigneeId ?? request.AssigneeIds?.FirstOrDefault();
          if (primaryAssigneeId.HasValue)
          {
            var assignee = await _managerRepo.GetAssigneeByIdAsync(primaryAssigneeId.Value);
            newManagerId = assignee?.ManagerId;
          }

          if (newManagerId.HasValue)
          {
            var actorUserIdClaim = User.FindFirst("uid")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(actorUserIdClaim, out var actorUserId))
            {
              try
              {
                await _notificationService.TriggerCustomerAssignmentNotificationAsync(newUser.Id, null, newManagerId.Value, actorUserId);
              }
              catch (Exception ex)
              {
                _logger.LogError(ex, "Failed to trigger Customer PM assignment notification on user creation for customer {CustomerId}", newUser.Id);
              }
            }
          }
        }

        return Ok(newUser);
      }
      catch (SqlException ex) when (ex.Number == 50000) 
      {
        _logger.LogWarning("Conflict creating user: {ErrorMessage}", ex.Message);
        return Conflict(new { message = ex.Message });
      }
      catch (Exception ex)
      {
        System.IO.File.WriteAllText("create_user_error.txt", ex.ToString());
        _logger.LogError(ex, "Error creating user.");
        return StatusCode(500, "An internal server error occurred.");
      }
    }

    [Authorize(Roles = "SuperManager,Super Admin,Manager,PM,Project Manager,HOD")]
    [HttpPost("users/bulk-upload/{role}")]
    public async Task<IActionResult> BulkUploadUsers(string role, IFormFile file)
    {
      if (file == null || file.Length == 0)
        return BadRequest("Please upload a valid Excel file.");

      if (role != "Customer" && role != "PM" && role != "Assignee")
        return BadRequest("Invalid role specified for bulk upload.");

      try
      {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        using var stream = new MemoryStream();
        await file.CopyToAsync(stream);
        using var package = new ExcelPackage(stream);
        var worksheet = package.Workbook.Worksheets.FirstOrDefault();
        if (worksheet == null) return BadRequest("The Excel file is empty.");

        var rowCount = worksheet.Dimension.Rows;
        int successCount = 0;
        int failCount = 0;
        var errors = new List<string>();

        // Cache for lookups
        var allAssignees = role == "Customer" ? (await _managerRepo.GetUsersByRoleAsync("Assignee")).ToList() : null;
        var allPMs = role == "Assignee" ? (await _managerRepo.GetUsersByRoleAsync("PM")).ToList() : null;

        for (int row = 2; row <= rowCount; row++)
        {
          try
          {
            var fullName = worksheet.Cells[row, 1].Text;
            var email = worksheet.Cells[row, 2].Text;
            var password = worksheet.Cells[row, 3].Text;
            var mobileNo = worksheet.Cells[row, 4].Text;
            var contactPerson = worksheet.Cells[row, 5].Text; // For customer
            var phoneNo = worksheet.Cells[row, 6].Text; // For customer
            var mappedEmail = worksheet.Cells[row, 7].Text; // AssigneeEmail for Customer, PMEmail for Assignee

            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email))
              continue;

            if (role == "Customer" || role == "PM")
            {
              int? mappedId = null;
              if (role == "Customer" && !string.IsNullOrWhiteSpace(mappedEmail) && allAssignees != null)
              {
                var match = allAssignees.FirstOrDefault(a => a.Email.Equals(mappedEmail, StringComparison.OrdinalIgnoreCase));
                if (match != null) mappedId = match.Id;
              }

              var req = new CreateUserRequest
              {
                Role = role,
                FullName = fullName,
                Email = email,
                Password = string.IsNullOrWhiteSpace(password) ? "Default@123" : password,
                MobileNo = mobileNo,
                ContactPerson = contactPerson,
                PhoneNo = phoneNo,
                DefaultAssigneeId = role == "Customer" ? mappedId : null
              };
              await _managerRepo.CreateUserAsync(req);
              successCount++;
            }
            else if (role == "Assignee")
            {
              int? mappedId = null;
              if (!string.IsNullOrWhiteSpace(mappedEmail) && allPMs != null)
              {
                var match = allPMs.FirstOrDefault(p => p.Email.Equals(mappedEmail, StringComparison.OrdinalIgnoreCase));
                if (match != null) mappedId = match.Id;
              }

              var req = new CreateAssigneeRequest
              {
                FullName = fullName,
                Email = email,
                Password = string.IsNullOrWhiteSpace(password) ? "Default@123" : password,
                MobileNo = mobileNo,
                ManagerId = mappedId
              };
              await _managerRepo.CreateAssigneeAsync(req);
              successCount++;
            }
          }
          catch (Exception ex)
          {
            failCount++;
            errors.Add($"Row {row}: {ex.Message}");
          }
        }

        return Ok(new { message = $"Bulk upload completed. Success: {successCount}, Failed: {failCount}.", errors });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error processing bulk upload file.");
        return StatusCode(500, "An internal server error occurred while processing the file.");
      }
    }

    [Authorize(Roles = "SuperManager,Super Admin,Manager,PM,Project Manager,TL,HOD")]
    [HttpGet("users/role/{role}")]
    public async Task<IActionResult> GetUsersByRole(string role)
    {
      var users = await _managerRepo.GetUsersByRoleAsync(role);
      return Ok(users);
    }

    [Authorize(Roles = "SuperManager,Super Admin,Manager,PM,Project Manager,HOD,Customer")]
    [HttpGet("users/{id}")]
    public async Task<IActionResult> GetUserById(int id, [FromQuery] string? role = null)
    {
      var callerRole = User.FindFirst(ClaimTypes.Role)?.Value
                    ?? User.FindFirst("role")?.Value;
      var callerIdClaim = User.FindFirst("uid")?.Value 
                       ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

      if (callerRole == "Customer")
      {
        if (int.TryParse(callerIdClaim, out var callerId) && callerId != id)
        {
          return StatusCode(403, "You can only view your own user profile.");
        }
      }

      // Determine the lookup role: use the explicit query param if provided,
      // otherwise derive it from the caller's JWT role claim so that PMs
      // and other staff see their own profile from the staff table (CL_Master_User),
      // not the customer table.
      string? lookupRole = role;
      if (lookupRole == null)
      {
        // Only force "Customer" when the caller explicitly needs a customer lookup.
        // For PM / staff self-profile calls leave it null so the SP uses the staff branch.
        lookupRole = (callerRole == "Customer") ? "Customer" : null;
      }
      var user = await _managerRepo.GetUserByIdAsync(id, lookupRole);
      return user == null ? NotFound() : Ok(user);
    }

    [Authorize(Roles = "SuperManager,Super Admin,Manager,PM,Project Manager,HOD")]
    [HttpPut("users/{id}")]
    public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserRequest request)
    {
      try
      {
        var userBefore = await _managerRepo.GetUserByIdAsync(id, "Customer");
        await _managerRepo.UpdateUserAsync(id, request);

        if (userBefore != null && userBefore.Role == "Customer")
        {
          int? newManagerId = null;
          int? primaryAssigneeId = request.DefaultAssigneeId ?? request.AssigneeIds?.FirstOrDefault();
          if (primaryAssigneeId.HasValue)
          {
            var assignee = await _managerRepo.GetAssigneeByIdAsync(primaryAssigneeId.Value);
            newManagerId = assignee?.ManagerId;
          }

          if (userBefore.ManagerId != newManagerId)
          {
            var actorUserIdClaim = User.FindFirst("uid")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(actorUserIdClaim, out var actorUserId))
            {
              try
              {
                await _notificationService.TriggerCustomerAssignmentNotificationAsync(id, userBefore.ManagerId, newManagerId, actorUserId);
              }
              catch (Exception ex)
              {
                _logger.LogError(ex, "Failed to trigger Customer PM assignment notification for customer {CustomerId}", id);
              }
            }
          }
        }

        return NoContent();
      }
      catch (SqlException ex) when (ex.Number == 50000) 
      {
        _logger.LogWarning("Conflict updating user {UserId}: {ErrorMessage}", id, ex.Message);
        return Conflict(new { message = ex.Message });
      }
      catch (Exception ex)
      {
        System.IO.File.WriteAllText("update_user_error.txt", ex.ToString());
        _logger.LogError(ex, "Error updating user {UserId}", id);
        return StatusCode(500, "An internal server error occurred.");
      }
    }

    [Authorize(Roles = "SuperManager,Super Admin")]
    [HttpDelete("users/{id}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
      try
      {
        var user = await _managerRepo.GetUserByIdAsync(id, null);
        if (user == null)
        {
          return NotFound(new { message = $"User with ID {id} not found." });
        }
        await _managerRepo.DeleteUserAsync(id);
        return NoContent();
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error deleting user {UserId}", id);
        return StatusCode(500, new { message = "An internal server error occurred while deleting the user." });
      }
    }

    [Authorize(Roles = "SuperManager,Super Admin")]
    [HttpPut("users/{id}/toggle-active")]
    public async Task<IActionResult> ToggleUserActive(int id, [FromQuery] bool isActive)
    {
      try
      {
        _logger.LogInformation("ToggleUserActive called for UserId: {UserId}, isActive: {IsActive}", id, isActive);
        using (var connection = new SqlConnection(_connectionString))
        {
          var rows = await connection.ExecuteAsync(
            "UPDATE dbo.Users SET IsActive = @IsActive WHERE Id = @Id",
            new { IsActive = isActive ? 1 : 0, Id = id });
          _logger.LogInformation("ToggleUserActive query executed. Rows affected: {Rows}", rows);
        }
        return NoContent();
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error toggling user active status {UserId}", id);
        return StatusCode(500, new { message = "An internal server error occurred while updating active status." });
      }
    }

    [HttpPut("pms/{id}")] 
    public async Task<IActionResult> UpdatePm(int id, [FromBody] UpdatePmRequest request)
    {
      try
      {
        await _managerRepo.UpdatePmAsync(id, request);
        return NoContent();
      }
      catch (SqlException ex) when (ex.Number == 50000) 
      {
        _logger.LogWarning("Conflict updating PM {PmId}: {ErrorMessage}", id, ex.Message);
        return Conflict(new { message = ex.Message });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error updating PM {PmId}", id);
        return StatusCode(500, "An internal server error occurred.");
      }
    }

    [Authorize(Roles = "SuperManager,Super Admin,Manager,PM,Project Manager,HOD")]
    [HttpGet("users/{userId}/products")]
    public async Task<IActionResult> GetUserProducts(int userId)
    {
      var productIds = await _managerRepo.GetUserProductsAsync(userId);
      return Ok(productIds);
    }

    [Authorize(Roles = "SuperManager,Super Admin,Manager,PM,Project Manager,HOD")]
    [HttpGet("users/{userId}/assignees")]
    public async Task<IActionResult> GetUserAssignees(int userId)
    {
      var assigneeIds = await _managerRepo.GetUserAssigneesAsync(userId);
      return Ok(assigneeIds);
    }

    [Authorize(Roles = "SuperManager,Super Admin,PM")]
    [HttpGet("customers/find")]
    public async Task<IActionResult> FindCustomerByNumber([FromQuery] string customerNumber)
    {
      if (string.IsNullOrWhiteSpace(customerNumber)) return BadRequest("Customer number is required.");
      var customer = await _managerRepo.FindCustomerByNumberAsync(customerNumber);
      if (customer == null) return NotFound(new { message = "Customer with that number was not found." });
      return Ok(new { customerId = customer.Id, customerName = customer.FullName });
    }

    // --- ASSIGNEE MANAGEMENT ---
    [Authorize(Roles = "SuperManager,Super Admin,Manager,PM,Project Manager,HOD")]
    [HttpPost("assignees")]
    public async Task<IActionResult> CreateAssignee([FromBody] CreateAssigneeRequest request)
    {
      try
      {
        var newAssignee = await _managerRepo.CreateAssigneeAsync(request);
        try
        {
          var emailContent = EmailTemplates.UserCreation(newAssignee.FullName, newAssignee.AssigneeNumber, request.Password, "Assignee");
          await _emailService.SendEmailAsync(newAssignee.Email, emailContent.Subject, emailContent.Body);
        }
        catch (Exception emailEx)
        {
          _logger.LogWarning(emailEx, "Assignee created successfully, but failed to send welcome email.");
        }
        return Ok(newAssignee);
      }
      catch (SqlException ex) when (ex.Number == 50000) 
      {
        _logger.LogWarning("Conflict creating assignee: {ErrorMessage}", ex.Message);
        return Conflict(new { message = ex.Message });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error creating assignee.");
        return StatusCode(500, "An internal server error occurred.");
      }
    }

    [Authorize]
    [HttpGet("assignees")]
    public async Task<IActionResult> GetAllAssignees([FromQuery] bool includeInactive = false)
    {
      var role = User.FindFirst(ClaimTypes.Role)?.Value;
      int? pmId = null;
      var userIdClaim = User.FindFirst("UserId")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("uid")?.Value;
      bool hasAssignAll = false;

      if (int.TryParse(userIdClaim, out int parsedUserId))
      {
          using (var connection = new SqlConnection(_connectionString))
          {
              string normalizedRole = role ?? "";
              if (normalizedRole == "PM") normalizedRole = "Manager";
              else if (normalizedRole == "Assignee") normalizedRole = "Developer";
              else if (normalizedRole == "Super Admin") normalizedRole = "SuperManager";

              var rolePerms = await connection.QueryAsync<string>(
                  @"SELECT p.PermissionKey 
                    FROM dbo.SystemPermissions p
                    JOIN dbo.RolePermissions rp ON p.Id = rp.PermissionId
                    JOIN dbo.SystemRoles r ON rp.RoleId = r.Id
                    WHERE r.RoleName = @RoleName",
                  new { RoleName = normalizedRole });
              
              var permsList = rolePerms.ToList();

              var prefRole = role switch { "Manager" => "PM", "TL" => "PM", "Project Manager" => "PM", "Developer" => "Assignee", _ => role };
              var overrides = await connection.QueryAsync<(string PermissionKey, bool IsEnabled)>(
                  "SELECT PermissionKey, IsEnabled FROM dbo.UserPermissions WHERE UserId = @UserId AND UserRole = @UserRole",
                  new { UserId = parsedUserId, UserRole = prefRole });
              
              foreach (var ov in overrides)
              {
                  if (ov.IsEnabled)
                  {
                      if (!permsList.Contains(ov.PermissionKey)) permsList.Add(ov.PermissionKey);
                  }
                  else
                  {
                      permsList.Remove(ov.PermissionKey);
                  }
              }

              hasAssignAll = permsList.Contains("assign_ticket_all") || permsList.Contains("view_master_users");
          }
      }

      if ((role == "Manager" || role == "PM" || role == "Project Manager") && !hasAssignAll)
      {
          if (int.TryParse(userIdClaim, out int parsedId))
          {
              pmId = parsedId;
          }
      }
      var assignees = await _managerRepo.GetAllAssigneesAsync(pmId, includeInactive);
      return Ok(assignees);
    }

    [Authorize(Roles = "SuperManager,Super Admin,Manager,PM,Project Manager,HOD,Assignee")]
    [HttpGet("assignees/{id}")]
    public async Task<IActionResult> GetAssigneeById(int id)
    {
      var assignee = await _managerRepo.GetAssigneeByIdAsync(id);
      return assignee == null ? NotFound() : Ok(assignee);
    }

    [Authorize(Roles = "SuperManager,Super Admin,Manager,PM,Project Manager,HOD")]
    [HttpPut("assignees/{id}")]
    public async Task<IActionResult> UpdateAssignee(int id, [FromBody] UpdateAssigneeRequest request)
    {
      try 
      {
        await _managerRepo.UpdateAssigneeAsync(id, request);
        return NoContent();
      }
      catch (SqlException ex) when (ex.Number == 50000) 
      {
        _logger.LogWarning("Conflict updating assignee {AssigneeId}: {ErrorMessage}", id, ex.Message);
        return Conflict(new { message = ex.Message });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error updating assignee {AssigneeId}", id);
        return StatusCode(500, "An internal server error occurred.");
      }
    }

    // --- DASHBOARD STATS ---
    [Authorize(Roles = "Assignee,SuperManager,Manager,Super Admin,HOD")]
    [HttpGet("dashboard-stats/developer/{developerId}")]
    public async Task<IActionResult> GetDeveloperDashboardStats(int developerId)
    {
      var stats = await _managerRepo.GetDeveloperDashboardStatsAsync(developerId);
      return Ok(stats);
    }

    [Authorize(Roles = "SuperManager,Super Admin,PM")]
    [HttpGet("dashboard-stats/manager")] 
    public async Task<IActionResult> GetManagerDashboardStats() 
    {
      var role = User.FindFirst(ClaimTypes.Role)?.Value;
      int? pmId = null;
      if (role == "Manager" || role == "PM" || role == "Project Manager")
      {
          var userIdClaim = User.FindFirst("UserId")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
          if (int.TryParse(userIdClaim, out int parsedId))
          {
              pmId = parsedId;
          }
      }
      var stats = await _managerRepo.GetDashboardStatsAsync(pmId);
      return Ok(stats);
    }

    [HttpGet("dashboard-stats/tat/{pmId}")]
    [Authorize(Roles = "Manager,Super Admin,SuperManager,PM,Project Manager,Assignee")]
    public async Task<IActionResult> GetTatDashboardStats(int pmId,
        [FromQuery] string? relationship = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] string? status = null)
    {
      try
      {
          var stats = await _managerRepo.GetTatDashboardStatsAsync(pmId, relationship, fromDate, toDate, status);
          return Ok(stats);
      }
      catch (Exception ex)
      {
          _logger.LogError(ex, "Error fetching TAT dashboard stats for pmId={PmId}", pmId);
          return StatusCode(500, "An internal server error occurred.");
      }
    }

    // --- REWORK & PERFORMANCE ---
    [Authorize(Roles = "SuperManager,Manager,Super Admin,HOD,PM,Project Manager,Assignee,Customer")]
    [HttpGet("tickets/{ticketId}/rework-stats")]
    public async Task<IActionResult> GetTicketReworkStats(int ticketId)
    {
      try
      {
        var reworkStats = await _managerRepo.GetTicketReworkCountByUserAsync(ticketId);
        return Ok(reworkStats);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error fetching rework stats for Ticket ID {TicketId}", ticketId);
        return StatusCode(500, "An internal server error occurred.");
      }
    }

    [Authorize(Roles = "SuperManager,Super Admin")]
    [HttpGet("assignees/{assigneeId}/rework-stats")]
    public async Task<IActionResult> GetAssigneeReworkStats(int assigneeId)
    {
      try
      {
        var totalReworks = await _managerRepo.GetTotalReworkCountForAssigneeAsync(assigneeId);
        return Ok(totalReworks);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error fetching total rework stats for Assignee ID {AssigneeId}", assigneeId);
        return StatusCode(500, "An internal server error occurred.");
      }
    }

    [Authorize(Roles = "SuperManager,Manager,PM,Project Manager,Super Admin,HOD")]
    [HttpGet("performance/assignees/all")]
    public async Task<IActionResult> GetAssigneePerformanceStats(
    [FromQuery] int? month = null,
    [FromQuery] int? year = null)
    {
      try
      {
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        int? pmId = null;
        if (role == "Manager" || role == "PM" || role == "Project Manager")
        {
            var userIdClaim = User.FindFirst("UserId")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out int parsedId))
            {
                pmId = parsedId;
            }
        }
        var stats = await _managerRepo.GetAssigneePerformanceStatsAsync(month, year, pmId);
        return Ok(stats);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error fetching assignee performance stats for month={Month}, year={Year}", month, year);
        return StatusCode(500, "An internal server error occurred.");
      }
    }

    [Authorize]
    [HttpGet("communications/{communicationId}/attachments/{attachmentId}")]
    public async Task<IActionResult> GetCommunicationAttachment(int communicationId, int attachmentId)
    {
      try
      {
        var attachment = await _managerRepo.GetCommunicationAttachmentByIdAsync(communicationId, attachmentId);
        if (attachment == null)
        {
          _logger.LogWarning("Communication attachment not found. CommID: {CommId}, AttachID: {AttachId}", communicationId, attachmentId);
          return NotFound("Attachment not found.");
        }

        var physicalPath = Path.Combine(_webHostEnvironment.WebRootPath, attachment.FileUrl.TrimStart('/'));
        _logger.LogInformation("Attempting to serve communication attachment from physical path: {Path}", physicalPath);

        if (!System.IO.File.Exists(physicalPath))
        {
          _logger.LogError("File not found at physical path: {Path}", physicalPath);
          if (System.IO.File.Exists(attachment.FileUrl))
          {
            physicalPath = attachment.FileUrl;
            _logger.LogWarning("File found using FileUrl directly as physical path: {Path}", physicalPath);
          }
          else
          {
            return NotFound("File not found on server.");
          }
        }

        var fileBytes = await System.IO.File.ReadAllBytesAsync(physicalPath);
        var contentType = GetContentType(attachment.FileName);
        return File(fileBytes, contentType, attachment.FileName);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error retrieving communication attachment {AttachmentId} for communication {CommunicationId}", attachmentId, communicationId);
        return StatusCode(500, "An internal server error occurred while retrieving the attachment.");
      }
    }

    private string GetContentType(string fileName)
    {
      var ext = Path.GetExtension(fileName).ToLowerInvariant();
      switch (ext)
      {
        case ".txt": return "text/plain";
        case ".pdf": return "application/pdf";
        case ".doc": return "application/msword";
        case ".docx": return "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
        case ".xls": return "application/vnd.ms-excel";
        case ".xlsx": return "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        case ".png": return "image/png";
        case ".jpg": case ".jpeg": return "image/jpeg";
        default: return "application/octet-stream";
      }
    }

    [Authorize(Roles = "SuperManager,Manager,Super Admin,HOD,PM,Project Manager,Assignee,Customer")]
    [HttpGet("tickets/{ticketId}/communications")]
    public async Task<IActionResult> GetCommunications(int ticketId)
    {
      try
      {
        var response = await _managerRepo.GetPmCommunicationsAsync(ticketId);
        if (response == null)
        {
          _logger.LogWarning("No communications found or error fetching for ticket {TicketId}", ticketId);
          return Ok(new PmCommunicationResponse { CustomerChannel = new List<TicketCommunication>(), DeveloperChannel = new List<TicketCommunication>() });
        }
        return Ok(response);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error getting communications for ticket {TicketId}", ticketId);
        return StatusCode(500, "An internal server error occurred while getting communications.");
      }
    }

    [Authorize(Roles = "SuperManager,Manager,Super Admin,HOD,PM,Project Manager,Assignee,Customer")]
    [HttpPost("tickets/{ticketId}/communications")]
    public async Task<IActionResult> AddCommunication(int ticketId, [FromForm] NewCommentRequest newComment)
    {
      if (newComment == null) return BadRequest("Comment data is required.");
      if (newComment.PostedByUserId <= 0) return BadRequest("PostedByUserId is required.");
      if (string.IsNullOrWhiteSpace(newComment.PostedByName)) return BadRequest("PostedByName is required.");
      if (string.IsNullOrWhiteSpace(newComment.PostedByUserRole)) return BadRequest("PostedByUserRole is required.");
      if (string.IsNullOrWhiteSpace(newComment.Channel) || 
          (newComment.Channel != "Customer" && 
           newComment.Channel != "Developer" && 
           newComment.Channel != "Developer(Note)" && 
           newComment.Channel != "Customer(Note)" && 
           newComment.Channel != "GlobalNotes" && 
           newComment.Channel != "GlobalNote")) 
          return BadRequest("Channel must be 'Customer', 'Developer', or 'GlobalNotes'.");

      if (string.IsNullOrWhiteSpace(newComment.CommentText) && (newComment.Files == null || newComment.Files.Count == 0)) return BadRequest("Cannot post an empty comment without attachments.");

      try
      {
        var communication = await _managerRepo.AddCommunicationAsync(
            ticketId,
            newComment,
            newComment.Files ?? new List<IFormFile>() 
        );
        if (communication == null)
        {
          _logger.LogWarning("AddCommunicationAsync returned null for ticket {TicketId}. Ticket might not exist.", ticketId);
          return NotFound($"Could not add communication, potentially ticket {ticketId} does not exist.");
        }

        try
        {
          await _notificationService.TriggerTicketNotificationAsync(ticketId, "CommentAdded", newComment.PostedByUserId);
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Failed to trigger comment added notification for ticket {TicketId}", ticketId);
        }

        return Ok(communication); 
      }
      catch (SqlException ex) when (ex.Number == 547) 
      {
        _logger.LogWarning(ex, "Foreign key violation while adding communication for ticket {TicketId}. Ticket likely does not exist.", ticketId);
        return NotFound($"Could not add communication: Ticket ID {ticketId} not found.");
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error adding communication for ticket {TicketId}", ticketId);
        return StatusCode(500, "An internal server error occurred while adding communication.");
      }
    }

    [Authorize(Roles = "SuperManager,Manager,Super Admin,HOD,PM,Project Manager,Assignee,Customer")]
    [HttpGet("tickets/{id}/relations")]
    public async Task<IActionResult> GetTicketRelations(int id)
    {
      try
      {
        var result = await _managerRepo.GetTicketRelationsAsync(id);
        return Ok(result);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error fetching relations for ticket {TicketId}", id);
        return StatusCode(500, "Error fetching ticket relations.");
      }
    }

    [Authorize(Roles = "SuperManager,Manager,Super Admin,HOD,PM,Project Manager,Assignee,Customer")]
    [HttpPut("tickets/{id}/parent")]
    public async Task<IActionResult> SetTicketParent(int id, [FromBody] SetParentRequest request)
    {
      try
      {
        if (request.ParentTicketId.HasValue && request.ParentTicketId.Value == id)
          return BadRequest("A ticket cannot be its own parent.");

        await _managerRepo.SetTicketParentAsync(id, request.ParentTicketId);
        return Ok(new { message = request.ParentTicketId.HasValue
            ? $"Ticket {id} is now a child of ticket {request.ParentTicketId}."
            : $"Ticket {id} parent link removed." });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error setting parent for ticket {TicketId}", id);
        return StatusCode(500, "Error setting ticket parent.");
      }
    }
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // CUSTOM TICKET STATUSES (Phase 7)
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    [Authorize]
    [HttpGet("statuses")]
    public async Task<IActionResult> GetCustomStatuses()
    {
      try
      {
        var statuses = await _managerRepo.GetCustomStatusesAsync();
        return Ok(statuses);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error fetching custom statuses");
        return StatusCode(500, "Internal server error");
      }
    }

    [Authorize(Roles = "SuperManager,Manager,Super Admin,PM,Project Manager")]
    [HttpPost("statuses")]
    public async Task<IActionResult> CreateCustomStatus([FromBody] CustomStatusRequest request)
    {
      try
      {
        if (string.IsNullOrWhiteSpace(request.StatusName))
          return BadRequest("Status name is required.");
          
        var newStatus = await _managerRepo.CreateCustomStatusAsync(request);
        return Ok(newStatus);
      }
      catch (InvalidOperationException ex)
      {
        return BadRequest(ex.Message);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error creating custom status");
        return StatusCode(500, "Internal server error");
      }
    }

    [Authorize(Roles = "SuperManager,Manager,Super Admin,PM,Project Manager")]
    [HttpDelete("statuses/{id}")]
    public async Task<IActionResult> DeleteCustomStatus(int id)
    {
      try
      {
        await _managerRepo.DeleteCustomStatusAsync(id);
        return Ok(new { message = "Status deleted successfully." });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error deleting custom status");
        return StatusCode(500, "Internal server error");
      }
    }
    [Authorize(Roles = "SuperManager,Manager,Super Admin,PM,Project Manager")]
    [HttpPut("statuses/{id}")]
    public async Task<IActionResult> UpdateCustomStatus(int id, [FromBody] CustomStatusRequest request)
    {
      try
      {
        await _managerRepo.UpdateCustomStatusAsync(id, request);
        return Ok(new { message = "Status updated successfully." });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error updating custom status");
        return StatusCode(500, "Internal server error");
      }
    }
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // CUSTOM DEVELOPER STATUSES
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    [Authorize]
    [HttpGet("developer-statuses")]
    public async Task<IActionResult> GetCustomDeveloperStatuses()
    {
      try
      {
        var statuses = await _managerRepo.GetCustomDeveloperStatusesAsync();
        return Ok(statuses);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error fetching custom developer statuses");
        return StatusCode(500, "Internal server error");
      }
    }

    [Authorize(Roles = "SuperManager,Manager,Super Admin,PM,Project Manager")]
    [HttpPost("developer-statuses")]
    public async Task<IActionResult> CreateCustomDeveloperStatus([FromBody] CustomStatusRequest request)
    {
      try
      {
        if (string.IsNullOrWhiteSpace(request.StatusName))
          return BadRequest("Status name is required.");
          
        var newStatus = await _managerRepo.CreateCustomDeveloperStatusAsync(request.StatusName);
        return Ok(newStatus);
      }
      catch (InvalidOperationException ex)
      {
        return BadRequest(ex.Message);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error creating custom developer status");
        return StatusCode(500, "Internal server error");
      }
    }

    [Authorize(Roles = "SuperManager,Manager,Super Admin,PM,Project Manager")]
    [HttpDelete("developer-statuses/{id}")]
    public async Task<IActionResult> DeleteCustomDeveloperStatus(int id)
    {
      try
      {
        await _managerRepo.DeleteCustomDeveloperStatusAsync(id);
        return Ok(new { message = "Developer status deleted successfully." });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error deleting custom developer status");
        return StatusCode(500, "Internal server error");
      }
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // PRIORITIES (Phase 7)
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    [Authorize]
    [HttpGet("priorities")]
    public async Task<IActionResult> GetPriorities()
    {
      try
      {
        var priorities = await _managerRepo.GetPrioritiesAsync();
        return Ok(priorities);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error fetching priorities");
        return StatusCode(500, "Internal server error");
      }
    }

    [Authorize(Roles = "SuperManager,Super Admin")]
    [HttpPost("priorities")]
    public async Task<IActionResult> CreatePriority([FromBody] PriorityRequest request)
    {
      try
      {
        if (string.IsNullOrWhiteSpace(request.PriorityName) || request.TATHours <= 0)
          return BadRequest("Priority name and valid TAT hours are required.");
          
        var newPriority = await _managerRepo.CreatePriorityAsync(request.PriorityName, request.TATHours);
        return Ok(newPriority);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error creating priority");
        return StatusCode(500, "Internal server error");
      }
    }

    [Authorize(Roles = "SuperManager,Super Admin")]
    [HttpDelete("priorities/{id}")]
    public async Task<IActionResult> DeletePriority(int id)
    {
      try
      {
        await _managerRepo.DeletePriorityAsync(id);
        return Ok(new { message = "Priority deleted successfully." });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error deleting priority");
        return StatusCode(500, "Internal server error");
      }
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // ISSUE CATEGORIES (Phase 7)
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    [Authorize]
    [HttpGet("categories")]
    public async Task<IActionResult> GetIssueCategories()
    {
      try
      {
        var categories = await _managerRepo.GetIssueCategoriesAsync();
        return Ok(categories);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error fetching issue categories");
        return StatusCode(500, "Internal server error");
      }
    }

    [Authorize(Roles = "SuperManager,Super Admin,Manager,PM,Customer")]
    [HttpPost("categories")]
    public async Task<IActionResult> CreateIssueCategory([FromBody] CategoryRequest request)
    {
      try
      {
        if (string.IsNullOrWhiteSpace(request.CategoryName))
          return BadRequest("Category name is required.");
          
        var newCat = await _managerRepo.CreateIssueCategoryAsync(request.CategoryName);
        return Ok(newCat);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error creating issue category");
        return StatusCode(500, "Internal server error");
      }
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // TICKET SOURCES
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    [Authorize(Roles = "SuperManager,Super Admin,Manager,PM,Customer")]
    [HttpGet("ticket-sources")]
    public async Task<IActionResult> GetTicketSources()
    {
      try
      {
        var sources = await _managerRepo.GetTicketSourcesAsync();
        return Ok(sources);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error fetching ticket sources");
        return StatusCode(500, "Internal server error");
      }
    }

    public class TicketSourceRequest
    {
        public string SourceName { get; set; }
    }

    [Authorize(Roles = "SuperManager,Super Admin,Manager,PM,Customer")]
    [HttpPost("ticket-sources")]
    public async Task<IActionResult> CreateTicketSource([FromBody] TicketSourceRequest request)
    {
      try
      {
        if (string.IsNullOrWhiteSpace(request.SourceName))
          return BadRequest("Source name is required.");
          
        var newId = await _managerRepo.AddTicketSourceAsync(request.SourceName);
        return Ok(new { Id = newId, SourceName = request.SourceName });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error creating ticket source");
        return StatusCode(500, "Internal server error");
      }
    }

    [Authorize(Roles = "SuperManager,Super Admin,Manager,PM")]
    [HttpDelete("ticket-sources/{id}")]
    public async Task<IActionResult> DeleteTicketSource(int id)
    {
      try
      {
        await _managerRepo.DeleteTicketSourceAsync(id);
        return Ok(new { message = "Ticket source deleted successfully." });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error deleting ticket source");
        return StatusCode(500, "Internal server error");
      }
    }

    [Authorize(Roles = "SuperManager,Super Admin,Manager,PM")]
    [HttpDelete("categories/{id}")]
    public async Task<IActionResult> DeleteIssueCategory(int id)
    {
      try
      {
        await _managerRepo.DeleteIssueCategoryAsync(id);
        return Ok(new { message = "Category deleted successfully." });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error deleting issue category");
        return StatusCode(500, "Internal server error");
      }
    }
  }

  public class PriorityRequest
  {
    public string PriorityName { get; set; }
    public int TATHours { get; set; }
  }

  public class CategoryRequest
  {
    public string CategoryName { get; set; }
  }
}