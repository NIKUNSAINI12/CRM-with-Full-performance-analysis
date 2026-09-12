using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ticketing_system_backend.Helpers;
using ticketing_system_backend.Interface;
using ticketing_system_backend.Models;

namespace ticketing_system_backend.Repository
{
    public class LocTicketRow
    {
        public int TicketId { get; set; }
        public string TicketNumber { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public int? Rating { get; set; }
        public DateTime? CreatedOn { get; set; }
        public DateTime? ClosedOn { get; set; }
        public int ModuleId { get; set; }
        public string ModuleName { get; set; } = string.Empty;
        public int? CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerCode { get; set; } = string.Empty;
        public int? AssignedTo { get; set; }
        public string AssigneeName { get; set; } = string.Empty;
        public string AssigneeUserName { get; set; } = string.Empty;
        public string AssigneeEmail { get; set; } = string.Empty;
        public string AssigneeMobile { get; set; } = string.Empty;
        public int RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public int? ManagerId { get; set; }
        public string ManagerName { get; set; } = string.Empty;
        public string ManagerRole { get; set; } = string.Empty;
    }

    public class CustTicketRow
    {
        public int TicketId { get; set; }
        public string TicketNumber { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public int? Rating { get; set; }
        public DateTime? CreatedOn { get; set; }
        public DateTime? ClosedOn { get; set; }
        public int ModuleId { get; set; }
        public string ModuleName { get; set; } = string.Empty;
        public int? AssignedTo { get; set; }
        public string AssigneeName { get; set; } = string.Empty;
        public int RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public int? ManagerId { get; set; }
        public string ManagerName { get; set; } = string.Empty;
        public string ManagerRole { get; set; } = string.Empty;
        public int LocationId { get; set; }
        public string LocationName { get; set; } = string.Empty;
        public string LocationCode { get; set; } = string.Empty;
    }

    public class UserTicketRow
    {
        public int TicketId { get; set; }
        public string TicketNumber { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public int? Rating { get; set; }
        public DateTime? CreatedOn { get; set; }
        public DateTime? ClosedOn { get; set; }
        public int ModuleId { get; set; }
        public string ModuleName { get; set; } = string.Empty;
        public int? CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerCode { get; set; } = string.Empty;
    }

    public class TeamTicketRow
    {
        public int TicketId { get; set; }
        public string TicketNumber { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public int? Rating { get; set; }
        public DateTime? CreatedOn { get; set; }
        public DateTime? ClosedOn { get; set; }
        public int? AssignedTo { get; set; }
        public string AssigneeName { get; set; } = string.Empty;
        public string AssigneeRole { get; set; } = string.Empty;
        public int ModuleId { get; set; }
        public string ModuleName { get; set; } = string.Empty;
        public int? CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerCode { get; set; } = string.Empty;
    }

    public class PerformanceAnalyticsRepository : IPerformanceAnalyticsRepository
    {
        private readonly IDbConnection _crmConnection;
        private readonly IConfiguration _config;
        private readonly ILogger<PerformanceAnalyticsRepository> _logger;

        public PerformanceAnalyticsRepository(
            IDbConnection crmConnection,
            IConfiguration config,
            ILogger<PerformanceAnalyticsRepository> logger)
        {
            _crmConnection = crmConnection;
            _config = config;
            _logger = logger;
        }

        private string GetCjDarclDb() => _config["CJDarclDbName"] ?? DbConfig.CJDarclDb ?? "CJDarcl";
        private string GetCrmDb() => _config["TicketingDbName"] ?? DbConfig.TicketingDb ?? "CRMDarcl";

