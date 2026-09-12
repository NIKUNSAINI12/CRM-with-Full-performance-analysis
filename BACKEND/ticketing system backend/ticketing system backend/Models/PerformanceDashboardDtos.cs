using System;
using System.Collections.Generic;

namespace ticketing_system_backend.Models
{
    // =====================================================================
    // CORE PERFORMANCE DASHBOARD DTOs
    // =====================================================================

    public class PerformanceKpiDto
    {
        public int TotalTickets { get; set; }
        public int ResolvedTickets { get; set; }
        public int UnresolvedTickets { get; set; }
        public int InProgressTickets { get; set; }
        public double ResolutionRate { get; set; }
        public double AvgResolutionHours { get; set; }
        public int TotalLocations { get; set; }
        public int TotalCustomers { get; set; }
        public int TotalStaff { get; set; }
        public int UserCount { get; set; }
        public int CustomerCount { get; set; }
    }

    public class LocationSummaryDto
    {
        public int LocationId { get; set; }
        public string LocationCode { get; set; } = string.Empty;
        public string LocationName { get; set; } = string.Empty;
        public int TotalTickets { get; set; }
        public int ResolvedTickets { get; set; }
        public int UnresolvedTickets { get; set; }
        public double ResolutionRate { get; set; }
        public int UserCount { get; set; }
        public int CustomerCount { get; set; }
        public double AvgResolutionHours { get; set; }
    }

    public class DepartmentStatDto
    {
        public int ModuleId { get; set; }
        public string ModuleName { get; set; } = string.Empty;
        public int TotalTickets { get; set; }
        public int ResolvedTickets { get; set; }
        public int UnresolvedTickets { get; set; }
        public double ResolutionRate { get; set; }
    }

    public class CustomerStatDto
    {
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerCode { get; set; } = string.Empty;
        public int TotalTickets { get; set; }
        public int ResolvedTickets { get; set; }
        public int UnresolvedTickets { get; set; }
        public double ResolutionRate { get; set; }
        public string TopDepartment { get; set; } = string.Empty;
    }

    public class TeamStatDto
    {
        public int RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public string ManagerName { get; set; } = string.Empty;
        public string ManagerRole { get; set; } = string.Empty;
        public int UserCount { get; set; }
        public int TotalTickets { get; set; }
        public int ResolvedTickets { get; set; }
        public int UnresolvedTickets { get; set; }
        public double ResolutionRate { get; set; }
    }

    public class PerformanceFilterParams
    {
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int? LocationId { get; set; }
        public string? Status { get; set; }
        public string? Priority { get; set; }
    }

    public class UserRankDto
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public int LocationId { get; set; }
        public string LocationName { get; set; } = string.Empty;
        public string LocationCode { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public int TotalTickets { get; set; }
        public int ResolvedTickets { get; set; }
        public int OpenTickets { get; set; }
        public double ResolutionRate { get; set; }
        public double AvgResolutionHours { get; set; }
    }

    public class PerformanceOverviewDto
    {
        public PerformanceKpiDto Summary { get; set; } = new();
        public List<LocationSummaryDto> Locations { get; set; } = new();
        public List<DepartmentStatDto> Departments { get; set; } = new();
        public List<CustomerStatDto> TopCustomers { get; set; } = new();
        public List<TeamStatDto> Teams { get; set; } = new();
        public List<UserRankDto> TopUsers { get; set; } = new();
    }

    // =====================================================================
    // LOCATION DETAIL DTOs
    // =====================================================================

    public class LocationBasicDto
    {
        public int LocationId { get; set; }
        public string LocationCode { get; set; } = string.Empty;
        public string LocationName { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Hierarchy { get; set; } = string.Empty;
    }

    public class LocationUserDto
    {
        public int UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string MobileNo { get; set; } = string.Empty;
        public int TotalAssigned { get; set; }
        public int Resolved { get; set; }
        public int Open { get; set; }
        public double ResolutionRate { get; set; }
        public double AvgResolutionHours { get; set; }
    }

    public class LocationDepartmentDto
    {
        public int ModuleId { get; set; }
        public string ModuleName { get; set; } = string.Empty;
        public int TotalTickets { get; set; }
        public int ResolvedTickets { get; set; }
        public int UnresolvedTickets { get; set; }
        public double ResolutionRate { get; set; }
        public List<LocationUserDto> Users { get; set; } = new();
    }

