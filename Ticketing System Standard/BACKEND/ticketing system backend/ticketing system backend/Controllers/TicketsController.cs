using System; // Trigger watch rebuild
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ticketing_system_backend.API.Interfaces;
using ticketing_system_backend.API.Repositories;
using ticketing_system_backend.Interface;
using ticketing_system_backend.Models;
using ticketing_system_backend.Helpers;

namespace ticketing_system_backend.Controllers
{
  [ApiController]
  [Route("api/[controller]")]
  [Authorize]   // All ticket endpoints require authentication
  public class TicketsController : ControllerBase
  {
    private readonly ITicketRepository _ticketRepository;
    private readonly IManager _managerRepo; 
    private readonly IEmailService _emailService; 
    private readonly ILogger<TicketsController> _logger;
    private readonly INotificationService _notificationService;
    private readonly IRolesRepository _rolesRepository;

    public TicketsController(
        ITicketRepository ticketRepository,
        IManager managerRepo,
        IEmailService emailService,
        ILogger<TicketsController> logger,
        INotificationService notificationService,
        IRolesRepository rolesRepository)
    {
      _ticketRepository = ticketRepository;
      _managerRepo = managerRepo;
      _emailService = emailService;
      _logger = logger;
      _notificationService = notificationService;
      _rolesRepository = rolesRepository;
    }

    [HttpGet("public-action")]
    [AllowAnonymous]
    public async Task<IActionResult> PublicAction([FromQuery] string payload)
    {
      try
      {
        if (string.IsNullOrEmpty(payload))
        {
          return BadRequest(new { error = "Missing secure payload." });
        }

        var decrypted = EncryptionHelper.Decrypt(payload);
        var parts = decrypted.Split('|');
        if (parts.Length != 2)
        {
          return BadRequest(new { error = "Invalid secure link structure." });
        }

        if (!int.TryParse(parts[0], out var ticketId))
        {
          return BadRequest(new { error = "Invalid ticket format." });
        }

        var targetStatus = parts[1];
        var ticket = await _ticketRepository.GetByIdAsync(ticketId);
        if (ticket == null)
        {
          return NotFound(new { error = $"Ticket with ID {ticketId} was not found." });
        }

        var oldStatus = ticket.Status;
        await _managerRepo.UpdateStatusAsync(ticketId, targetStatus, null, "Customer", "Updated via email action button.");

        try
        {
          await _notificationService.TriggerTicketNotificationAsync(ticketId, "Updated", 0, oldStatus: oldStatus);
        }
        catch { /* ignore notification errors */ }

        return Ok(new { success = true, oldStatus = oldStatus, newStatus = targetStatus });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to execute secure public status update action.");
        return BadRequest(new { error = "The link is expired, invalid, or has been tampered with." });
      }
    }