        // =====================================================================
        // 1. OVERVIEW: Summary KPIs, Locations, Departments, Customers, Teams, Users
        // =====================================================================
        public async Task<PerformanceOverviewDto> GetPerformanceOverviewAsync(PerformanceFilterParams? filters = null)
        {
            var cjdDb = GetCjDarclDb();
            var crmDb = GetCrmDb();

            var response = new PerformanceOverviewDto();

            try
            {
                var tktFilterConditions = new List<string>();
                var filterParams = new DynamicParameters();

                if (filters != null)
                {
                    if (filters.FromDate.HasValue)
                    {
                        tktFilterConditions.Add("t.CreatedOn >= @FromDate");
                        filterParams.Add("FromDate", filters.FromDate.Value);
                    }
                    if (filters.ToDate.HasValue)
                    {
                        tktFilterConditions.Add("t.CreatedOn <= @ToDate");
                        filterParams.Add("ToDate", filters.ToDate.Value);
                    }
                    if (!string.IsNullOrWhiteSpace(filters.Status))
                    {
                        if (filters.Status.Equals("Resolved", StringComparison.OrdinalIgnoreCase))
                            tktFilterConditions.Add("t.Status IN ('Closed', 'Resolved')");
                        else if (filters.Status.Equals("Unresolved", StringComparison.OrdinalIgnoreCase))
                            tktFilterConditions.Add("t.Status NOT IN ('Closed', 'Resolved')");
                        else
                        {
                            tktFilterConditions.Add("t.Status = @Status");
                            filterParams.Add("Status", filters.Status);
                        }
                    }
                    if (!string.IsNullOrWhiteSpace(filters.Priority))
                    {
                        tktFilterConditions.Add("t.Priority = @Priority");
                        filterParams.Add("Priority", filters.Priority);
                    }
                }

                string tktWhere = tktFilterConditions.Count > 0 ? "WHERE " + string.Join(" AND ", tktFilterConditions) : "";
                string andTktWhere = tktFilterConditions.Count > 0 ? "AND " + string.Join(" AND ", tktFilterConditions) : "";

                // Query 1: Location Summary with aggregated tickets and staff
                string sqlLocations = $@"
                    WITH TicketBase AS (
                        SELECT 
                            t.TicketId,
                            t.Status,
                            t.CreatedOn,
                            t.ClosedOn,
                            t.CustomerId,
                            COALESCE(u.LocationId, d.FromLocationId, cLoc.LocationId, 1) AS ResolvedLocationId
                        FROM [{crmDb}].dbo.Tickets t
                        LEFT JOIN [{cjdDb}].dbo.CL_Master_User u ON t.AssignedTo = u.UserId
                        LEFT JOIN [{cjdDb}].dbo.CL_Docket d ON t.DocketNumber = d.DocketNo
                        OUTER APPLY (
                            SELECT TOP 1 LocationId 
                            FROM [{cjdDb}].dbo.CL_Master_Customer_Location_Mapping 
                            WHERE CustomerId = t.CustomerId
                        ) cLoc
                        {tktWhere}
                    ),
                    TicketSummary AS (
                        SELECT 
                            ResolvedLocationId AS LocationId,
                            COUNT(*) AS TotalTickets,
                            SUM(CASE WHEN Status IN ('Closed', 'Resolved') THEN 1 ELSE 0 END) AS ResolvedTickets,
                            SUM(CASE WHEN Status NOT IN ('Closed', 'Resolved') THEN 1 ELSE 0 END) AS UnresolvedTickets,
                            COUNT(DISTINCT CustomerId) AS CustomerCount,
                            AVG(CASE WHEN ClosedOn IS NOT NULL AND CreatedOn IS NOT NULL THEN DATEDIFF(hour, CreatedOn, ClosedOn) * 1.0 ELSE NULL END) AS AvgResolutionHours
                        FROM TicketBase
                        GROUP BY ResolvedLocationId
                    ),
                    UserSummary AS (
                        SELECT LocationId, COUNT(*) AS UserCount
                        FROM [{cjdDb}].dbo.CL_Master_User
                        WHERE IsActive = 1
                        GROUP BY LocationId
                    )
                    SELECT 
                        loc.LocationId,
                        loc.LocationCode,
                        loc.LocationName,
                        COALESCE(ts.TotalTickets, 0) AS TotalTickets,
                        COALESCE(ts.ResolvedTickets, 0) AS ResolvedTickets,
                        COALESCE(ts.UnresolvedTickets, 0) AS UnresolvedTickets,
                        COALESCE(us.UserCount, 0) AS UserCount,
                        COALESCE(ts.CustomerCount, 0) AS CustomerCount,
                        ROUND(COALESCE(ts.AvgResolutionHours, 0), 1) AS AvgResolutionHours
                    FROM [{cjdDb}].dbo.CL_Master_Location loc
                    LEFT JOIN TicketSummary ts ON loc.LocationId = ts.LocationId
                    LEFT JOIN UserSummary us ON loc.LocationId = us.LocationId
                    ORDER BY TotalTickets DESC, loc.LocationName ASC;
                ";

                var locations = (await _crmConnection.QueryAsync<LocationSummaryDto>(sqlLocations, filterParams)).ToList();
                foreach (var loc in locations)
                {
                    loc.ResolutionRate = loc.TotalTickets > 0 ? Math.Round((double)loc.ResolvedTickets / loc.TotalTickets * 100, 1) : 0;
                }
                response.Locations = locations;

                // Query 2: Summary KPIs
                string sqlKpis = $@"
                    SELECT 
                        COUNT(*) AS TotalTickets,
                        SUM(CASE WHEN t.Status IN ('Closed', 'Resolved') THEN 1 ELSE 0 END) AS ResolvedTickets,
                        SUM(CASE WHEN t.Status NOT IN ('Closed', 'Resolved') THEN 1 ELSE 0 END) AS UnresolvedTickets,
                        SUM(CASE WHEN t.Status IN ('In Progress', 'Rework') THEN 1 ELSE 0 END) AS InProgressTickets,
                        AVG(CASE WHEN t.ClosedOn IS NOT NULL AND t.CreatedOn IS NOT NULL THEN DATEDIFF(hour, t.CreatedOn, t.ClosedOn) * 1.0 ELSE NULL END) AS AvgResolutionHours,
                        COUNT(DISTINCT t.CustomerId) AS TotalCustomers,
                        COUNT(DISTINCT t.AssignedTo) AS TotalStaff
                    FROM [{crmDb}].dbo.Tickets t
                    {tktWhere};
                ";
                var kpi = await _crmConnection.QueryFirstOrDefaultAsync<PerformanceKpiDto>(sqlKpis, filterParams) ?? new PerformanceKpiDto();
                kpi.TotalLocations = locations.Count(l => l.TotalTickets > 0 || l.UserCount > 0);
                kpi.ResolutionRate = kpi.TotalTickets > 0 ? Math.Round((double)kpi.ResolvedTickets / kpi.TotalTickets * 100, 1) : 0;
                kpi.AvgResolutionHours = Math.Round(kpi.AvgResolutionHours, 1);
                response.Summary = kpi;

                // Query 3: Department / Module Comparison
                string sqlDepartments = $@"
                    SELECT 
                        COALESCE(m.Id, 0) AS ModuleId,
                        COALESCE(m.Name, 'General Support') AS ModuleName,
                        COUNT(*) AS TotalTickets,
                        SUM(CASE WHEN t.Status IN ('Closed', 'Resolved') THEN 1 ELSE 0 END) AS ResolvedTickets,
                        SUM(CASE WHEN t.Status NOT IN ('Closed', 'Resolved') THEN 1 ELSE 0 END) AS UnresolvedTickets
                    FROM [{crmDb}].dbo.Tickets t
                    LEFT JOIN [{crmDb}].dbo.Modules m ON t.ModuleId = m.Id
                    {tktWhere}
                    GROUP BY COALESCE(m.Id, 0), COALESCE(m.Name, 'General Support')
                    ORDER BY TotalTickets DESC;
                ";
                var departments = (await _crmConnection.QueryAsync<DepartmentStatDto>(sqlDepartments, filterParams)).ToList();
                foreach (var d in departments)
                {
                    d.ResolutionRate = d.TotalTickets > 0 ? Math.Round((double)d.ResolvedTickets / d.TotalTickets * 100, 1) : 0;
                }
                response.Departments = departments;

                // Query 4: Customer Comparison
                string sqlCustomers = $@"
                    SELECT 
                        c.CustomerId,
                        c.CustomerName,
                        c.CustomerCode,
                        COUNT(t.TicketId) AS TotalTickets,
                        SUM(CASE WHEN t.Status IN ('Closed', 'Resolved') THEN 1 ELSE 0 END) AS ResolvedTickets,
                        SUM(CASE WHEN t.Status NOT IN ('Closed', 'Resolved') THEN 1 ELSE 0 END) AS UnresolvedTickets
                    FROM [{crmDb}].dbo.Tickets t
                    INNER JOIN [{cjdDb}].dbo.CL_Master_Customer c ON t.CustomerId = c.CustomerId
                    {tktWhere}
                    GROUP BY c.CustomerId, c.CustomerName, c.CustomerCode
                    ORDER BY TotalTickets DESC;
                ";
                var customers = (await _crmConnection.QueryAsync<CustomerStatDto>(sqlCustomers, filterParams)).ToList();
                foreach (var c in customers)
                {
                    c.ResolutionRate = c.TotalTickets > 0 ? Math.Round((double)c.ResolvedTickets / c.TotalTickets * 100, 1) : 0;
                }
                response.TopCustomers = customers;

                // Query 5: Manager-Reporting Hierarchy Teams (True Operational Teams)
                string sqlTeams = $@"
                    SELECT 
                        m.UserId AS RoleId,
                        m.Name + ' Team' AS RoleName,
                        m.Name AS ManagerName,
                        COALESCE(mr.RoleName, 'Manager') AS ManagerRole,
                        COUNT(DISTINCT u.UserId) AS UserCount,
                        COUNT(DISTINCT t.TicketId) AS TotalTickets,
                        COUNT(DISTINCT CASE WHEN t.Status IN ('Closed', 'Resolved') THEN t.TicketId ELSE NULL END) AS ResolvedTickets,
                        COUNT(DISTINCT CASE WHEN t.Status NOT IN ('Closed', 'Resolved') THEN t.TicketId ELSE NULL END) AS UnresolvedTickets
                    FROM [{cjdDb}].dbo.CL_Master_User m
                    LEFT JOIN [{cjdDb}].dbo.CL_Master_Role mr ON m.RoleId = mr.RoleId
                    INNER JOIN [{cjdDb}].dbo.CL_Master_User u ON u.ManagerId = m.UserId
                    LEFT JOIN [{crmDb}].dbo.Tickets t ON (t.AssignedTo = u.UserId OR t.AssignedTo = m.UserId) {andTktWhere}
                    GROUP BY m.UserId, m.Name, mr.RoleName
                    ORDER BY TotalTickets DESC, UserCount DESC;
                ";
                var teams = (await _crmConnection.QueryAsync<TeamStatDto>(sqlTeams, filterParams)).ToList();
                foreach (var tm in teams)
                {
                    tm.ResolutionRate = tm.TotalTickets > 0 ? Math.Round((double)tm.ResolvedTickets / tm.TotalTickets * 100, 1) : 0;
                }
                response.Teams = teams;

                // Query 6: Top User Performance
                string sqlUsers = $@"
                    SELECT 
                        u.UserId,
                        u.Name AS UserName,
                        COALESCE(loc.LocationId, 0) AS LocationId,
                        COALESCE(loc.LocationName, 'HQ') AS LocationName,
                        COALESCE(loc.LocationCode, 'HQ') AS LocationCode,
                        COALESCE(r.RoleName, 'Assignee') AS RoleName,
                        COUNT(t.TicketId) AS TotalTickets,
                        SUM(CASE WHEN t.Status IN ('Closed', 'Resolved') THEN 1 ELSE 0 END) AS ResolvedTickets,
                        SUM(CASE WHEN t.Status NOT IN ('Closed', 'Resolved') THEN 1 ELSE 0 END) AS OpenTickets,
                        AVG(CASE WHEN t.ClosedOn IS NOT NULL AND t.CreatedOn IS NOT NULL THEN DATEDIFF(hour, t.CreatedOn, t.ClosedOn) * 1.0 ELSE NULL END) AS AvgResolutionHours
                    FROM [{crmDb}].dbo.Tickets t
                    INNER JOIN [{cjdDb}].dbo.CL_Master_User u ON t.AssignedTo = u.UserId
                    LEFT JOIN [{cjdDb}].dbo.CL_Master_Location loc ON u.LocationId = loc.LocationId
                    LEFT JOIN [{cjdDb}].dbo.CL_Master_Role r ON u.RoleId = r.RoleId
                    {tktWhere}
                    GROUP BY u.UserId, u.Name, loc.LocationId, loc.LocationName, loc.LocationCode, r.RoleName
                    ORDER BY TotalTickets DESC;
                ";
                var users = (await _crmConnection.QueryAsync<UserRankDto>(sqlUsers, filterParams)).ToList();
                foreach (var u in users)
                {
                    u.ResolutionRate = u.TotalTickets > 0 ? Math.Round((double)u.ResolvedTickets / u.TotalTickets * 100, 1) : 0;
                    u.AvgResolutionHours = Math.Round(u.AvgResolutionHours, 1);
                }
                response.TopUsers = users;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching Performance Overview stats");
                throw;
            }

            return response;
        }

