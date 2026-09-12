using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ticketing_system_backend.Interface;
using ticketing_system_backend.Models;

namespace ticketing_system_backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PerformanceDashboardController : ControllerBase
    {
        private readonly IPerformanceAnalyticsRepository _analyticsRepo;
        private readonly ILogger<PerformanceDashboardController> _logger;

        public PerformanceDashboardController(
            IPerformanceAnalyticsRepository analyticsRepo,
            ILogger<PerformanceDashboardController> logger)
        {
            _analyticsRepo = analyticsRepo;
            _logger = logger;
        }

        /// <summary>
        /// Global Performance Overview: KPIs, all locations, departments, customers, teams, top users.
        /// </summary>
        [HttpGet("overview")]
        public async Task<IActionResult> GetOverview([FromQuery] PerformanceFilterParams? filters)
        {
            try
            {
                var data = await _analyticsRepo.GetPerformanceOverviewAsync(filters);
                return Ok(data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting performance overview");
                return StatusCode(500, new { message = "Failed to load performance overview", error = ex.Message });
            }
        }

        /// <summary>
        /// Location Drill-down: Per team, per department, per user, customer breakdown, recent tickets for this location.
        /// </summary>
        [HttpGet("location/{locationId}")]
        public async Task<IActionResult> GetLocationDetail(int locationId)
        {
            try
            {
                var data = await _analyticsRepo.GetLocationDetailAsync(locationId);
                if (data == null)
                    return NotFound(new { message = $"Location with ID {locationId} not found" });

                return Ok(data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting location detail for {LocationId}", locationId);
                return StatusCode(500, new { message = "Failed to load location details", error = ex.Message });
            }
        }

        /// <summary>
        /// Customer Drill-down: Which departments raised against, which teams, which users, locations, priorities, tickets list.
        /// </summary>
        [HttpGet("customer/{customerId}")]
        public async Task<IActionResult> GetCustomerDetail(int customerId)
        {
            try
            {
                var data = await _analyticsRepo.GetCustomerDetailAsync(customerId);
                if (data == null)
                    return NotFound(new { message = $"Customer with ID {customerId} not found" });

                return Ok(data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting customer detail for {CustomerId}", customerId);
                return StatusCode(500, new { message = "Failed to load customer details", error = ex.Message });
            }
        }

        /// <summary>
        /// User Drill-down: Full performance profile, departments, customers, monthly trend, tickets list.
        /// </summary>
        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetUserDetail(int userId)
        {
            try
            {
                var data = await _analyticsRepo.GetUserDetailAsync(userId);
                if (data == null)
                    return NotFound(new { message = $"User with ID {userId} not found" });

                return Ok(data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user detail for {UserId}", userId);
                return StatusCode(500, new { message = "Failed to load user details", error = ex.Message });
            }
        }

        /// <summary>
        /// Team Drill-down: Reporting hierarchy, members list, who solved how many tickets, department and customer breakdown, tickets.
        /// </summary>
        [HttpGet("team/{managerId:int}")]
        public async Task<IActionResult> GetTeamDetail(int managerId)
        {
            try
            {
                var data = await _analyticsRepo.GetTeamDetailAsync(managerId);
                if (data == null)
                    return NotFound(new { message = $"Team with Manager ID {managerId} not found" });

                return Ok(data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting team detail for Manager {ManagerId}", managerId);
                return StatusCode(500, new { message = "Failed to load team details", error = ex.Message });
            }
        }
    }
}