    [HttpPost]
    public async Task<ActionResult<Ticket>> CreateTicket([FromForm] CreateTicketRequest request)
    {
      try
      {
        if (!ModelState.IsValid)
        {
          return BadRequest(ModelState);
        }

        var actorUidClaim = User.FindFirst("uid")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var actorRoleClaim2 = User.FindFirst("role")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

        if (int.TryParse(actorUidClaim, out var actorId))
        {
          if (actorRoleClaim2 == "Assignee")
          {
            var allowedCustomers = await _managerRepo.GetCustomerIdsByAssigneeIdAsync(actorId);
            if (!allowedCustomers.Contains(request.CustomerId))
              return StatusCode(403, "You can only create tickets for your assigned customers.");
          }
          else if (actorRoleClaim2 == "PM" || actorRoleClaim2 == "Project Manager" || actorRoleClaim2 == "Manager")
          {
            var allowedCustomers = await _managerRepo.GetCustomerIdsByPmIdAsync(actorId);
            if (!allowedCustomers.Contains(request.CustomerId))
              return StatusCode(403, "You can only create tickets for customers assigned to you or your team.");
          }

          // Stamp the real creator so ticket history shows the correct user (not always Customer)
          request.CreatedByUserId   = actorId;
          request.CreatedByUserRole = actorRoleClaim2 ?? "Customer";
          var nameClaim = User.FindFirst("name")?.Value
                          ?? User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
                          ?? User.FindFirst("unique_name")?.Value;
          request.CreatedByUserName = nameClaim ?? actorRoleClaim2 ?? "Customer";
        }

        var createdTicket = await _ticketRepository.CreateAsync(request);

        if (createdTicket != null)
        {
          var newTicketId = createdTicket.Id;
          var newTicketNumber = createdTicket.TicketNumber;
          var newTicketSubject = createdTicket.Subject;
          var newCustomerId = createdTicket.CustomerId;

          var newCustomer = await _managerRepo.GetUserByIdAsync(newCustomerId, "Customer");
          var newPms = await _managerRepo.GetUsersByRoleAsync("PM");
          var newCustomerFullName = newCustomer?.FullName;
          var newCustomerEmail = newCustomer?.Email;
          var newPmList = newPms.Select(pm => new { pm.FullName, pm.Email }).ToList();

          _ = Task.Run(async () =>
          {
            try
            {
              var threadId = EmailTemplates.GetThreadId(newTicketNumber);
              if (newCustomerEmail != null)
              {
                try
                {
                  var emailContent = EmailTemplates.NewTicketConfirmation(
                      newCustomerFullName!, newTicketNumber, newTicketSubject,
                      request.Description ?? "", request.Priority ?? "Medium");
                  await _emailService.SendEmailAsync(newCustomerEmail, emailContent.Subject, emailContent.Body, threadId, isThreadRoot: true);
                }
                catch (Exception emailEx) { _logger.LogError(emailEx, "Failed to send email to customer for ticket {TicketId}", newTicketId); }
              }
              foreach (var pm in newPmList)
              {
                try
                {
                  var emailContent = EmailTemplates.NewTicket(pm.FullName, newCustomerFullName ?? "N/A", newTicketNumber, newTicketSubject);
                  await _emailService.SendEmailAsync(pm.Email, emailContent.Subject, emailContent.Body, threadId);
                }
                catch (Exception emailEx) { _logger.LogError(emailEx, "Failed to send email to PM for ticket {TicketId}", newTicketId); }
              }
            }
            catch (Exception bgEx) { _logger.LogError(bgEx, "Error in bg email task for ticket {TicketId}", newTicketId); }
          });

          var notifUidClaim = User.FindFirst("uid")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
          if (int.TryParse(notifUidClaim, out var notifActorId))
          {
            try { await _notificationService.TriggerTicketNotificationAsync(newTicketId, "Created", notifActorId); }
            catch (Exception ex) { _logger.LogError(ex, "Failed to trigger notification for ticket {TicketId}", newTicketId); }
          }
        }

        return CreatedAtAction(nameof(GetTicket), new { id = createdTicket.Id }, createdTicket);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error creating ticket");
        if (ex.Message.Contains("Invalid Docket Number") || ex.Message.Contains("MKFoodsConnection"))
          return BadRequest(ex.Message);
        return StatusCode(500, "Internal server error occurred while creating ticket");
      }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateTicket(int id, [FromBody] UpdateTicketRequest request)
    {
      try
      {
        var ticketBeforeUpdate = await _ticketRepository.GetByIdAsync(id);
        if (ticketBeforeUpdate == null)
        {
          return NotFound($"Ticket with ID {id} not found.");
        }

        var oldStatus = ticketBeforeUpdate.Status;
        var oldPriority = ticketBeforeUpdate.Priority;
        var newStatus = request.Status;
        var newAssignedToId = request.AssignedToId;
        var actorRoleClaim = User.FindFirst("role")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

        int? oldAssigneeId = null;
        if (!string.IsNullOrEmpty(ticketBeforeUpdate.AssignedTo))
        {
          if (int.TryParse(ticketBeforeUpdate.AssignedTo, out int parsedId))
          {
            oldAssigneeId = parsedId;
          }
        }

        // ── IsBlocked check: if any role blocks this role when ticket is in current status ──
        var allWorkflowsForBlock = (await _rolesRepository.GetStatusWorkflowsAsync()).ToList();
        var blockedRule = allWorkflowsForBlock.FirstOrDefault(w =>
            w.RoleName.Equals(actorRoleClaim, StringComparison.OrdinalIgnoreCase) &&
            (w.CurrentStatus == "*" || w.CurrentStatus.Equals(oldStatus ?? "", StringComparison.OrdinalIgnoreCase)) &&
            w.IsBlocked);

        if (blockedRule != null)
        {
            return StatusCode(403, $"Ticket is locked. Role '{actorRoleClaim}' cannot update a ticket with status '{oldStatus}'.");
        }

        if (!string.IsNullOrEmpty(newStatus) && !newStatus.Equals(oldStatus ?? "", StringComparison.OrdinalIgnoreCase))
        {
            var allowedWorkflows = (await _rolesRepository.GetStatusWorkflowsAsync())
                                    .Where(w => w.RoleName.Equals(actorRoleClaim, StringComparison.OrdinalIgnoreCase))
                                    .ToList();

            if (allowedWorkflows.Any())
            {
                var match = allowedWorkflows.FirstOrDefault(w => 
                    (w.CurrentStatus == "*" || w.CurrentStatus.Equals(oldStatus ?? "", StringComparison.OrdinalIgnoreCase)) &&
                    w.NextStatuses != null && w.NextStatuses.Any(ns => ns.Equals(newStatus, StringComparison.OrdinalIgnoreCase))
                );

                if (match == null)
                {
                    return StatusCode(403, $"Role '{actorRoleClaim}' is not authorized to transition ticket from '{oldStatus}' to '{newStatus}'.");
                }

                if (!string.IsNullOrEmpty(match.Condition))
                {
                    // Basic placeholder for condition logic
                    if (match.Condition.Equals("RequiresAllChildrenClosed", StringComparison.OrdinalIgnoreCase))
                    {
                        // E.g. Check children tickets here
                    }
                }
            }
        }

        int? updateActorId = null;
        var actIdClaim = User.FindFirst("uid")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (int.TryParse(actIdClaim, out var parsedActId))
        {
            updateActorId = parsedActId;
        }

        await _ticketRepository.UpdateAsync(id, request, updateActorId);

        var ticketNumber = ticketBeforeUpdate.TicketNumber;
        var ticketSubject = ticketBeforeUpdate.Subject;
        var customerId = ticketBeforeUpdate.CustomerId;

        // Fetch configs synchronously to avoid background thread DI scope/connection errors
        string? newAssigneeFullName = null;
        string? newAssigneeEmail = null;
        string? customerFullName = null;
        string? customerEmail = null;
        
        var customer = await _managerRepo.GetUserByIdAsync(customerId, "Customer");
        if (customer != null)
        {
          customerFullName = customer.FullName;
          customerEmail = customer.Email;
        }

        if (newAssignedToId.HasValue && newAssignedToId != oldAssigneeId)
        {
          var newAssignee = await _managerRepo.GetAssigneeByIdAsync(newAssignedToId.Value);
          if (newAssignee != null)
          {
            newAssigneeFullName = newAssignee.FullName;
            newAssigneeEmail = newAssignee.Email;
          }
        }

        (string emailSubject, string emailBody)? customerEmailContent = null;
        if (!string.IsNullOrEmpty(newStatus) && !newStatus.Equals(oldStatus ?? "", StringComparison.OrdinalIgnoreCase) && customerEmail != null)
        {
          try
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
                    ticketNumber, 
                    newStatus, 
                    "the Support Team", 
                    ticketSubject, 
                    id, 
                    customerRule.NextStatuses
                );
              }
            }
            else
            {
              var customStatuses = await _managerRepo.GetCustomStatusesAsync();
              var statusConfig = customStatuses.FirstOrDefault(s => s.StatusName.Equals(newStatus, StringComparison.OrdinalIgnoreCase));
              bool notifyCustomer = statusConfig?.NotifyCustomer ?? false;

              if (notifyCustomer)
              {
                customerEmailContent = EmailTemplates.TicketStatusUpdate(customerFullName!, ticketNumber, newStatus, "the Support Team", ticketSubject, isCustomer: true);
              }
            }
          }
          catch (Exception ex)
          {
            _logger.LogError(ex, "Error preparing status update email for customer for ticket {TicketId}", id);
          }
        }