    public class LocationTeamDto
    {
        public int RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public string ManagerName { get; set; } = string.Empty;
        public string ManagerRole { get; set; } = string.Empty;
        public int TotalTickets { get; set; }
        public int ResolvedTickets { get; set; }
        public int UnresolvedTickets { get; set; }
        public double ResolutionRate { get; set; }
        public List<LocationUserDto> Users { get; set; } = new();
    }

    public class TicketSimpleDto
    {
        public int TicketId { get; set; }
        public string TicketNumber { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string AssigneeName { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string ModuleName { get; set; } = string.Empty;
        public DateTime? CreatedOn { get; set; }
        public DateTime? ClosedOn { get; set; }
        public int? Rating { get; set; }
    }

    public class LocationPerformanceDetailDto
    {
        public LocationBasicDto Location { get; set; } = new();
        public PerformanceKpiDto Kpi { get; set; } = new();
        public List<LocationDepartmentDto> Departments { get; set; } = new();
        public List<LocationTeamDto> Teams { get; set; } = new();
        public List<LocationUserDto> Users { get; set; } = new();
        public List<CustomerStatDto> Customers { get; set; } = new();
        public List<TicketSimpleDto> RecentTickets { get; set; } = new();
    }

    // =====================================================================
    // CUSTOMER DETAIL DTOs
    // =====================================================================

    public class CustomerBasicDto
    {
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerCode { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string MobileNo { get; set; } = string.Empty;
    }

    public class LocationStatDto
    {
        public int LocationId { get; set; }
        public string LocationCode { get; set; } = string.Empty;
        public string LocationName { get; set; } = string.Empty;
        public int TotalTickets { get; set; }
        public int ResolvedTickets { get; set; }
        public int UnresolvedTickets { get; set; }
    }

    public class UserStatDto
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public string LocationName { get; set; } = string.Empty;
        public int TotalTickets { get; set; }
        public int ResolvedTickets { get; set; }
        public int UnresolvedTickets { get; set; }
    }

    public class CustomerPerformanceDetailDto
    {
        public CustomerBasicDto Customer { get; set; } = new();
        public PerformanceKpiDto Kpi { get; set; } = new();
        public List<DepartmentStatDto> DepartmentBreakdown { get; set; } = new();
        public List<TeamStatDto> TeamBreakdown { get; set; } = new();
        public List<UserStatDto> UserBreakdown { get; set; } = new();
        public List<LocationStatDto> LocationBreakdown { get; set; } = new();
        public Dictionary<string, int> PriorityBreakdown { get; set; } = new();
        public List<TicketSimpleDto> Tickets { get; set; } = new();
    }

    // =====================================================================
    // USER DETAIL DTOs
    // =====================================================================

    public class UserBasicDto
    {
        public int UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string MobileNo { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public int LocationId { get; set; }
        public string LocationName { get; set; } = string.Empty;
        public string LocationCode { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    public class MonthlyTrendDto
    {
        public int Month { get; set; }
        public int Year { get; set; }
        public string Label { get; set; } = string.Empty;
        public int TotalTickets { get; set; }
        public int ResolvedTickets { get; set; }
    }

    public class UserPerformanceDetailDto
    {
        public UserBasicDto User { get; set; } = new();
        public PerformanceKpiDto Kpi { get; set; } = new();
        public List<DepartmentStatDto> DepartmentBreakdown { get; set; } = new();
        public List<CustomerStatDto> CustomerBreakdown { get; set; } = new();
        public List<MonthlyTrendDto> MonthlyTrend { get; set; } = new();
        public List<TicketSimpleDto> Tickets { get; set; } = new();
    }

    // =====================================================================
    // TEAM DETAIL DTOs (Hierarchy & Member Stats)
    // =====================================================================

    public class TeamMemberUserDto
    {
        public int UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public int LocationId { get; set; }
        public string LocationName { get; set; } = string.Empty;
        public string LocationCode { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string MobileNo { get; set; } = string.Empty;
        public bool IsManager { get; set; }
        public int TotalAssigned { get; set; }
        public int Resolved { get; set; }
        public int Unresolved { get; set; }
        public double ResolutionRate { get; set; }
        public double AvgResolutionHours { get; set; }
    }

    public class TeamPerformanceDetailDto
    {
        public TeamStatDto Team { get; set; } = new();
        public UserBasicDto Manager { get; set; } = new();
        public PerformanceKpiDto Kpi { get; set; } = new();
        public List<TeamMemberUserDto> Members { get; set; } = new();
        public List<DepartmentStatDto> DepartmentBreakdown { get; set; } = new();
        public List<CustomerStatDto> CustomerBreakdown { get; set; } = new();
        public List<TicketSimpleDto> Tickets { get; set; } = new();
    }
}
