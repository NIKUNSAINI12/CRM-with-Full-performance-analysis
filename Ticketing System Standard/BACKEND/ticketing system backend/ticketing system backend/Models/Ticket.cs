using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Http;

namespace ticketing_system_backend.Models
{
  public class Ticket
  {
    public int Id { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string TicketNumber { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Difficulty { get; set; } = "Medium";  // Easy | Medium | Hard | Expert
    public string? Product { get; set; }
    public string? SubProduct { get; set; }
    public string? AssignedTo { get; set; }
    public DateTime CreatedOn { get; set; }
    public DateTime? LastRepliedOn { get; set; }


    public int CustomerId { get; set; }
    public int? SprintId { get; set; }
    public string? SprintName { get; set; }
    public int? SprintOrder { get; set; }
    public string? TicketSource { get; set; }
    public string? DocketNumber { get; set; }

    // This is the correct, single declaration for Attachments
    public List<TicketAttachment> Attachments { get; set; } = new();

    // This correctly holds the messages
    public List<TicketMessage> Messages { get; set; } = new();

    // These calculated properties are useful and can stay
    public bool HasAttachment => Attachments?.Any() == true;
    public int AttachmentCount => Attachments?.Count ?? 0;

    public string CustomerName { get; set; }
    public string AssignedToName { get; set; }
    public string? AssignedToNumber { get; set; }
  }

  public class TicketAttachment
  {
    public int Id { get; set; }
    public int TicketId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public DateTime UploadedOn { get; set; }
    public int UploadedById { get; set; }
    public string UploadedBy { get; set; } = string.Empty;
  }

  public class TicketResponse
  {
    public List<Ticket> Tickets { get; set; } = new List<Ticket>();
    public int TotalCount { get; set; }
  }

  public class CreateTicketRequest
  {
    public string Subject { get; set; }
    public string Description { get; set; }
    public string Priority { get; set; }
    public string TicketType { get; set; }
    public int CustomerId { get; set; }
    public string? DocketNumber { get; set; }

    // CHANGE THESE LINES from string to int
    public int? ProductId { get; set; }
    public int? ModuleId { get; set; }
    public int? SubModuleId { get; set; }
    public string? ProductName { get; set; }
    public string? ModuleName { get; set; }
    public string? SubModuleName { get; set; }
    public string? TicketSource { get; set; }
    public int? AssignedToId { get; set; }

    // Who actually created the ticket (populated from JWT by the controller)
    public int? CreatedByUserId { get; set; }
    public string? CreatedByUserRole { get; set; }
    public string? CreatedByUserName { get; set; }

    public List<IFormFile>? Attachments { get; set; }
  }
  
  public class ProductData
  {
    public int Id { get; set; }
    public string Name { get; set; }
    public List<ModuleData> Modules { get; set; } = new();
  }

  public class ModuleData
  {
    public int Id { get; set; }
    public string Name { get; set; }
    public int ProductId { get; set; }
    public List<SubModuleData> SubModules { get; set; } = new();
  }
  
  public class UpdateTicketRequest
  {
    public string? Status { get; set; }
    public string? Priority { get; set; }
    public int? AssignedToId { get; set; }
  }
  
  public class SubModuleData
  {
    public int Id { get; set; }
    public string Name { get; set; }
    public int ModuleId { get; set; }
  }

  public class TatTicket
  {
    public string TicketId { get; set; }
    public string Customer { get; set; }
    public string DocketNumber { get; set; }
    public string Subject { get; set; }
    public string Priority { get; set; }
    public string Status { get; set; }
    public string AssignedTo { get; set; }
    public string SlaStatus { get; set; } // "On Track", "At Risk", "Breached"
  }

  public class TatDashboardStats
  {
    public int TotalActiveTickets { get; set; }
    public int OpenTickets { get; set; }
    public int InProgressTickets { get; set; }
    public int SlaCompliancePercent { get; set; }
    public List<TatTicket> RecentTickets { get; set; } = new();
  }

}
