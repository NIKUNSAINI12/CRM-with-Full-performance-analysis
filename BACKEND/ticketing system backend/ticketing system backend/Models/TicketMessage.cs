using System;
using System.Collections.Generic;

namespace ticketing_system_backend.Models
{
  public class TicketMessage
  {
    public int Id { get; set; }
    public int TicketId { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } // Changed from CreatedOn
    public int CreatedBy { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public bool IsCustomerMessage { get; set; }
    public List<TicketAttachment> Attachments { get; set; } = new();
  }
}