        _ = Task.Run(async () =>
        {
          try
          {
            var threadId = EmailTemplates.GetThreadId(ticketNumber);

            if (newAssigneeEmail != null)
            {
              try
              {
                var emailContent = EmailTemplates.TicketAssigned(newAssigneeFullName!, ticketNumber, "the System", ticketSubject);
                await _emailService.SendEmailAsync(newAssigneeEmail, emailContent.Subject, emailContent.Body, threadId);

                if (customerEmail != null)
                {
                  var custEmailContent = EmailTemplates.TicketAssignedToCustomer(customerFullName!, ticketNumber, newAssigneeFullName!, ticketSubject);
                  await _emailService.SendEmailAsync(customerEmail, custEmailContent.Subject, custEmailContent.Body, threadId);
                }
              }
              catch (Exception emailEx)
              {
                _logger.LogError(emailEx, "Failed to send assignment email for ticket {TicketId}", id);
              }
            }
            
            if (customerEmailContent != null && customerEmail != null)
            {
              try
              {
                await _emailService.SendEmailAsync(customerEmail, customerEmailContent.Value.emailSubject, customerEmailContent.Value.emailBody, threadId);
                _logger.LogInformation("Status update email successfully sent to customer for ticket {TicketId}", id);
              }
              catch (Exception emailEx)
              {
                _logger.LogError(emailEx, "Failed to send status update email to customer for ticket {TicketId}", id);
              }
            }

            _logger.LogInformation("Background email notifications processed for ticket {TicketId}", id);
          }
          catch (Exception ex)
          {
            _logger.LogError(ex, "Error in background email task for ticket update {TicketId}", id);
          }
        });

