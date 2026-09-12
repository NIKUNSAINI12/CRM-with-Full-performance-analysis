using System; // Trigger watch rebuild v2
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

    public TicketsController(
        ITicketRepository ticketRepository,
        IManager managerRepo,
        IEmailService emailService,
        ILogger<TicketsController> logger,
        INotificationService notificationService)
    {
      _ticketRepository = ticketRepository;
      _managerRepo = managerRepo;
      _emailService = emailService;
      _logger = logger;
      _notificationService = notificationService;
    }

    [AllowAnonymous]
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
        var actorRoleClaim = User.FindFirst("role")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

        if (int.TryParse(actorUidClaim, out var actorId))
        {
          request.CreatedByUserId = actorId;
          request.IsCreatedByCustomer = (actorId == request.CustomerId);
        }
        else
        {
          request.IsCreatedByCustomer = false;
        }

        var ticket = await _ticketRepository.CreateAsync(request);

        if (ticket != null)
        {
          var ticketId = ticket.Id;
          var ticketNumber = ticket.TicketNumber;
          var ticketSubject = ticket.Subject;
          var customerId = ticket.CustomerId;

          var customer = await _managerRepo.GetUserByIdAsync(customerId, "Customer");
          
          var customerFullName = customer?.FullName;
          var customerEmail = customer?.Email;

          int? assignedToId = null;
          if (request.AssignedToId.HasValue)
          {
            assignedToId = request.AssignedToId.Value;
          }
          else if (!string.IsNullOrEmpty(ticket.AssignedTo) && int.TryParse(ticket.AssignedTo, out var parsedAssigneeId))
          {
            assignedToId = parsedAssigneeId;
          }

          Assignee? assignee = null;
          if (assignedToId.HasValue)
          {
            try
            {
              assignee = await _managerRepo.GetAssigneeByIdAsync(assignedToId.Value);
            }
            catch (Exception ex)
            {
              _logger.LogError(ex, "Failed to fetch Assignee {AssigneeId} before sending email", assignedToId.Value);
            }
          }

          var assigneeFullName = assignee?.FullName;
          var assigneeEmail = assignee?.Email;

          _ = Task.Run(async () =>
          {
            try
            {
              // Deterministic thread ID — all emails for this ticket share this ID
              var threadId = EmailTemplates.GetThreadId(ticketNumber);

              // --- Confirmation Email to Customer (ROOT of thread) ---
              if (customerEmail != null)
              {
                try
                {
                  var emailContent = EmailTemplates.NewTicketConfirmation(
                      customerFullName!, ticketNumber,
                      request.DocketNumber,
                      request.DeadlineDate ?? ticket.LastDeadlineSet);
                  await _emailService.SendEmailAsync(customerEmail, emailContent.Subject, emailContent.Body, threadId, isThreadRoot: true);
                  
                  if (!string.IsNullOrWhiteSpace(request.SourceEmail))
                  {
                      await _emailService.SendEmailAsync(request.SourceEmail, emailContent.Subject, emailContent.Body, threadId);
                  }
                }
                catch (Exception emailEx)
                {
                  _logger.LogError(emailEx, "Failed to send confirmation email to customer {CustomerId} for ticket {TicketId}", customerId, ticketId);
                }
              }

              // --- Email to Assigned Developer (if assigned) ---
              if (!string.IsNullOrWhiteSpace(assigneeEmail))
              {
                try
                {
                  var emailContent = EmailTemplates.TicketAssigned(assigneeFullName!, ticketNumber, "the System", ticketSubject);
                  await _emailService.SendEmailAsync(assigneeEmail, emailContent.Subject, emailContent.Body, threadId);
                }
                catch (Exception emailEx)
                {
                  _logger.LogError(emailEx, "Failed to send email to Assignee {AssigneeEmail} for ticket {TicketId}", assigneeEmail, ticketId);
                }
              }

              _logger.LogInformation("Background email notifications sent successfully for ticket {TicketId}", ticketId);
            }
            catch (Exception ex)
            {
              _logger.LogError(ex, "Error in background email task for ticket {TicketId}", ticketId);
            }
          });

          // Trigger real-time notifications
          var actorUserIdClaim = User.FindFirst("uid")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
          if (int.TryParse(actorUserIdClaim, out var actorUserId))
          {
            try
            {
              await _notificationService.TriggerTicketNotificationAsync(ticketId, "Created", actorUserId);
            }
            catch (Exception ex)
            {
              _logger.LogError(ex, "Failed to trigger ticket creation notification for ticket {TicketId}", ticketId);
            }
          }
        }

        return CreatedAtAction(nameof(GetTicket), new { id = ticket.Id }, ticket);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error creating ticket");
        if (ex.Message.Contains("Invalid Docket Number") || ex.Message.Contains("CJDarclConnection"))
        {
          return BadRequest(ex.Message);
        }
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

        await _ticketRepository.UpdateAsync(id, request);

        int? oldAssigneeId = null;
        if (!string.IsNullOrEmpty(ticketBeforeUpdate.AssignedTo))
        {
          if (int.TryParse(ticketBeforeUpdate.AssignedTo, out int parsedId))
          {
            oldAssigneeId = parsedId;
          }
        }

        var ticketNumber = ticketBeforeUpdate.TicketNumber;
        var ticketSubject = ticketBeforeUpdate.Subject;
        var customerId = ticketBeforeUpdate.CustomerId;
        var oldStatus = ticketBeforeUpdate.Status;
        var oldPriority = ticketBeforeUpdate.Priority;
        var newAssignedToId = request.AssignedToId;
        var newStatus = request.Status;

        _ = Task.Run(async () =>
        {
          try
          {
            var threadId = EmailTemplates.GetThreadId(ticketNumber);

            if (newAssignedToId.HasValue && newAssignedToId != oldAssigneeId)
            {
              try
              {
                var newAssignee = await _managerRepo.GetAssigneeByIdAsync(newAssignedToId.Value);
                if (newAssignee != null)
                {
                  var emailContent = EmailTemplates.TicketAssigned(newAssignee.FullName, ticketNumber, "the System", ticketSubject);
                  await _emailService.SendEmailAsync(newAssignee.Email, emailContent.Subject, emailContent.Body, threadId);

                  var customer = await _managerRepo.GetUserByIdAsync(customerId, "Customer");
                  if (customer != null)
                  {
                    var custEmailContent = EmailTemplates.TicketAssignedToCustomer(customer.FullName, ticketNumber, newAssignee.FullName, ticketSubject);
                    await _emailService.SendEmailAsync(customer.Email, custEmailContent.Subject, custEmailContent.Body, threadId);

                    if (!string.IsNullOrWhiteSpace(ticketBeforeUpdate.SourceEmail))
                    {
                      await _emailService.SendEmailAsync(ticketBeforeUpdate.SourceEmail, custEmailContent.Subject, custEmailContent.Body, threadId);
                    }
                  }
                }
              }
              catch (Exception emailEx)
              {
                _logger.LogError(emailEx, "Failed to send assignment email for ticket {TicketId}", id);
              }
            }

            if (!string.IsNullOrEmpty(newStatus) &&
                !newStatus.Equals(oldStatus ?? "", StringComparison.OrdinalIgnoreCase))
            {
              try
              {
                var customer = await _managerRepo.GetUserByIdAsync(customerId, "Customer");
                if (customer != null)
                {
                  var emailContent = EmailTemplates.TicketStatusUpdate(customer.FullName, ticketNumber, newStatus, "the Support Team", ticketSubject, isCustomer: true, ticketId: ticketBeforeUpdate.Id, remark: request.Note ?? request.Remark);
                  await _emailService.SendEmailAsync(customer.Email, emailContent.Subject, emailContent.Body, threadId);

                  if (!string.IsNullOrWhiteSpace(ticketBeforeUpdate.SourceEmail))
                  {
                    await _emailService.SendEmailAsync(ticketBeforeUpdate.SourceEmail, emailContent.Subject, emailContent.Body, threadId);
                  }
                }
              }
              catch (Exception emailEx)
              {
                _logger.LogError(emailEx, "Failed to send closure email for ticket {TicketId}", id);
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

        await _managerRepo.UpdateTicketByPmAsync(ticketId, pmId, request);

        int? oldAssigneeId = null;
        if (!string.IsNullOrEmpty(ticketBeforeUpdate.AssignedTo))
        {
          if (int.TryParse(ticketBeforeUpdate.AssignedTo, out int parsedId))
          {
            oldAssigneeId = parsedId;
          }
        }

        var ticketNumber = ticketBeforeUpdate.TicketNumber;
        var ticketSubject = ticketBeforeUpdate.Subject;
        var customerId = ticketBeforeUpdate.CustomerId;
        var oldStatus = ticketBeforeUpdate.Status;
        var oldPriority = ticketBeforeUpdate.Priority;
        var newAssignedToId = request.AssignedToId;
        var newStatus = request.Status;

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

                    if (!string.IsNullOrWhiteSpace(ticketBeforeUpdate.SourceEmail))
                    {
                      await _emailService.SendEmailAsync(ticketBeforeUpdate.SourceEmail, custEmailContent.Subject, custEmailContent.Body, threadId);
                    }
                  }
              }
              catch (Exception emailEx)
              {
                _logger.LogError(emailEx, "Failed to send PM assignment email for ticket {TicketId}", ticketId);
              }
            }

            if (!string.IsNullOrEmpty(newStatus) &&
                !newStatus.Equals(oldStatus ?? "", StringComparison.OrdinalIgnoreCase))
            {
              try
              {
                if (customerEmail != null)
                {
                  var emailContent = EmailTemplates.TicketStatusUpdate(customerFullName!, ticketNumber, newStatus, pmName, ticketSubject, isCustomer: true, ticketId: ticketId, remark: request.Note);
                  await _emailService.SendEmailAsync(customerEmail, emailContent.Subject, emailContent.Body, threadId);

                  if (!string.IsNullOrWhiteSpace(ticketBeforeUpdate.SourceEmail))
                  {
                    await _emailService.SendEmailAsync(ticketBeforeUpdate.SourceEmail, emailContent.Subject, emailContent.Body, threadId);
                  }
                }
              }
              catch (Exception emailEx)
              {
                _logger.LogError(emailEx, "Failed to send PM closure email for ticket {TicketId}", ticketId);
                System.IO.File.WriteAllText(@"C:\Users\USER\Desktop\email_error_tkt.txt", emailEx.ToString());
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

    [HttpGet("dockets")]
    public async Task<IActionResult> GetDockets([FromQuery] string? search = null, [FromQuery] int? customerId = null)
    {
      try
      {
        var dockets = await _ticketRepository.GetDocketsAsync(search, customerId);
        return Ok(dockets);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error retrieving dockets from CJDarcl DB");
        return StatusCode(500, "Internal server error occurred while retrieving dockets");
      }
    }
    [HttpGet("dockets/{docketNo}")]
    public async Task<IActionResult> GetDocket(string docketNo)
    {
      try
      {
        var docket = await _ticketRepository.GetDocketDetailsAsync(docketNo);
        if (docket == null)
        {
          return NotFound($"Docket {docketNo} not found");
        }
        return Ok(docket);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error retrieving docket {DocketNo} from CJDarcl DB", docketNo);
        return StatusCode(500, "Internal server error occurred while retrieving docket details");
      }
    }

    [HttpGet("dockets/documents/{type}/{filename}")]
    public IActionResult GetDocketDocument(string type, string filename)
    {
      try
      {
        string basePath = "D:\\TMS_Angular\\working\\Image";
        string filePath = string.Empty;

        if (type.Equals("pod", StringComparison.OrdinalIgnoreCase))
        {
          filePath = Path.Combine(basePath, "POD", filename);
        }
        else if (type.Equals("signature", StringComparison.OrdinalIgnoreCase))
        {
          filePath = Path.Combine(basePath, "SIGNATURE", filename);
        }
        else if (type.Equals("status-photo", StringComparison.OrdinalIgnoreCase))
        {
          var folders = new[] { "DELIVERED_PHOTOS", "UNDELIVERED_PHOTOS", "PART_DELIVERY_PHOTOS", "CBU_PHOTOS" };
          foreach (var folder in folders)
          {
            var testPath = Path.Combine(basePath, folder, filename);
            if (System.IO.File.Exists(testPath))
            {
              filePath = testPath;
              break;
            }
          }
        }

        if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
        {
          return NotFound("File not found");
        }

        var fileBytes = System.IO.File.ReadAllBytes(filePath);
        var contentType = "image/jpeg";
        if (filename.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
          contentType = "application/pdf";
        }
        else if (filename.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
          contentType = "image/png";
        }
        return File(fileBytes, contentType, filename);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error serving docket document {Type}/{Filename}", type, filename);
        return StatusCode(500, "Internal server error serving document");
      }
    }

    [HttpGet("{id}/rate")]
    [AllowAnonymous]
    public async Task<IActionResult> RateTicket(int id, [FromQuery] int rating)
    {
      try
      {
        if (rating < 1 || rating > 5)
        {
          return BadRequest("Rating must be between 1 and 5.");
        }

        var success = await _ticketRepository.UpdateRatingAsync(id, rating);
        if (!success)
        {
          return NotFound($"Ticket with ID {id} not found.");
        }

        var ticket = await _ticketRepository.GetByIdAsync(id);
        var displayTicketNumber = !string.IsNullOrWhiteSpace(ticket?.TicketNumber)
            ? ticket.TicketNumber
            : $"CJD_CRM-{DateTime.UtcNow.AddMinutes(330):yyyy}-{id}";

        var starsText = "".PadLeft(rating, '★').PadRight(5, '☆');
        var html = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Thank You</title>
    <link href='https://fonts.googleapis.com/css2?family=Inter:wght@400;600;700&display=swap' rel='stylesheet'>
    <style>
        body {{
            background-color: #0f172a;
            color: #f1f5f9;
            font-family: 'Inter', sans-serif;
            display: flex;
            align-items: center;
            justify-content: center;
            height: 100vh;
            margin: 0;
        }}
        .card {{
            background-color: #1e293b;
            padding: 40px;
            border-radius: 16px;
            box-shadow: 0 10px 25px -5px rgba(0, 0, 0, 0.3), 0 8px 10px -6px rgba(0, 0, 0, 0.3);
            text-align: center;
            max-width: 400px;
            border: 1px solid #334155;
        }}
        h1 {{
            color: #10b981;
            font-size: 24px;
            margin-bottom: 16px;
        }}
        p {{
            color: #94a3b8;
            font-size: 15px;
            line-height: 1.6;
            margin-bottom: 24px;
        }}
        .stars {{
            font-size: 32px;
            color: #eab308;
            margin-bottom: 20px;
        }}
    </style>
</head>
<body>
    <div class='card'>
        <div class='stars'>{starsText}</div>
        <h1>Thank You!</h1>
        <p>Your feedback is highly appreciated. We have successfully saved your rating of {rating} out of 5 stars for Ticket {displayTicketNumber}.</p>
    </div>
</body>
</html>";
        return Content(html, "text/html");
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error saving rating for ticket {TicketId}", id);
        return StatusCode(500, "Internal server error occurred while saving your rating.");
      }
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

    [HttpGet("search-global")]
    [Authorize]
    public async Task<IActionResult> SearchGlobal([FromQuery] string query, [FromQuery] string searchType = "auto")
    {
      if (string.IsNullOrWhiteSpace(query))
      {
        return Ok(new List<object>());
      }

      try
      {
        var cleanQuery = query.Trim();
        var results = await _ticketRepository.SearchGlobalTicketsAsync(cleanQuery, searchType);
        return Ok(results);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error performing global search for query {Query}", query);
        return StatusCode(500, "An internal server error occurred.");
      }
    }
  }
}
