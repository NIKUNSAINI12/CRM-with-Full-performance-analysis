using System;

namespace ticketing_system_backend.Models
{
  public class Sprint
  {
    public int SprintId { get; set; }
    public string SprintName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Status { get; set; } = "Future"; // Future | Active | Completed
    public int CreatedBy { get; set; }
    public DateTime CreatedOn { get; set; }
    // Historical stats (for completed sprints)
    public int TotalTicketsAtClose { get; set; }
    public int CompletedTicketsAtClose { get; set; }
    public int InProgressTicketsAtClose { get; set; }
    public int OpenTicketsAtClose { get; set; }
    public int OnHoldTicketsAtClose { get; set; }
    public int ReworkTicketsAtClose { get; set; }
  }

  public class CreateSprintRequest
  {
    public string SprintName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Status { get; set; } = "Future";
  }

  public class UpdateSprintRequest
  {
    public string SprintName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Status { get; set; } = "Future";
  }

  public class AssignTicketsRequest
  {
    public string TicketIds { get; set; } = string.Empty; // Comma-separated IDs
  }

  public class CompleteSprintRequest
  {
    public int? DestinationSprintId { get; set; } // NULL = move to Backlog
  }

  public class SprintAnalyticsDto
  {
    public IEnumerable<SprintPerformanceDto> Sprints { get; set; } = Array.Empty<SprintPerformanceDto>();
    public IEnumerable<WeeklyCompletionDto> WeeklyCompletion { get; set; } = Array.Empty<WeeklyCompletionDto>();
    public IEnumerable<MonthlyCompletionDto> MonthlyCompletion { get; set; } = Array.Empty<MonthlyCompletionDto>();
    public IEnumerable<YearlyCompletionDto> YearlyCompletion { get; set; } = Array.Empty<YearlyCompletionDto>();
  }

  public class SprintPerformanceDto
  {
    public int SprintId { get; set; }
    public string SprintName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public int TotalTickets { get; set; }
    public int CompletedTickets { get; set; }
    public int InProgressTickets { get; set; }
    public int OpenTickets { get; set; }
  }

  public class WeeklyCompletionDto
  {
    public int Year { get; set; }
    public int Week { get; set; }
    public int CompletedCount { get; set; }
  }

  public class MonthlyCompletionDto
  {
    public int Year { get; set; }
    public int Month { get; set; }
    public int CompletedCount { get; set; }
  }

  public class YearlyCompletionDto
  {
    public int Year { get; set; }
    public int CompletedCount { get; set; }
  }

  public class UpdateTicketOrderRequest
  {
    public List<TicketOrderDto> Orders { get; set; } = new();
  }

  public class TicketOrderDto
  {
    public int TicketId { get; set; }
    public int Order { get; set; }
  }

  public class CompletedSprintHistoryDto
  {
    public Sprint Sprint { get; set; } = new();
    public List<SprintTicketHistoryDto> Tickets { get; set; } = new();
  }

  public class SprintTicketHistoryDto
  {
    public int HistoryId { get; set; }
    public int SprintId { get; set; }
    public int TicketId { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string StatusAtClose { get; set; } = string.Empty;
    public int? RolledToSprintId { get; set; }
    public string? RolledToSprintName { get; set; }
  }
}