        // Trigger real-time notifications
        var actorUserIdClaim = User.FindFirst("uid")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (int.TryParse(actorUserIdClaim, out var actorUserId))
        {
          try
          {
            await _notificationService.TriggerTicketNotificationAsync(id, "Updated", actorUserId, oldStatus: oldStatus, oldPriority: oldPriority, oldAssigneeId: oldAssigneeId);
          }
          catch (Exception ex)
          {
            _logger.LogError(ex, "Failed to trigger ticket update notification for ticket {TicketId}", id);
          }
        }

        return NoContent(); 
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error updating ticket {TicketId}", id);
        return StatusCode(500, "An internal server error occurred.");
      }
    }

    [HttpPut("{pmId}/tickets/{ticketId}")]
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
        var newStatus = request.Status;
        var newAssignedToId = request.AssignedToId;
        var actorRoleClaim = User.FindFirst("role")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

        int? oldAssigneeId = null;
        if (!string.IsNullOrEmpty(ticketBeforeUpdate.AssignedTo))
        {
          if (int.TryParse(ticketBeforeUpdate.AssignedTo, out int parsedId))
          {
            oldAssigneeId = parsedId;
          }
        }

        if (newAssignedToId.HasValue && newAssignedToId != oldAssigneeId)
        {
            var targetUser = await _managerRepo.GetAssigneeByIdAsync(newAssignedToId.Value);
            var targetRole = targetUser?.Role ?? "Developer";

            var allowedAssignments = (await _rolesRepository.GetAssignmentWorkflowsAsync())
                                    .Where(w => w.AssignerRole.Equals(actorRoleClaim, StringComparison.OrdinalIgnoreCase))
                                    .ToList();

            if (allowedAssignments.Any())
            {
                var match = allowedAssignments.FirstOrDefault(w => w.AssignableToRole.Equals(targetRole, StringComparison.OrdinalIgnoreCase) || w.AssignableToRole == "*");
                if (match == null)
                {
                    return StatusCode(403, $"Role '{actorRoleClaim}' is not authorized to assign tickets to role '{targetRole}'.");
                }
            }
        }

        // â”€â”€ IsBlocked check â”€â”€
        var allWorkflowsForBlock2 = (await _rolesRepository.GetStatusWorkflowsAsync()).ToList();
        var blockedRule2 = allWorkflowsForBlock2.FirstOrDefault(w =>
            w.RoleName.Equals(actorRoleClaim, StringComparison.OrdinalIgnoreCase) &&
            (w.CurrentStatus == "*" || w.CurrentStatus.Equals(oldStatus ?? "", StringComparison.OrdinalIgnoreCase)) &&
            w.IsBlocked);
        if (blockedRule2 != null)
        {
            return StatusCode(403, $"Ticket is locked. Role '{actorRoleClaim}' cannot update a ticket with status '{oldStatus}'.");
        }

        if (!string.IsNullOrEmpty(newStatus) && !newStatus.Equals(oldStatus ?? "", StringComparison.OrdinalIgnoreCase))
        {
            var allowedWorkflows = (await _rolesRepository.GetStatusWorkflowsAsync())
                                    .Where(w => w.RoleName.Equals(actorRoleClaim, StringComparison.OrdinalIgnoreCase))
                                    .ToList();

            if (allowedWorkflows.Any())
            {
                var match = allowedWorkflows.FirstOrDefault(w => 
                    (w.CurrentStatus == "*" || w.CurrentStatus.Equals(oldStatus ?? "", StringComparison.OrdinalIgnoreCase)) &&
                    w.NextStatuses != null && w.NextStatuses.Any(ns => ns.Equals(newStatus, StringComparison.OrdinalIgnoreCase))
                );

                if (match == null)
                {
                    return StatusCode(403, $"Role '{actorRoleClaim}' is not authorized to transition ticket from '{oldStatus}' to '{newStatus}'.");
                }

                if (!string.IsNullOrEmpty(match.Condition))
                {
                    if (match.Condition.Equals("RequiresAllChildrenClosed", StringComparison.OrdinalIgnoreCase))
                    {
                        // E.g. Check children tickets here
                    }
                }
            }
        }

        await _managerRepo.UpdateTicketByPmAsync(ticketId, pmId, request);

        var ticketNumber = ticketBeforeUpdate.TicketNumber;
        var ticketSubject = ticketBeforeUpdate.Subject;
        var customerId = ticketBeforeUpdate.CustomerId;
        var oldPriority = ticketBeforeUpdate.Priority;

        var customer = await _managerRepo.GetUserByIdAsync(customerId, "Customer");
        var pm = await _managerRepo.GetUserByIdAsync(pmId, "PM");

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

        var customerFullName = customer?.FullName;
        var customerEmail = customer?.Email;
        var pmName = pm?.FullName ?? "Project Manager";

        // Check status workflow configurations synchronously before launching Task.Run
        (string emailSubject, string emailBody)? customerEmailContent = null;
        if (!string.IsNullOrEmpty(newStatus) && !newStatus.Equals(oldStatus ?? "", StringComparison.OrdinalIgnoreCase) && customerEmail != null)
        {
          try
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
                    ticketNumber, 
                    newStatus, 
                    pmName, 
                    ticketSubject, 
                    ticketId, 
                    customerRule.NextStatuses
                );
              }
            }
            else
            {
              var customStatuses = await _managerRepo.GetCustomStatusesAsync();
              var statusConfig = customStatuses.FirstOrDefault(s => s.StatusName.Equals(newStatus, StringComparison.OrdinalIgnoreCase));
              bool notifyCustomer = statusConfig?.NotifyCustomer ?? false;

              if (notifyCustomer)
              {
                customerEmailContent = EmailTemplates.TicketStatusUpdate(customerFullName!, ticketNumber, newStatus, pmName, ticketSubject, isCustomer: true);
              }
            }
          }
          catch (Exception ex)
          {
            _logger.LogError(ex, "Error preparing status update email for customer for ticket {TicketId}", ticketId);
          }
        }

        _ = Task.Run(async () =>
        {
          try
          {
            var threadId = EmailTemplates.GetThreadId(ticketNumber);

            if (newAssigneeEmail != null)
            {
              try
              {
                  var emailContent = EmailTemplates.TicketAssigned(newAssigneeFullName!, ticketNumber, pmName, ticketSubject);
                  await _emailService.SendEmailAsync(newAssigneeEmail, emailContent.Subject, emailContent.Body, threadId);

                  if (customerEmail != null)
                  {
                    var custEmailContent = EmailTemplates.TicketAssignedToCustomer(customerFullName!, ticketNumber, newAssigneeFullName!, ticketSubject);
                    await _emailService.SendEmailAsync(customerEmail, custEmailContent.Subject, custEmailContent.Body, threadId);
                  }
              }
              catch (Exception emailEx)
              {
                _logger.LogError(emailEx, "Failed to send PM assignment email for ticket {TicketId}", ticketId);
              }
            }
            
            if (customerEmailContent != null && customerEmail != null)
            {
              try
              {
                await _emailService.SendEmailAsync(customerEmail, customerEmailContent.Value.emailSubject, customerEmailContent.Value.emailBody, threadId);
                _logger.LogInformation("Status update email successfully sent to customer for ticket {TicketId}", ticketId);
              }
              catch (Exception emailEx)
              {
                _logger.LogError(emailEx, "Failed to send status update email to customer for ticket {TicketId}", ticketId);
              }
            }

            _logger.LogInformation("Background email notifications processed for PM ticket update {TicketId}", ticketId);
          }
          catch (Exception ex)
          {
            _logger.LogError(ex, "Error in background email task for PM ticket update {TicketId}", ticketId);
          }
        });

        // Trigger real-time notifications
        try
        {
          await _notificationService.TriggerTicketNotificationAsync(ticketId, "Updated", pmId, oldStatus: oldStatus, oldPriority: oldPriority, oldAssigneeId: oldAssigneeId);
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Failed to trigger PM ticket update notification for ticket {TicketId}", ticketId);
        }

        return NoContent(); 
      }
      catch (InvalidOperationException ex)
      {
        _logger.LogWarning(ex, "Business rule violation failed for ticket {TicketId} by PM {PmId}", ticketId, pmId);
        return BadRequest(new { error = ex.Message });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error updating ticket {TicketId} by PM {PmId}", ticketId, pmId);
        return StatusCode(500, "An internal server error occurred.");
      }
    }

    [HttpGet("customer/{customerId}/executive")]
    public async Task<IActionResult> GetCustomerExecutive(int customerId)
    {
      try
      {
        var executive = await _managerRepo.GetExecutiveByCustomerIdAsync(customerId);
        if (executive == null)
        {
          return NotFound("Mapped executive not found for this customer.");
        }
        return Ok(executive);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error retrieving mapped executive for customer {CustomerId}", customerId);
        return StatusCode(500, "Internal server error occurred while retrieving mapped executive.");
      }
    }

    [HttpGet("customer/{customerId}")]
    public async Task<ActionResult<TicketResponse>> GetCustomerTickets(
         int customerId,
         [FromQuery] string? status = null,
         [FromQuery] string? priority = null,
         [FromQuery] string? product = null,
         [FromQuery] string? subProduct = null,
         [FromQuery] string? assignedTo = null,
         [FromQuery] DateTime? dateFrom = null,
         [FromQuery] DateTime? dateTo = null)
    {
      try
      {
        var statusList = !string.IsNullOrEmpty(status) ? status.Split(',').Select(s => s.Trim()).ToArray() : null;
        var priorityList = !string.IsNullOrEmpty(priority) ? priority.Split(',').Select(p => p.Trim()).ToArray() : null;
        var productList = !string.IsNullOrEmpty(product) ? product.Split(',').Select(p => p.Trim()).ToArray() : null;
        var subProductList = !string.IsNullOrEmpty(subProduct) ? subProduct.Split(',').Select(sp => sp.Trim()).ToArray() : null;
        var assignedToList = !string.IsNullOrEmpty(assignedTo) ? assignedTo.Split(',').Select(a => a.Trim()).ToArray() : null;

        var response = await _ticketRepository.GetAllForCustomerAsync(
            customerId,
            statusList?.Length > 0 ? string.Join(",", statusList) : null,
            priorityList?.Length > 0 ? string.Join(",", priorityList) : null,
            productList?.Length > 0 ? string.Join(",", productList) : null,
            subProductList?.Length > 0 ? string.Join(",", subProductList) : null,
            assignedToList?.Length > 0 ? string.Join(",", assignedToList) : null,
            dateFrom,
            dateTo
        );

        return Ok(response);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error retrieving tickets for customer {CustomerId}", customerId);
        return StatusCode(500, "Internal server error occurred while retrieving tickets");
      }
    }


    [HttpGet("{id}")]
    public async Task<ActionResult<Ticket>> GetTicket(int id)
    {
      try
      {
        var ticket = await _ticketRepository.GetByIdAsync(id);
        if (ticket == null)
        {
          return NotFound($"Ticket with ID {id} not found");
        }

        return Ok(ticket);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error retrieving ticket {TicketId}", id);
        return StatusCode(500, "Internal server error occurred while retrieving ticket");
      }
    }

    [HttpGet("status/{status}")]
    public async Task<ActionResult<TicketResponse>> GetTicketsByStatus(string status)
    {
      try
      {
        var response = await _ticketRepository.GetByStatusAsync(status);
        return Ok(response);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error retrieving tickets by status {Status}", status);
        return StatusCode(500, "Internal server error occurred while retrieving tickets");
      }
    }

    [HttpGet("{ticketId}/attachments/{attachmentId}")]
    public async Task<IActionResult> GetAttachment(int ticketId, int attachmentId)
    {
      try
      {
        var attachment = await _ticketRepository.GetAttachmentAsync(ticketId, attachmentId);
        if (attachment == null)
        {
          return NotFound("Attachment not found");
        }

        if (!System.IO.File.Exists(attachment.FilePath))
        {
          _logger.LogWarning("File not found at path: {FilePath}", attachment.FilePath);
          return NotFound("File not found on server");
        }

        var fileBytes = await System.IO.File.ReadAllBytesAsync(attachment.FilePath);
        return File(fileBytes, attachment.ContentType, attachment.FileName);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error retrieving attachment {AttachmentId} for ticket {TicketId}", attachmentId, ticketId);
        return StatusCode(500, "Internal server error occurred while retrieving attachment");
      }
    }

    [HttpGet("customer/{customerId}/products")]
    public async Task<IActionResult> GetCustomerProducts(int customerId)
    {
      var productHierarchy = await _ticketRepository.GetProductHierarchyAsync(customerId);
      return Ok(productHierarchy);
    }

    [HttpGet("export/{format}/customer/{customerId}")]
    public async Task<IActionResult> ExportTickets(string format, int customerId)
    {
      try
      {
        var response = await _ticketRepository.GetAllForCustomerAsync(customerId);

        switch (format.ToLower())
        {
          case "csv":
            var csvContent = GenerateCsv(response.Tickets);
            return File(Encoding.UTF8.GetBytes(csvContent), "text/csv", $"tickets_export_{DateTime.UtcNow.AddMinutes(330):yyyyMMdd}.csv");

          case "json":
            var jsonContent = System.Text.Json.JsonSerializer.Serialize(response.Tickets, new System.Text.Json.JsonSerializerOptions
            {
              PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
              WriteIndented = true
            });
            return File(Encoding.UTF8.GetBytes(jsonContent), "application/json", $"tickets_export_{DateTime.UtcNow.AddMinutes(330):yyyyMMdd}.json");

          default:
            return BadRequest("Unsupported export format. Supported formats: csv, json");
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error exporting tickets for customer {CustomerId}", customerId);
        return StatusCode(500, "Internal server error occurred while exporting tickets");
      }
    }

    [HttpGet("{ticketId}/history")]
    public async Task<IActionResult> GetTicketHistory(int ticketId)
    {
      var history = await _ticketRepository.GetTicketHistoryAsync(ticketId);
      return Ok(history);
    }

    private string GenerateCsv(List<Ticket> tickets)
    {
      var sb = new StringBuilder();
      sb.AppendLine("ID,Subject,Status,Priority,Product,SubProduct,AssignedTo,CreatedOn,LastRepliedOn,AttachmentCount");
      foreach (var ticket in tickets)
      {
        sb.AppendLine($"{ticket.Id}," +
                        $"\"{EscapeCsv(ticket.Subject)}\"," +
                        $"\"{EscapeCsv(ticket.Status)}\"," +
                        $"\"{EscapeCsv(ticket.Priority)}\"," +
                        $"\"{EscapeCsv(ticket.Product ?? "")}\"," +
                        $"\"{EscapeCsv(ticket.SubProduct ?? "")}\"," +
                        $"\"{EscapeCsv(ticket.AssignedTo ?? "")}\"," +
                        $"{ticket.CreatedOn:yyyy-MM-dd HH:mm:ss}," +
                        $"{ticket.LastRepliedOn?.ToString("yyyy-MM-dd HH:mm:ss") ?? ""}," +
                        $"{ticket.AttachmentCount}");
      }
      return sb.ToString();
    }

    private string EscapeCsv(string value)
    {
      if (string.IsNullOrEmpty(value)) return value;
      return value.Replace("\"", "\"\"");
    }
  }
}
