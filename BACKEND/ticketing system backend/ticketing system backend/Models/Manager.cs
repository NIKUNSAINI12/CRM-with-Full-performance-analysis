using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace ticketing_system_backend.Models
{
  public class Manager
  {
    
  }

  // ── Parent-Child Ticket Relations ─────────────────────────────
  public class LinkedTicketSummary
  {
    public int Id { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;

    public DateTime CreatedOn { get; set; }
    public string? AssignedToName { get; set; }
    public int? ParentId { get; set; }
  }

  public class TicketRelationsResponse
  {
    public LinkedTicketSummary? Parent { get; set; }
    public List<LinkedTicketSummary> Children { get; set; } = new();
    public List<LinkedTicketSummary> Tree { get; set; } = new();
  }

  public class SetParentRequest
  {
    /// <summary>Pass null to remove the parent (make this ticket a root).</summary>
    public int? ParentTicketId { get; set; }
    public int PmId { get; set; }
  }
  // New parent model
  public class TicketTimelineAnalysis
  {
    public TicketTimelineMetrics Summary { get; set; }
    public IEnumerable<TicketStateDuration> Breakdown { get; set; }
  }

  // Model for the detailed breakdown (Result Set 1)
  public class TicketStateDuration
  {
    public string StateOrAssigneeName { get; set; }
    public int TotalDurationMinutes { get; set; }
  }
  public class TicketCommunication
  {
    [Key]
    public int Id { get; set; }

    [Required]
    public int TicketId { get; set; }
    [ForeignKey("TicketId")]
    public virtual Ticket Ticket { get; set; }

    public string CommentText { get; set; }

    [Required]
    public DateTime PostedOn { get; set; }

    // Tracks *who* posted it
    public int PostedByUserId { get; set; }
    [Required]
    public string PostedByName { get; set; }
    [Required]
    public string PostedByUserRole { get; set; } // "PM", "Developer", "Customer"

    // This is the "privacy" flag
    [Required]
    public string Channel { get; set; } // "Customer" or "Developer"

    // Navigation property for one-to-many
    public virtual ICollection<CommunicationAttachment> Attachments { get; set; }

    public TicketCommunication()
    {
      Attachments = new HashSet<CommunicationAttachment>();
    }
  }
  // Existing model for the summary (Result Set 2)
  public class TicketTimelineMetrics

  {
    public DateTime? TicketCreatedDate { get; set; }
    public DateTime? TicketClosedDate { get; set; }
    public int? TimeToFirstResponseMinutes { get; set; }
    public int? TimeToResolutionHours { get; set; }
  }
  public class Module
  {
    public int Id { get; set; }
    public string Name { get; set; }
    public int ProductId { get; set; }
  }
  public class Product
  {
    public int Id { get; set; }
    public string Name { get; set; }
  }
  public class TicketListResponse
  {
    public IEnumerable<Ticket> Tickets { get; set; }
    public int TotalCount { get; set; }
  }
  public class SimpleProductRequest
  {
    public string Name { get; set; }
    public List<SimpleModule> Modules { get; set; } = new List<SimpleModule>();
  }

  // A simple representation of a module
  public class SimpleModule
  {
    public string Name { get; set; }
    public List<SimpleSubModule> SubModules { get; set; } = new List<SimpleSubModule>();
  }

  // A simple representation of a sub-module
  public class SimpleSubModule
  {
    public string Name { get; set; }
  }
  public class CustomerTicketGroup
  {
    public int CustomerId { get; set; }
    public string CustomerName { get; set; }
    public List<TicketSummary> Tickets { get; set; } = new();
  }

  public class TicketSummary
  {
    public int Id { get; set; }
    public string Subject { get; set; }
    public string Status { get; set; }
    public string Priority { get; set; }
    public string AssignedToName { get; set; }
    public DateTime CreatedOn { get; set; }
    public string TicketNumber { get; set; }
    public DateTime? LastRepliedOn { get; set; }
    public string Product { get; set; }
    public string SubProduct { get; set; }
  }
  public class LoginRequest
  {
    public string UserNumber { get; set; }
    public string Password { get; set; }
  }
  public class User
  {
    public int Id { get; set; }
    public string UserNumber { get; set; }
    public string FullName { get; set; }
    public string Email { get; set; }
    public string Role { get; set; }
    public string? Password { get; set; }
    public string? ContactPerson { get; set; }
    public string? PhoneNo { get; set; }
    public string? MobileNo { get; set; }
    public int? ManagerId { get; set; }
    public int? DefaultAssigneeId { get; set; }
  }
  public class Assignee
  {
    public int Id { get; set; }
    public string FullName { get; set; }
    public string Email { get; set; }

    // This property must exist to receive the data
    public string AssigneeNumber { get; set; }
    public string? Password { get; set; }
    public string? MobileNo { get; set; }
    public int? ManagerId { get; set; }
    public string? Role { get; set; }

  }
  public class CustomerUpdateTicketRequest
  {
    public string Status { get; set; }
    public string Priority { get; set; }
    public string? Note { get; set; }
    public string? Remark { get; set; }
  }
  public class PmLoginRequest
  {
    public string UserNumber { get; set; }
    public string Password { get; set; }
  }

  // Request model for Assignee login
  public class AssigneeLoginRequest
  {
    public string AssigneeNumber { get; set; }
    public string Password { get; set; }
  }
  public class UpdatePmRequest
  {
    public string FullName { get; set; }
    public string Email { get; set; }
    public string? Password { get; set; }

    public string? MobileNo { get; set; }
    public int? ManagerId { get; set; }

  }

  public class UpdateUserRequest
  {
    public string FullName { get; set; }
    public string Email { get; set; }
    public string? Password { get; set; }

    // ADD THIS PROPERTY
    // This allows the request to carry the list of selected product IDs
    public List<int>? ProductIds { get; set; }
    public string? ContactPerson { get; set; }
    public string? PhoneNo { get; set; }
    public string? MobileNo { get; set; }
    public int? DefaultAssigneeId { get; set; }
    public int? ManagerId { get; set; }
  }
  public class UpdateAssigneeRequest
  {
    public string FullName { get; set; }
    public string Email { get; set; }
    public string? Password { get; set; } // Optional: only if allowing password changes here
    public string? MobileNo { get; set; }
    public int? ManagerId { get; set; }
  }
  public class UpdateAssignmentRequest
  {
    public int? AssignedToId { get; set; }

    // ADD THESE TWO PROPERTIES:
    public int UpdatedByUserId { get; set; }
    public string UpdatedByUserRole { get; set; } // e.g., "PM"
  }
  public class CreateAssigneeRequest
  {
    public string FullName { get; set; }
    public string Email { get; set; }
    public string Password { get; set; } // ADDED
    public string? MobileNo { get; set; }
    public int? ManagerId { get; set; }
  }
  public class TeamLoginRequest
  {
    public string UserOrAssigneeNumber { get; set; }
    public string Password { get; set; }
  }

  // Assumed model for your Assignees table
  
  public class CreateUserRequest
  {
    public string FullName { get; set; }
    public string? Password { get; set; }
    public string Email { get; set; }
    public string Role { get; set; }
    public string? ContactPerson { get; set; }
    public string? PhoneNo { get; set; }
    public string? MobileNo { get; set; }
    public List<int>? ProductIds { get; set; }
    public int? DefaultAssigneeId { get; set; }
    public int? ManagerId { get; set; }
  }
  public class TicketDeadline
  {
    public int TicketId { get; set; }
    public DateTime DeadlineDate { get; set; }
    public int SetByManagerId { get; set; }
    public DateTime SetOn { get; set; }
  }
  public class SetDeadlineRequest
  {
    
    public DateTime DeadlineDate { get; set; }
    public int ManagerId { get; set; }
  }

  public class TicketDashboardView
  {
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string Subject { get; set; }
    public string Status { get; set; }
    public string Priority { get; set; }
    public DateTime CreatedOn { get; set; }
    public string? CustomerName { get; set; }
    public string? AssignedToName { get; set; }
    public string? AssignedToNumber { get; set; }
    public string TicketNumber { get; set; }
    public DateTime? LastRepliedOn { get; set; }
    public string Product { get; set; }
    public string SubProduct { get; set; }
    public int? CreatedByUserId { get; set; }
    public string? CreatedByName { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedByRole { get; set; }
    public bool? IsCreatedByCustomer { get; set; }
    public DateTime? ClosedOn { get; set; }
    public string? ClosingRemark { get; set; }
  }
 
  public class UpdateStatusRequest
  {
    public string Status { get; set; }
    public string? Note { get; set; }
  }

  public class TicketHistory
  {
    public int HistoryId { get; set; }
    public DateTime ChangeDate { get; set; }
    public string EventDescription { get; set; }
    public string? ChangedByUserRole { get; set; }
    public string? ChangedByUserName { get; set; }
  }
  public class PmTicketUpdateRequest
  {
    // Properties are nullable — PM might only change one at a time
    public string? Status { get; set; }
    public string? Priority { get; set; }

    public int? AssignedToId { get; set; }
    public string? Note { get; set; }
    public DateTime? DeadlineDate { get; set; }
  }
  public class DeveloperUpdateRequest
  {
    public string Status { get; set; }
  }
  public class DashboardStats
  {
    public int TotalPending { get; set; }
    public int ForReview { get; set; }
    public int UrgentTickets { get; set; }
    public int NewToday { get; set; }
    public int DelayedTickets { get; set; }
  }
  public class TicketReworkInfo
  {
    public int UserId { get; set; }
    public string UserName { get; set; }
    public string UserRole { get; set; }
    public int ReworkCount { get; set; }
  }
  public class TotalReworkCounts
  {
    public int TotalReworkCount { get; set; }
  }

  // Response model for PMs (to hold both channels)
  public class PmCommunicationResponse
  {
    public List<TicketCommunication> CustomerChannel { get; set; }
    public List<TicketCommunication> DeveloperChannel { get; set; }
    public List<TicketCommunication> UpdateNotesCustomer { get; set; }
    public List<TicketCommunication> UpdateNotesDeveloper { get; set; }
    public List<TicketCommunication> GlobalNotes { get; set; } // Added for Global Notes
  }
  public class NewCommentRequest
  {
    public string CommentText { get; set; }
    public string Channel { get; set; } // "Customer" or "Developer"

    // Add these three properties:
    public string PostedByUserRole { get; set; } // "PM", "Developer", "Customer"
    public int PostedByUserId { get; set; }
    public string PostedByName { get; set; }
  }

  public class CommunicationAttachment
  {
    [Key]
    public int Id { get; set; }

    [Required]
    public int CommunicationId { get; set; }
    [ForeignKey("CommunicationId")]
    public virtual TicketCommunication TicketCommunication { get; set; }

    [Required]
    public string FileName { get; set; }

    [Required]
    public string FileUrl { get; set; } // Path to the file on your server/blob storage

    public long FileSize { get; set; } // Size in bytes
  }
  public class AssigneePerformanceStat
  {
    public int AssigneeId { get; set; }
    public string AssigneeName { get; set; } = string.Empty;

    // --- Raw Counts (for display badges) ---
    public int TotalTicketsCount { get; set; }        // Unweighted count of all assigned tickets
    public int TotalTicketsOpen { get; set; }         // Currently open
    public int TotalPendingPmReview { get; set; }     // In PM review queue
    public int TotalOnHold { get; set; }              // On hold
    public int TotalTicketsClosed { get; set; }       // Closed in period
    public int TotalStayedClosed { get; set; }        // Closed and never reopened
    public int TotalReworks { get; set; }             // Rework/reopened count
    public int TotalOverdue { get; set; }             // Missed deadline count
    public int TotalWorkDone { get; set; }            // Submitted for review count

    // --- Difficulty-Weighted Points (Expert=5, Hard=3, Medium=2, Easy=1) ---
    public double WeightedAssigned { get; set; }
    public double WeightedClosed { get; set; }
    public double WeightedStayedClosed { get; set; }
    public double WeightedReworks { get; set; }
    public double WeightedOverdue { get; set; }
    public double WeightedOpen { get; set; }
    public double WeightedPmReview { get; set; }
    public double WeightedWorkDone { get; set; }

    // --- Difficulty Insight ---
    // avg of (Expert=5,Hard=3,Medium=2,Easy=1) across assigned tickets
    public double AvgDifficultyWeight { get; set; }

    // --- Pre-computed Rates (0-100 scale, difficulty-weighted for fairness) ---
    public double ClosureRate { get; set; }        // StayedClosed / TotalAssigned × 100
    public double ReworkRate { get; set; }         // Reworks / TotalAssigned × 100
    public double DeadlineMissRate { get; set; }   // Overdue / TotalAssigned × 100
    public double ClearanceRate { get; set; }      // Closed / (Closed+Open+PmReview) × 100

    // --- Legacy support ---
    public int TotalTicketsPending => TotalTicketsOpen + TotalPendingPmReview + TotalOnHold;
  }

  public class CustomTicketStatus
  {
    public int Id { get; set; }
    public string StatusName { get; set; }
    public bool IsActive { get; set; }
  }

  public class PriorityMaster
  {
    public int Id { get; set; }
    public string PriorityName { get; set; }
    public int TATHours { get; set; }
    public int? ResponseSLA { get; set; }
    public bool IsActive { get; set; }
  }

  public class IssueCategoryMaster
  {
    public int Id { get; set; }
    public string CategoryName { get; set; }
    public bool IsActive { get; set; }
  }
}

