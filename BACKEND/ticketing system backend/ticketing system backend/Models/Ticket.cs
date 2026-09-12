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

    public string? Product { get; set; }
    public string? SubProduct { get; set; }
    public string? AssignedTo { get; set; }
    public DateTime CreatedOn { get; set; }
    public DateTime? LastRepliedOn { get; set; }


    public int CustomerId { get; set; }
    public string? TicketSource { get; set; }
    public string? DocketNumber { get; set; }
    public string? SourcePhone { get; set; }
    public string? SourceEmail { get; set; }
    public DateTime? LastDeadlineSet { get; set; }

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
    public int? Rating { get; set; }
    public int? CreatedByUserId { get; set; }
    public string? CreatedByName { get; set; }
    public string? CreatedByRole { get; set; }
    public bool? IsCreatedByCustomer { get; set; }
    public string? LastUpdateNote { get; set; }
    public string? ClosingRemark { get; set; }
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
    public int? CreatedByUserId { get; set; }
    public bool? IsCreatedByCustomer { get; set; }
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
    public string? SourcePhone { get; set; }
    public string? SourceEmail { get; set; }
    public DateTime? DeadlineDate { get; set; }

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
    public string? Note { get; set; }
    public string? Remark { get; set; }
  }
  
  public class SubModuleData
  {
    public int Id { get; set; }
    public string Name { get; set; }
    public int ModuleId { get; set; }
  }

  public class TatTicket
  {
    public int Id { get; set; }
    public string TicketId { get; set; }
    public string Customer { get; set; }
    public string DocketNumber { get; set; }
    public string Subject { get; set; }
    public string Priority { get; set; }
    public string Status { get; set; }
    public string AssignedTo { get; set; }
    public string SlaStatus { get; set; } // "On Track", "At Risk", "Breached"
    public DateTime CreatedOn { get; set; }
    public string? CreatedBy { get; set; }
    public double? Rating { get; set; }
    public string CategoryName { get; set; }
  }

  public class TatDashboardStats
  {
    public int TotalActiveTickets { get; set; }
    public int OpenTickets { get; set; }
    public int InProgressTickets { get; set; }
    public int ClosedTickets { get; set; }
    public int SlaCompliancePercent { get; set; }
    public double AverageResolutionTime { get; set; }
    public double AverageSatisfactionScore { get; set; }
    public List<RatingCount> RatingSummary { get; set; } = new();
    public List<CategoryCount> CategoryTrend { get; set; } = new();
    public List<CustomerTatStat> CustomerStats { get; set; } = new();
    public List<TatTicket> RecentTickets { get; set; } = new();
  }

  public class CustomerTatStat
  {
    public int CustomerId { get; set; }
    public string CustomerName { get; set; }
    public int TotalTickets { get; set; }
    public int ClosedTickets { get; set; }
    public int SlaCompliancePercent { get; set; }
    public double AverageResolutionTime { get; set; }
    public double AverageSatisfactionScore { get; set; }
  }

  public class RatingCount
  {
    public int Rating { get; set; }
    public int Count { get; set; }
  }

  public class CategoryCount
  {
    public string CategoryName { get; set; }
    public int Count { get; set; }
  }

  public class DocketDetails
  {
    public long DocketId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? DocketDate { get; set; }
    public string DocketNo { get; set; } = string.Empty;
    public DateTime? Edd { get; set; }
    public string? BillingParty { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public string? PaymentType { get; set; }
    public string? CurrentLocation { get; set; }
    public string? FromCity { get; set; }
    public string? ToCity { get; set; }
    public string? Destination { get; set; }
    public string? Consignor { get; set; }
    public string? Consignee { get; set; }
    public string? Remarks { get; set; }
    public int? Packages { get; set; }
    public string? TransportMode { get; set; }
    public int? StatusId { get; set; }
    public string? PodDocumentName { get; set; }
  }

  public class DocketHistory
  {
    public string? TransitLocation { get; set; }
    public string? ActivityAtLocation { get; set; }
    public DateTime? ActivityDateTime { get; set; }
    public string? Remark { get; set; }
    public string? StatusPhotoName { get; set; }
    public string? StatusPhotoType { get; set; }
  }
}
