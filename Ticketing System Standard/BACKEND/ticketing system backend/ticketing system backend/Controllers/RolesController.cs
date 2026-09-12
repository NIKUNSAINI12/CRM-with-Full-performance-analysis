using System;
using System.Collections.Generic;
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
    [Authorize(Roles = "SuperManager,Super Admin,Manager,PM,Project Manager,HOD")] // Restricted as requested by user, allowed read access to other staff roles
    public class RolesController : ControllerBase
    {
        private readonly IRolesRepository _rolesRepository;
        private readonly ILogger<RolesController> _logger;

        public RolesController(IRolesRepository rolesRepository, ILogger<RolesController> logger)
        {
            _rolesRepository = rolesRepository;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetRoles()
        {
            try
            {
                var roles = await _rolesRepository.GetRolesAsync();
                return Ok(roles);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching roles.");
                return StatusCode(500, "Internal server error.");
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetRole(int id)
        {
            try
            {
                var role = await _rolesRepository.GetRoleByIdAsync(id);
                if (role == null) return NotFound();
                return Ok(role);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching role.");
                return StatusCode(500, "Internal server error.");
            }
        }

        [HttpPost]
        [Authorize(Roles = "SuperManager,Super Admin")]
        public async Task<IActionResult> CreateRole([FromBody] CreateRoleRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.RoleName))
                    return BadRequest("Role Name is required.");

                var roleId = await _rolesRepository.CreateRoleAsync(request);
                return CreatedAtAction(nameof(GetRole), new { id = roleId }, new { id = roleId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating role.");
                return StatusCode(500, "Internal server error.");
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "SuperManager,Super Admin")]
        public async Task<IActionResult> UpdateRole(int id, [FromBody] UpdateRoleRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.RoleName))
                    return BadRequest("Role Name is required.");

                await _rolesRepository.UpdateRoleAsync(id, request);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating role.");
                return StatusCode(500, "Internal server error.");
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "SuperManager,Super Admin")]
        public async Task<IActionResult> DeleteRole(int id)
        {
            try
            {
                // Optionally add logic to prevent deleting core roles
                if (id <= 6) // Assuming 1-6 are default system roles
                {
                    return BadRequest("Cannot delete core system roles.");
                }

                await _rolesRepository.DeleteRoleAsync(id);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting role.");
                return StatusCode(500, "Internal server error.");
            }
        }

        [HttpGet("permissions")]
        [AllowAnonymous] // Might be needed for dropdowns across UI, or we can keep it protected
        public async Task<IActionResult> GetPermissions()
        {
            try
            {
                var perms = await _rolesRepository.GetPermissionsAsync();
                return Ok(perms);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching permissions.");
                return StatusCode(500, "Internal server error.");
            }
        }

        [HttpGet("status-workflows")]
        [AllowAnonymous]
        public async Task<IActionResult> GetStatusWorkflows()
        {
            try
            {
                var workflows = await _rolesRepository.GetStatusWorkflowsAsync();
                return Ok(workflows);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching status workflows.");
                return StatusCode(500, "Internal server error.");
            }
        }

        [HttpPost("status-workflows")]
        [Authorize(Roles = "SuperManager,Super Admin")]
        public async Task<IActionResult> CreateStatusWorkflow([FromBody] StatusWorkflow workflow)
        {
            try
            {
                var id = await _rolesRepository.CreateStatusWorkflowAsync(workflow);
                return Ok(new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating status workflow.");
                return StatusCode(500, "Internal server error.");
            }
        }

        [HttpDelete("status-workflows/{id}")]
        [Authorize(Roles = "SuperManager,Super Admin")]
        public async Task<IActionResult> DeleteStatusWorkflow(int id)
        {
            try
            {
                await _rolesRepository.DeleteStatusWorkflowAsync(id);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting status workflow.");
                return StatusCode(500, "Internal server error.");
            }
        }

        [HttpPut("status-workflows/{id}")]
        [Authorize(Roles = "SuperManager,Super Admin")]
        public async Task<IActionResult> UpdateStatusWorkflow(int id, [FromBody] StatusWorkflow workflow)
        {
            try
            {
                await _rolesRepository.UpdateStatusWorkflowAsync(id, workflow);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating status workflow.");
                return StatusCode(500, "Internal server error.");
            }
        }

        [HttpGet("assignment-workflows")]
        public async Task<IActionResult> GetAssignmentWorkflows()
        {
            try
            {
                var workflows = await _rolesRepository.GetAssignmentWorkflowsAsync();
                return Ok(workflows);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching assignment workflows.");
                return StatusCode(500, "Internal server error.");
            }
        }

        [HttpPost("assignment-workflows")]
        [Authorize(Roles = "SuperManager,Super Admin")]
        public async Task<IActionResult> CreateAssignmentWorkflow([FromBody] AssignmentWorkflow workflow)
        {
            try
            {
                var id = await _rolesRepository.CreateAssignmentWorkflowAsync(workflow);
                return Ok(new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating assignment workflow.");
                return StatusCode(500, "Internal server error.");
            }
        }

        [HttpDelete("assignment-workflows/{id}")]
        [Authorize(Roles = "SuperManager,Super Admin")]
        public async Task<IActionResult> DeleteAssignmentWorkflow(int id)
        {
            try
            {
                await _rolesRepository.DeleteAssignmentWorkflowAsync(id);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting assignment workflow.");
                return StatusCode(500, "Internal server error.");
            }
        }

        [HttpPut("assignment-workflows/{id}")]
        [Authorize(Roles = "SuperManager,Super Admin")]
        public async Task<IActionResult> UpdateAssignmentWorkflow(int id, [FromBody] AssignmentWorkflow workflow)
        {
            try
            {
                await _rolesRepository.UpdateAssignmentWorkflowAsync(id, workflow);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating assignment workflow.");
                return StatusCode(500, "Internal server error.");
            }
        }
    }
}