        // =====================================================================
        // 2. LOCATION DETAIL: When user clicks a Location
        // =====================================================================
        public async Task<LocationPerformanceDetailDto?> GetLocationDetailAsync(int locationId)
        {
            var cjdDb = GetCjDarclDb();
            var crmDb = GetCrmDb();

            var detail = new LocationPerformanceDetailDto();

            try
            {
                // Location info
                string sqlLoc = $@"
                    SELECT LocationId, LocationCode, LocationName, COALESCE(Address, '') AS Address, COALESCE(LocationHierarchyId, '') AS Hierarchy
                    FROM [{cjdDb}].dbo.CL_Master_Location
                    WHERE LocationId = @LocationId;
                ";
                var loc = await _crmConnection.QueryFirstOrDefaultAsync<LocationBasicDto>(sqlLoc, new { LocationId = locationId });
                if (loc == null) return null;
                detail.Location = loc;

                // Base tickets for this location (assignee located here OR docket origin/dest here OR customer mapped here)
                string sqlBase = $@"
                    WITH LocTickets AS (
                        SELECT 
                            t.TicketId,
                            COALESCE(t.TicketNumber, 'TKT-' + CAST(t.TicketId AS VARCHAR)) AS TicketNumber,
                            COALESCE(t.Subject, 'No Subject') AS Subject,
                            COALESCE(t.Status, 'Open') AS Status,
                            COALESCE(t.Priority, 'Medium') AS Priority,
                            t.Rating,
                            t.CreatedOn,
                            t.ClosedOn,
                            COALESCE(t.ModuleId, 0) AS ModuleId,
                            COALESCE(m.Name, 'General Support') AS ModuleName,
                            t.CustomerId,
                            COALESCE(c.CustomerName, 'Customer #' + CAST(COALESCE(t.CustomerId, 0) AS VARCHAR)) AS CustomerName,
                            COALESCE(c.CustomerCode, '') AS CustomerCode,
                            t.AssignedTo,
                            COALESCE(u.Name, 'Unassigned') AS AssigneeName,
                            COALESCE(u.UserName, '') AS AssigneeUserName,
                            COALESCE(u.EmailId, '') AS AssigneeEmail,
                            COALESCE(u.MobileNo, '') AS AssigneeMobile,
                            COALESCE(r.RoleId, 0) AS RoleId,
                            COALESCE(r.RoleName, 'Support Team') AS RoleName,
                            COALESCE(mgr.UserId, u.UserId, 0) AS ManagerId,
                            COALESCE(mgr.Name, u.Name, 'General Support') AS ManagerName,
                            COALESCE(mgrRole.RoleName, r.RoleName, 'Lead') AS ManagerRole
                        FROM [{crmDb}].dbo.Tickets t
                        LEFT JOIN [{crmDb}].dbo.Modules m ON t.ModuleId = m.Id
                        LEFT JOIN [{cjdDb}].dbo.CL_Master_User u ON t.AssignedTo = u.UserId
                        LEFT JOIN [{cjdDb}].dbo.CL_Master_Role r ON u.RoleId = r.RoleId
                        LEFT JOIN [{cjdDb}].dbo.CL_Master_User mgr ON u.ManagerId = mgr.UserId
                        LEFT JOIN [{cjdDb}].dbo.CL_Master_Role mgrRole ON mgr.RoleId = mgrRole.RoleId
                        LEFT JOIN [{cjdDb}].dbo.CL_Master_Customer c ON t.CustomerId = c.CustomerId
                        LEFT JOIN [{cjdDb}].dbo.CL_Docket d ON t.DocketNumber = d.DocketNo
                        OUTER APPLY (
                            SELECT TOP 1 LocationId 
                            FROM [{cjdDb}].dbo.CL_Master_Customer_Location_Mapping 
                            WHERE CustomerId = t.CustomerId
                        ) cLoc
                        WHERE COALESCE(u.LocationId, d.FromLocationId, cLoc.LocationId, 1) = @LocationId
                    )
                    SELECT * FROM LocTickets;
                ";

                var locTickets = (await _crmConnection.QueryAsync<LocTicketRow>(sqlBase, new { LocationId = locationId })).ToList();

                // Users stationed at this location
                string sqlStaff = $@"
                    SELECT 
                        u.UserId,
                        COALESCE(u.Name, u.UserName) AS Name,
                        COALESCE(u.UserName, '') AS UserName,
                        COALESCE(r.RoleName, 'Assignee') AS RoleName,
                        COALESCE(u.EmailId, '') AS Email,
                        COALESCE(u.MobileNo, '') AS MobileNo
                    FROM [{cjdDb}].dbo.CL_Master_User u
                    LEFT JOIN [{cjdDb}].dbo.CL_Master_Role r ON u.RoleId = r.RoleId
                    WHERE u.LocationId = @LocationId AND u.IsActive = 1;
                ";
                var staffList = (await _crmConnection.QueryAsync<LocationUserDto>(sqlStaff, new { LocationId = locationId })).ToList();

                // Aggregate staff tickets
                foreach (var s in staffList)
                {
                    var userTkts = locTickets.Where(t => t.AssignedTo == s.UserId).ToList();
                    s.TotalAssigned = userTkts.Count;
                    s.Resolved = userTkts.Count(t => t.Status == "Closed" || t.Status == "Resolved");
                    s.Open = s.TotalAssigned - s.Resolved;
                    s.ResolutionRate = s.TotalAssigned > 0 ? Math.Round((double)s.Resolved / s.TotalAssigned * 100, 1) : 0;
                }
                detail.Users = staffList.OrderByDescending(s => s.TotalAssigned).ToList();

                // Location KPI
                int totalTickets = locTickets.Count;
                int resolvedTickets = locTickets.Count(t => t.Status == "Closed" || t.Status == "Resolved");
                int unresolved = totalTickets - resolvedTickets;
                int inProgress = locTickets.Count(t => t.Status == "In Progress" || t.Status == "Rework");

                detail.Kpi = new PerformanceKpiDto
                {
                    TotalTickets = totalTickets,
                    ResolvedTickets = resolvedTickets,
                    UnresolvedTickets = unresolved,
                    InProgressTickets = inProgress,
                    ResolutionRate = totalTickets > 0 ? Math.Round((double)resolvedTickets / totalTickets * 100, 1) : 0,
                    UserCount = staffList.Count,
                    CustomerCount = locTickets.Select(t => t.CustomerId).Where(id => id.HasValue).Distinct().Count()
                };

                // Department / Module breakdown with users in that department
                var deptGroups = locTickets.GroupBy(t => new { t.ModuleId, t.ModuleName });
                foreach (var g in deptGroups)
                {
                    var deptDto = new LocationDepartmentDto
                    {
                        ModuleId = g.Key.ModuleId,
                        ModuleName = g.Key.ModuleName,
                        TotalTickets = g.Count(),
                        ResolvedTickets = g.Count(t => t.Status == "Closed" || t.Status == "Resolved"),
                        UnresolvedTickets = g.Count(t => t.Status != "Closed" && t.Status != "Resolved"),
                    };
                    deptDto.ResolutionRate = deptDto.TotalTickets > 0 ? Math.Round((double)deptDto.ResolvedTickets / deptDto.TotalTickets * 100, 1) : 0;

                    // Users working on this module
                    var userSubgroup = g.Where(t => t.AssignedTo.HasValue).GroupBy(t => t.AssignedTo!.Value);
                    foreach (var ug in userSubgroup)
                    {
                        var first = ug.First();
                        int uTot = ug.Count();
                        int uRes = ug.Count(t => t.Status == "Closed" || t.Status == "Resolved");
                        deptDto.Users.Add(new LocationUserDto
                        {
                            UserId = ug.Key,
                            Name = !string.IsNullOrEmpty(first.AssigneeName) ? first.AssigneeName : first.AssigneeUserName,
                            UserName = first.AssigneeUserName,
                            RoleName = first.RoleName,
                            TotalAssigned = uTot,
                            Resolved = uRes,
                            Open = uTot - uRes,
                            ResolutionRate = uTot > 0 ? Math.Round((double)uRes / uTot * 100, 1) : 0
                        });
                    }
                    deptDto.Users = deptDto.Users.OrderByDescending(u => u.TotalAssigned).ToList();
                    detail.Departments.Add(deptDto);
                }
                detail.Departments = detail.Departments.OrderByDescending(d => d.TotalTickets).ToList();

                // Teams breakdown using Manager reporting hierarchy
                var teamGroups = locTickets.GroupBy(t => new { 
                    RoleId = t.ManagerId ?? t.RoleId, 
                    RoleName = (!string.IsNullOrEmpty(t.ManagerName) ? t.ManagerName + " Team" : t.RoleName),
                    ManagerName = (!string.IsNullOrEmpty(t.ManagerName) ? t.ManagerName : t.RoleName),
                    ManagerRole = t.ManagerRole
                });
                foreach (var tg in teamGroups)
                {
                    var teamDto = new LocationTeamDto
                    {
                        RoleId = tg.Key.RoleId,
                        RoleName = tg.Key.RoleName,
                        ManagerName = tg.Key.ManagerName,
                        ManagerRole = tg.Key.ManagerRole,
                        TotalTickets = tg.Count(),
                        ResolvedTickets = tg.Count(t => t.Status == "Closed" || t.Status == "Resolved"),
                        UnresolvedTickets = tg.Count(t => t.Status != "Closed" && t.Status != "Resolved"),
                    };
                    teamDto.ResolutionRate = teamDto.TotalTickets > 0 ? Math.Round((double)teamDto.ResolvedTickets / teamDto.TotalTickets * 100, 1) : 0;

                    var teamUsers = tg.Where(t => t.AssignedTo.HasValue).GroupBy(t => t.AssignedTo!.Value);
                    foreach (var ug in teamUsers)
                    {
                        var first = ug.First();
                        int uTot = ug.Count();
                        int uRes = ug.Count(t => t.Status == "Closed" || t.Status == "Resolved");
                        teamDto.Users.Add(new LocationUserDto
                        {
                            UserId = ug.Key,
                            Name = !string.IsNullOrEmpty(first.AssigneeName) ? first.AssigneeName : first.AssigneeUserName,
                            UserName = first.AssigneeUserName,
                            RoleName = first.RoleName,
                            TotalAssigned = uTot,
                            Resolved = uRes,
                            Open = uTot - uRes,
                            ResolutionRate = uTot > 0 ? Math.Round((double)uRes / uTot * 100, 1) : 0
                        });
                    }
                    teamDto.Users = teamDto.Users.OrderByDescending(u => u.TotalAssigned).ToList();
                    detail.Teams.Add(teamDto);
                }
                detail.Teams = detail.Teams.OrderByDescending(t => t.TotalTickets).ToList();

                // Customers at this location
                var custGroups = locTickets.Where(t => t.CustomerId.HasValue).GroupBy(t => new { CustomerId = t.CustomerId!.Value, t.CustomerName, t.CustomerCode });
                foreach (var cg in custGroups)
                {
                    int cTot = cg.Count();
                    int cRes = cg.Count(t => t.Status == "Closed" || t.Status == "Resolved");
                    detail.Customers.Add(new CustomerStatDto
                    {
                        CustomerId = cg.Key.CustomerId,
                        CustomerName = cg.Key.CustomerName,
                        CustomerCode = cg.Key.CustomerCode,
                        TotalTickets = cTot,
                        ResolvedTickets = cRes,
                        UnresolvedTickets = cTot - cRes,
                        ResolutionRate = cTot > 0 ? Math.Round((double)cRes / cTot * 100, 1) : 0
                    });
                }
                detail.Customers = detail.Customers.OrderByDescending(c => c.TotalTickets).ToList();

                // Recent Tickets
                detail.RecentTickets = locTickets.Take(50).Select(t => new TicketSimpleDto
                {
                    TicketId = t.TicketId,
                    TicketNumber = t.TicketNumber,
                    Subject = t.Subject,
                    Status = t.Status,
                    Priority = t.Priority,
                    AssigneeName = t.AssigneeName,
                    CustomerName = t.CustomerName,
                    ModuleName = t.ModuleName,
                    CreatedOn = t.CreatedOn,
                    ClosedOn = t.ClosedOn,
                    Rating = t.Rating
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching Location Detail for LocationId {LocationId}", locationId);
                throw;
            }

            return detail;
        }

        // =====================================================================
        // 3. CUSTOMER DETAIL: When user clicks a Customer
        // =====================================================================
        public async Task<CustomerPerformanceDetailDto?> GetCustomerDetailAsync(int customerId)
        {
            var cjdDb = GetCjDarclDb();
            var crmDb = GetCrmDb();

            var detail = new CustomerPerformanceDetailDto();

            try
            {
                // Customer profile
                string sqlCust = $@"
                    SELECT c.CustomerId, c.CustomerName, c.CustomerCode, COALESCE(c.EmailId1, '') AS Email, COALESCE(d.MobileNo, '') AS MobileNo
                    FROM [{cjdDb}].dbo.CL_Master_Customer c
                    LEFT JOIN [{cjdDb}].dbo.CL_Master_Customer_Detail d ON c.CustomerId = d.CustomerId
                    WHERE c.CustomerId = @CustomerId;
                ";
                var cust = await _crmConnection.QueryFirstOrDefaultAsync<CustomerBasicDto>(sqlCust, new { CustomerId = customerId });
                if (cust == null) return null;
                detail.Customer = cust;

                // All tickets for this customer
                string sqlTickets = $@"
                    SELECT 
                        t.TicketId,
                        COALESCE(t.TicketNumber, 'TKT-' + CAST(t.TicketId AS VARCHAR)) AS TicketNumber,
                        COALESCE(t.Subject, 'No Subject') AS Subject,
                        COALESCE(t.Status, 'Open') AS Status,
                        COALESCE(t.Priority, 'Medium') AS Priority,
                        t.Rating,
                        t.CreatedOn,
                        t.ClosedOn,
                        COALESCE(t.ModuleId, 0) AS ModuleId,
                        COALESCE(m.Name, 'General Support') AS ModuleName,
                        t.AssignedTo,
                        COALESCE(u.Name, 'Unassigned') AS AssigneeName,
                        COALESCE(r.RoleId, 0) AS RoleId,
                        COALESCE(r.RoleName, 'Support Team') AS RoleName,
                        COALESCE(loc.LocationId, 1) AS LocationId,
                        COALESCE(loc.LocationName, 'CENTRAL CORPORATE OFFICE') AS LocationName,
                        COALESCE(loc.LocationCode, 'CCO') AS LocationCode
                    FROM [{crmDb}].dbo.Tickets t
                    LEFT JOIN [{crmDb}].dbo.Modules m ON t.ModuleId = m.Id
                    LEFT JOIN [{cjdDb}].dbo.CL_Master_User u ON t.AssignedTo = u.UserId
                    LEFT JOIN [{cjdDb}].dbo.CL_Master_Role r ON u.RoleId = r.RoleId
                    LEFT JOIN [{cjdDb}].dbo.CL_Master_Location loc ON u.LocationId = loc.LocationId
                    WHERE t.CustomerId = @CustomerId
                    ORDER BY t.CreatedOn DESC;
                ";
                var tickets = (await _crmConnection.QueryAsync<CustTicketRow>(sqlTickets, new { CustomerId = customerId })).ToList();

                // KPIs
                int total = tickets.Count;
                int resolved = tickets.Count(t => t.Status == "Closed" || t.Status == "Resolved");
                int unresolved = total - resolved;
                int inProgress = tickets.Count(t => t.Status == "In Progress" || t.Status == "Rework");

                detail.Kpi = new PerformanceKpiDto
                {
                    TotalTickets = total,
                    ResolvedTickets = resolved,
                    UnresolvedTickets = unresolved,
                    InProgressTickets = inProgress,
                    ResolutionRate = total > 0 ? Math.Round((double)resolved / total * 100, 1) : 0
                };

                // Department Breakdown (which department was ticket raised against)
                var deptGroups = tickets.GroupBy(t => new { t.ModuleId, t.ModuleName });
                foreach (var g in deptGroups)
                {
                    int dTot = g.Count();
                    int dRes = g.Count(t => t.Status == "Closed" || t.Status == "Resolved");
                    detail.DepartmentBreakdown.Add(new DepartmentStatDto
                    {
                        ModuleId = g.Key.ModuleId,
                        ModuleName = g.Key.ModuleName,
                        TotalTickets = dTot,
                        ResolvedTickets = dRes,
                        UnresolvedTickets = dTot - dRes,
                        ResolutionRate = dTot > 0 ? Math.Round((double)dRes / dTot * 100, 1) : 0
                    });
                }
                detail.DepartmentBreakdown = detail.DepartmentBreakdown.OrderByDescending(d => d.TotalTickets).ToList();

                // Team / Role Breakdown
                var teamGroups = tickets.GroupBy(t => new { t.RoleId, t.RoleName });
                foreach (var tg in teamGroups)
                {
                    int tTot = tg.Count();
                    int tRes = tg.Count(t => t.Status == "Closed" || t.Status == "Resolved");
                    detail.TeamBreakdown.Add(new TeamStatDto
                    {
                        RoleId = tg.Key.RoleId,
                        RoleName = tg.Key.RoleName,
                        TotalTickets = tTot,
                        ResolvedTickets = tRes,
                        UnresolvedTickets = tTot - tRes,
                        ResolutionRate = tTot > 0 ? Math.Round((double)tRes / tTot * 100, 1) : 0
                    });
                }
                detail.TeamBreakdown = detail.TeamBreakdown.OrderByDescending(t => t.TotalTickets).ToList();

                // User Breakdown (who handled them)
                var userGroups = tickets.Where(t => t.AssignedTo.HasValue).GroupBy(t => new { UserId = t.AssignedTo!.Value, t.AssigneeName, t.RoleName, t.LocationName });
                foreach (var ug in userGroups)
                {
                    int uTot = ug.Count();
                    int uRes = ug.Count(t => t.Status == "Closed" || t.Status == "Resolved");
                    detail.UserBreakdown.Add(new UserStatDto
                    {
                        UserId = ug.Key.UserId,
                        UserName = ug.Key.AssigneeName,
                        RoleName = ug.Key.RoleName,
                        LocationName = ug.Key.LocationName,
                        TotalTickets = uTot,
                        ResolvedTickets = uRes,
                        UnresolvedTickets = uTot - uRes
                    });
                }
                detail.UserBreakdown = detail.UserBreakdown.OrderByDescending(u => u.TotalTickets).ToList();

                // Location Breakdown
                var locGroups = tickets.GroupBy(t => new { t.LocationId, t.LocationName, t.LocationCode });
                foreach (var lg in locGroups)
                {
                    int lTot = lg.Count();
                    int lRes = lg.Count(t => t.Status == "Closed" || t.Status == "Resolved");
                    detail.LocationBreakdown.Add(new LocationStatDto
                    {
                        LocationId = lg.Key.LocationId,
                        LocationName = lg.Key.LocationName,
                        LocationCode = lg.Key.LocationCode,
                        TotalTickets = lTot,
                        ResolvedTickets = lRes,
                        UnresolvedTickets = lTot - lRes
                    });
                }
                detail.LocationBreakdown = detail.LocationBreakdown.OrderByDescending(l => l.TotalTickets).ToList();

                // Priority Breakdown
                detail.PriorityBreakdown = tickets
                    .GroupBy(t => !string.IsNullOrEmpty(t.Priority) ? t.Priority : "Medium")
                    .ToDictionary(g => g.Key, g => g.Count());

                // Ticket list
                detail.Tickets = tickets.Select(t => new TicketSimpleDto
                {
                    TicketId = t.TicketId,
                    TicketNumber = t.TicketNumber,
                    Subject = t.Subject,
                    Status = t.Status,
                    Priority = t.Priority,
                    AssigneeName = t.AssigneeName,
                    ModuleName = t.ModuleName,
                    CreatedOn = t.CreatedOn,
                    ClosedOn = t.ClosedOn,
                    Rating = t.Rating
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching Customer Detail for CustomerId {CustomerId}", customerId);
                throw;
            }

            return detail;
        }

        // =====================================================================
        // 4. USER DETAIL: When user clicks a User
        // =====================================================================
        public async Task<UserPerformanceDetailDto?> GetUserDetailAsync(int userId)
        {
            var cjdDb = GetCjDarclDb();
            var crmDb = GetCrmDb();

            var detail = new UserPerformanceDetailDto();

            try
            {
                // User info
                string sqlUser = $@"
                    SELECT 
                        u.UserId,
                        COALESCE(u.Name, u.UserName) AS Name,
                        COALESCE(u.UserName, '') AS UserName,
                        COALESCE(u.EmailId, '') AS Email,
                        COALESCE(u.MobileNo, '') AS MobileNo,
                        COALESCE(r.RoleName, 'Assignee') AS RoleName,
                        COALESCE(loc.LocationId, 1) AS LocationId,
                        COALESCE(loc.LocationName, 'CENTRAL CORPORATE OFFICE') AS LocationName,
                        COALESCE(loc.LocationCode, 'CCO') AS LocationCode,
                        u.IsActive
                    FROM [{cjdDb}].dbo.CL_Master_User u
                    LEFT JOIN [{cjdDb}].dbo.CL_Master_Role r ON u.RoleId = r.RoleId
                    LEFT JOIN [{cjdDb}].dbo.CL_Master_Location loc ON u.LocationId = loc.LocationId
                    WHERE u.UserId = @UserId;
                ";
                var usr = await _crmConnection.QueryFirstOrDefaultAsync<UserBasicDto>(sqlUser, new { UserId = userId });
                if (usr == null) return null;
                detail.User = usr;

                // Tickets assigned to this user
                string sqlTkts = $@"
                    SELECT 
                        t.TicketId,
                        COALESCE(t.TicketNumber, 'TKT-' + CAST(t.TicketId AS VARCHAR)) AS TicketNumber,
                        COALESCE(t.Subject, 'No Subject') AS Subject,
                        COALESCE(t.Status, 'Open') AS Status,
                        COALESCE(t.Priority, 'Medium') AS Priority,
                        t.Rating,
                        t.CreatedOn,
                        t.ClosedOn,
                        COALESCE(t.ModuleId, 0) AS ModuleId,
                        COALESCE(m.Name, 'General Support') AS ModuleName,
                        t.CustomerId,
                        COALESCE(c.CustomerName, 'Customer #' + CAST(COALESCE(t.CustomerId, 0) AS VARCHAR)) AS CustomerName,
                        COALESCE(c.CustomerCode, '') AS CustomerCode
                    FROM [{crmDb}].dbo.Tickets t
                    LEFT JOIN [{crmDb}].dbo.Modules m ON t.ModuleId = m.Id
                    LEFT JOIN [{cjdDb}].dbo.CL_Master_Customer c ON t.CustomerId = c.CustomerId
                    WHERE t.AssignedTo = @UserId
                    ORDER BY t.CreatedOn DESC;
                ";
                var tickets = (await _crmConnection.QueryAsync<UserTicketRow>(sqlTkts, new { UserId = userId })).ToList();

                int total = tickets.Count;
                int resolved = tickets.Count(t => t.Status == "Closed" || t.Status == "Resolved");
                int unresolved = total - resolved;
                int inProgress = tickets.Count(t => t.Status == "In Progress" || t.Status == "Rework");

                detail.Kpi = new PerformanceKpiDto
                {
                    TotalTickets = total,
                    ResolvedTickets = resolved,
                    UnresolvedTickets = unresolved,
                    InProgressTickets = inProgress,
                    ResolutionRate = total > 0 ? Math.Round((double)resolved / total * 100, 1) : 0
                };

                // Department breakdown
                var deptGroups = tickets.GroupBy(t => new { t.ModuleId, t.ModuleName });
                foreach (var g in deptGroups)
                {
                    int dTot = g.Count();
                    int dRes = g.Count(t => t.Status == "Closed" || t.Status == "Resolved");
                    detail.DepartmentBreakdown.Add(new DepartmentStatDto
                    {
                        ModuleId = g.Key.ModuleId,
                        ModuleName = g.Key.ModuleName,
                        TotalTickets = dTot,
                        ResolvedTickets = dRes,
                        UnresolvedTickets = dTot - dRes,
                        ResolutionRate = dTot > 0 ? Math.Round((double)dRes / dTot * 100, 1) : 0
                    });
                }
                detail.DepartmentBreakdown = detail.DepartmentBreakdown.OrderByDescending(d => d.TotalTickets).ToList();

                // Customer breakdown
                var custGroups = tickets.Where(t => t.CustomerId.HasValue).GroupBy(t => new { CustomerId = t.CustomerId!.Value, t.CustomerName, t.CustomerCode });
                foreach (var cg in custGroups)
                {
                    int cTot = cg.Count();
                    int cRes = cg.Count(t => t.Status == "Closed" || t.Status == "Resolved");
                    detail.CustomerBreakdown.Add(new CustomerStatDto
                    {
                        CustomerId = cg.Key.CustomerId,
                        CustomerName = cg.Key.CustomerName,
                        CustomerCode = cg.Key.CustomerCode,
                        TotalTickets = cTot,
                        ResolvedTickets = cRes,
                        UnresolvedTickets = cTot - cRes,
                        ResolutionRate = cTot > 0 ? Math.Round((double)cRes / cTot * 100, 1) : 0
                    });
                }
                detail.CustomerBreakdown = detail.CustomerBreakdown.OrderByDescending(c => c.TotalTickets).ToList();

                // Monthly Trend
                var monthGroups = tickets
                    .Where(t => t.CreatedOn != null)
                    .GroupBy(t => new { Year = t.CreatedOn!.Value.Year, Month = t.CreatedOn!.Value.Month })
                    .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month);

                foreach (var mg in monthGroups)
                {
                    var dt = new DateTime(mg.Key.Year, mg.Key.Month, 1);
                    detail.MonthlyTrend.Add(new MonthlyTrendDto
                    {
                        Year = mg.Key.Year,
                        Month = mg.Key.Month,
                        Label = dt.ToString("MMM yyyy"),
                        TotalTickets = mg.Count(),
                        ResolvedTickets = mg.Count(t => t.Status == "Closed" || t.Status == "Resolved")
                    });
                }

                // Tickets
                detail.Tickets = tickets.Select(t => new TicketSimpleDto
                {
                    TicketId = t.TicketId,
                    TicketNumber = t.TicketNumber,
                    Subject = t.Subject,
                    Status = t.Status,
                    Priority = t.Priority,
                    CustomerName = t.CustomerName,
                    ModuleName = t.ModuleName,
                    CreatedOn = t.CreatedOn,
                    ClosedOn = t.ClosedOn,
                    Rating = t.Rating
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching User Detail for UserId {UserId}", userId);
                throw;
            }

            return detail;
        }

        // =====================================================================
        // 5. TEAM DETAIL: Drill-down into a Team's reporting hierarchy & stats
        // =====================================================================
        public async Task<TeamPerformanceDetailDto?> GetTeamDetailAsync(int managerId)
        {
            var cjdDb = GetCjDarclDb();
            var crmDb = GetCrmDb();

            var detail = new TeamPerformanceDetailDto();

            try
            {
                // 1. Manager info
                string sqlManager = $@"
                    SELECT 
                        u.UserId,
                        COALESCE(u.Name, u.UserName) AS Name,
                        COALESCE(u.UserName, '') AS UserName,
                        COALESCE(u.EmailId, '') AS Email,
                        COALESCE(u.MobileNo, '') AS MobileNo,
                        COALESCE(r.RoleName, 'Manager') AS RoleName,
                        COALESCE(loc.LocationId, 1) AS LocationId,
                        COALESCE(loc.LocationName, 'CENTRAL CORPORATE OFFICE') AS LocationName,
                        COALESCE(loc.LocationCode, 'CCO') AS LocationCode,
                        u.IsActive
                    FROM [{cjdDb}].dbo.CL_Master_User u
                    LEFT JOIN [{cjdDb}].dbo.CL_Master_Role r ON u.RoleId = r.RoleId
                    LEFT JOIN [{cjdDb}].dbo.CL_Master_Location loc ON u.LocationId = loc.LocationId
                    WHERE u.UserId = @ManagerId;
                ";
                var manager = await _crmConnection.QueryFirstOrDefaultAsync<UserBasicDto>(sqlManager, new { ManagerId = managerId });
                if (manager == null) return null;
                detail.Manager = manager;

                // 2. Direct reports / team members
                string sqlMembers = $@"
                    SELECT 
                        u.UserId,
                        COALESCE(u.Name, u.UserName) AS Name,
                        COALESCE(u.UserName, '') AS UserName,
                        COALESCE(r.RoleName, 'Staff') AS RoleName,
                        COALESCE(loc.LocationId, 1) AS LocationId,
                        COALESCE(loc.LocationName, 'HQ') AS LocationName,
                        COALESCE(loc.LocationCode, 'HQ') AS LocationCode,
                        COALESCE(u.EmailId, '') AS Email,
                        COALESCE(u.MobileNo, '') AS MobileNo,
                        CASE WHEN u.UserId = @ManagerId THEN 1 ELSE 0 END AS IsManager
                    FROM [{cjdDb}].dbo.CL_Master_User u
                    LEFT JOIN [{cjdDb}].dbo.CL_Master_Role r ON u.RoleId = r.RoleId
                    LEFT JOIN [{cjdDb}].dbo.CL_Master_Location loc ON u.LocationId = loc.LocationId
                    WHERE u.ManagerId = @ManagerId OR u.UserId = @ManagerId
                    ORDER BY (CASE WHEN u.UserId = @ManagerId THEN 0 ELSE 1 END), u.Name;
                ";
                var members = (await _crmConnection.QueryAsync<TeamMemberUserDto>(sqlMembers, new { ManagerId = managerId })).ToList();

                // 3. Team tickets (assigned to manager or any direct report)
                string sqlTickets = $@"
                    SELECT 
                        t.TicketId,
                        COALESCE(t.TicketNumber, 'TKT-' + CAST(t.TicketId AS VARCHAR)) AS TicketNumber,
                        COALESCE(t.Subject, 'No Subject') AS Subject,
                        COALESCE(t.Status, 'Open') AS Status,
                        COALESCE(t.Priority, 'Medium') AS Priority,
                        t.Rating,
                        t.CreatedOn,
                        t.ClosedOn,
                        t.AssignedTo,
                        COALESCE(u.Name, u.UserName, 'Unassigned') AS AssigneeName,
                        COALESCE(r.RoleName, 'Staff') AS AssigneeRole,
                        COALESCE(t.ModuleId, 0) AS ModuleId,
                        COALESCE(m.Name, 'General Support') AS ModuleName,
                        t.CustomerId,
                        COALESCE(c.CustomerName, 'Customer #' + CAST(COALESCE(t.CustomerId, 0) AS VARCHAR)) AS CustomerName,
                        COALESCE(c.CustomerCode, '') AS CustomerCode
                    FROM [{crmDb}].dbo.Tickets t
                    INNER JOIN [{cjdDb}].dbo.CL_Master_User u ON t.AssignedTo = u.UserId
                    LEFT JOIN [{cjdDb}].dbo.CL_Master_Role r ON u.RoleId = r.RoleId
                    LEFT JOIN [{crmDb}].dbo.Modules m ON t.ModuleId = m.Id
                    LEFT JOIN [{cjdDb}].dbo.CL_Master_Customer c ON t.CustomerId = c.CustomerId
                    WHERE u.ManagerId = @ManagerId OR u.UserId = @ManagerId
                    ORDER BY t.CreatedOn DESC;
                ";
                var tickets = (await _crmConnection.QueryAsync<TeamTicketRow>(sqlTickets, new { ManagerId = managerId })).ToList();

                // 4. Calculate member-level stats ("Who solved how many tickets")
                foreach (var member in members)
                {
                    var mTickets = tickets.Where(t => t.AssignedTo == member.UserId).ToList();
                    member.TotalAssigned = mTickets.Count;
                    member.Resolved = mTickets.Count(t => t.Status == "Closed" || t.Status == "Resolved");
                    member.Unresolved = member.TotalAssigned - member.Resolved;
                    member.ResolutionRate = member.TotalAssigned > 0 
                        ? Math.Round((double)member.Resolved / member.TotalAssigned * 100, 1) 
                        : 0;

                    var closedDurations = mTickets
                        .Where(t => t.ClosedOn.HasValue && t.CreatedOn.HasValue)
                        .Select(t => (t.ClosedOn!.Value - t.CreatedOn!.Value).TotalHours)
                        .ToList();
                    member.AvgResolutionHours = closedDurations.Any() ? Math.Round(closedDurations.Average(), 1) : 0;
                }
                detail.Members = members;

                // 5. Team KPI summary
                int totalTickets = tickets.Count;
                int resolvedTickets = tickets.Count(t => t.Status == "Closed" || t.Status == "Resolved");
                int unresolvedTickets = totalTickets - resolvedTickets;
                int inProgressTickets = tickets.Count(t => t.Status == "In Progress" || t.Status == "Rework");

                var allClosedDurations = tickets
                    .Where(t => t.ClosedOn.HasValue && t.CreatedOn.HasValue)
                    .Select(t => (t.ClosedOn!.Value - t.CreatedOn!.Value).TotalHours)
                    .ToList();
                double avgHours = allClosedDurations.Any() ? Math.Round(allClosedDurations.Average(), 1) : 0;

                detail.Kpi = new PerformanceKpiDto
                {
                    TotalTickets = totalTickets,
                    ResolvedTickets = resolvedTickets,
                    UnresolvedTickets = unresolvedTickets,
                    InProgressTickets = inProgressTickets,
                    ResolutionRate = totalTickets > 0 ? Math.Round((double)resolvedTickets / totalTickets * 100, 1) : 0,
                    AvgResolutionHours = avgHours
                };

                detail.Team = new TeamStatDto
                {
                    RoleId = manager.UserId,
                    RoleName = manager.Name + " Team",
                    ManagerName = manager.Name,
                    ManagerRole = manager.RoleName,
                    UserCount = members.Count(m => !m.IsManager),
                    TotalTickets = totalTickets,
                    ResolvedTickets = resolvedTickets,
                    UnresolvedTickets = unresolvedTickets,
                    ResolutionRate = detail.Kpi.ResolutionRate
                };

                // 6. Department / Module breakdown
                var deptGroups = tickets.GroupBy(t => new { t.ModuleId, t.ModuleName });
                foreach (var g in deptGroups)
                {
                    int dTot = g.Count();
                    int dRes = g.Count(t => t.Status == "Closed" || t.Status == "Resolved");
                    detail.DepartmentBreakdown.Add(new DepartmentStatDto
                    {
                        ModuleId = g.Key.ModuleId,
                        ModuleName = g.Key.ModuleName,
                        TotalTickets = dTot,
                        ResolvedTickets = dRes,
                        UnresolvedTickets = dTot - dRes,
                        ResolutionRate = dTot > 0 ? Math.Round((double)dRes / dTot * 100, 1) : 0
                    });
                }
                detail.DepartmentBreakdown = detail.DepartmentBreakdown.OrderByDescending(d => d.TotalTickets).ToList();

                // 7. Customer breakdown
                var custGroups = tickets.Where(t => t.CustomerId.HasValue).GroupBy(t => new { CustomerId = t.CustomerId!.Value, t.CustomerName, t.CustomerCode });
                foreach (var cg in custGroups)
                {
                    int cTot = cg.Count();
                    int cRes = cg.Count(t => t.Status == "Closed" || t.Status == "Resolved");
                    detail.CustomerBreakdown.Add(new CustomerStatDto
                    {
                        CustomerId = cg.Key.CustomerId,
                        CustomerName = cg.Key.CustomerName,
                        CustomerCode = cg.Key.CustomerCode,
                        TotalTickets = cTot,
                        ResolvedTickets = cRes,
                        UnresolvedTickets = cTot - cRes,
                        ResolutionRate = cTot > 0 ? Math.Round((double)cRes / cTot * 100, 1) : 0
                    });
                }
                detail.CustomerBreakdown = detail.CustomerBreakdown.OrderByDescending(c => c.TotalTickets).ToList();

                // 8. Tickets list
                detail.Tickets = tickets.Select(t => new TicketSimpleDto
                {
                    TicketId = t.TicketId,
                    TicketNumber = t.TicketNumber,
                    Subject = t.Subject,
                    Status = t.Status,
                    Priority = t.Priority,
                    AssigneeName = t.AssigneeName,
                    CustomerName = t.CustomerName,
                    ModuleName = t.ModuleName,
                    CreatedOn = t.CreatedOn,
                    ClosedOn = t.ClosedOn,
                    Rating = t.Rating
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching Team Detail for ManagerId {ManagerId}", managerId);
                throw;
            }

            return detail;
        }
    }
}
