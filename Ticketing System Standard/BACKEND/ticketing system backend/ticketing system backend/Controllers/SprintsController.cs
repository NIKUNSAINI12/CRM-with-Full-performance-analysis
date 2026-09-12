using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ticketing_system_backend.Models;

namespace ticketing_system_backend.Controllers
{
  [Authorize]
  [ApiController]
  [Route("api/[controller]")]
  public class SprintsController : ControllerBase
  {
    private readonly IDbConnection _connection;
    private readonly ILogger<SprintsController> _logger;

    public SprintsController(IDbConnection connection, ILogger<SprintsController> logger)
    {
      _connection = connection;
      _logger = logger;
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> GetSprints([FromQuery] int? pmId = null)
    {
      try
      {
        var sprints = await _connection.QueryAsync<Sprint>(
            "usp_GetSprints",
            new { PMId = pmId },
            commandType: CommandType.StoredProcedure
        );
        return Ok(sprints);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error fetching sprints");
        return StatusCode(500, "Internal server error");
      }
    }

    [HttpPost]
    public async Task<IActionResult> CreateSprint([FromBody] CreateSprintRequest request)
    {
      try
      {
        var pmIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        int createdBy = pmIdClaim != null ? int.Parse(pmIdClaim) : 0;

        var sprint = await _connection.QuerySingleAsync<Sprint>(
            "usp_CreateSprint",
            new
            {
              request.SprintName,
              request.StartDate,
              request.EndDate,
              request.Status,
              CreatedBy = createdBy
            },
            commandType: CommandType.StoredProcedure
        );
        return Created($"/api/sprints/{sprint.SprintId}", sprint);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error creating sprint");
        return StatusCode(500, "Internal server error");
      }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateSprint(int id, [FromBody] UpdateSprintRequest request)
    {
      try
      {
        var sprint = await _connection.QuerySingleOrDefaultAsync<Sprint>(
            "usp_UpdateSprint",
            new
            {
              SprintId = id,
              request.SprintName,
              request.StartDate,
              request.EndDate,
              request.Status
            },
            commandType: CommandType.StoredProcedure
        );
        if (sprint == null) return NotFound();
        return Ok(sprint);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error updating sprint {SprintId}", id);
        return StatusCode(500, "Internal server error");
      }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteSprint(int id)
    {
      try
      {
        await _connection.ExecuteAsync(
            "usp_DeleteSprint",
            new { SprintId = id },
            commandType: CommandType.StoredProcedure
        );
        return NoContent();
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error deleting sprint {SprintId}", id);
        return StatusCode(500, "Internal server error");
      }
    }

    [HttpPost("{id}/assign-tickets")]
    public async Task<IActionResult> AssignTickets(int id, [FromBody] AssignTicketsRequest request)
    {
      try
      {
        await _connection.ExecuteAsync(
            "usp_AssignTicketsToSprint",
            new { SprintId = id, request.TicketIds },
            commandType: CommandType.StoredProcedure
        );
        return Ok();
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error assigning tickets to sprint {SprintId}", id);
        return StatusCode(500, "Internal server error");
      }
    }

    [HttpPost("{id}/remove-tickets")]
    public async Task<IActionResult> RemoveTickets(int id, [FromBody] AssignTicketsRequest request)
    {
      try
      {
        await _connection.ExecuteAsync(
            "usp_AssignTicketsToSprint",
            new { SprintId = (int?)null, request.TicketIds },
            commandType: CommandType.StoredProcedure
        );
        return Ok();
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error removing tickets from sprint {SprintId}", id);
        return StatusCode(500, "Internal server error");
      }
    }

    [HttpPost("{id}/start")]
    public async Task<IActionResult> StartSprint(int id)
    {
      try
      {
        var sprint = await _connection.QuerySingleOrDefaultAsync<Sprint>(
            "usp_StartSprint",
            new { SprintId = id },
            commandType: CommandType.StoredProcedure
        );
        if (sprint == null) return NotFound();
        return Ok(sprint);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error starting sprint {SprintId}", id);
        return BadRequest(new { message = ex.Message });
      }
    }

    [HttpPost("{id}/complete")]
    public async Task<IActionResult> CompleteSprint(int id, [FromBody] CompleteSprintRequest request)
    {
      try
      {
        var sprint = await _connection.QuerySingleOrDefaultAsync<Sprint>(
            "usp_CompleteSprint",
            new { SprintId = id, DestinationSprintId = request?.DestinationSprintId },
            commandType: CommandType.StoredProcedure
        );
        if (sprint == null) return NotFound();
        return Ok(sprint);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error completing sprint {SprintId}", id);
        return BadRequest(new { message = ex.Message });
      }
    }

    [AllowAnonymous]
    [HttpGet("analytics")]
    public async Task<IActionResult> GetSprintAnalytics([FromQuery] int? pmId = null)
    {
      try
      {
        using var multi = await _connection.QueryMultipleAsync(
            "usp_GetSprintAnalytics",
            new { PMId = pmId },
            commandType: CommandType.StoredProcedure
        );

        var analytics = new SprintAnalyticsDto
        {
          Sprints = await multi.ReadAsync<SprintPerformanceDto>(),
          WeeklyCompletion = await multi.ReadAsync<WeeklyCompletionDto>(),
          MonthlyCompletion = await multi.ReadAsync<MonthlyCompletionDto>(),
          YearlyCompletion = await multi.ReadAsync<YearlyCompletionDto>()
        };

        return Ok(analytics);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error fetching sprint analytics");
        return StatusCode(500, "Internal server error");
      }
    }

    [HttpPost("update-ticket-order")]
    public async Task<IActionResult> UpdateTicketOrder([FromBody] UpdateTicketOrderRequest request)
    {
      try
      {
        if (request?.Orders == null || !request.Orders.Any())
        {
          return BadRequest("No orders provided.");
        }
        await _connection.ExecuteAsync(
            "UPDATE dbo.Tickets SET SprintOrder = @Order WHERE TicketId = @TicketId",
            request.Orders
        );
        return Ok();
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error updating ticket orders");
        return StatusCode(500, "Internal server error");
      }
    }
    [AllowAnonymous]
    [HttpGet("completed-history")]
    public async Task<IActionResult> GetCompletedSprintsHistory([FromQuery] int? pmId = null)
    {
      try
      {
        using var multi = await _connection.QueryMultipleAsync(
            "usp_GetCompletedSprintsHistory",
            new { PMId = pmId },
            commandType: CommandType.StoredProcedure
        );

        var sprints = (await multi.ReadAsync<Sprint>()).ToList();
        var tickets = (await multi.ReadAsync<SprintTicketHistoryDto>()).ToList();

        var historyList = sprints.Select(s => new CompletedSprintHistoryDto
        {
          Sprint = s,
          Tickets = tickets.Where(t => t.SprintId == s.SprintId).ToList()
        }).ToList();

        return Ok(historyList);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error fetching completed sprints history");
        return StatusCode(500, "Internal server error");
      }
    }
  }
}
