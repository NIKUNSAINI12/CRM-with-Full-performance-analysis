using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Dapper;

namespace ticketing_system_backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "SuperManager,Super Admin")] // Only Super Admins can manage roles
    public class RoleController : ControllerBase
    {
        private readonly string _connectionString;

        public RoleController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") ?? "";
        }

        public class RoleResponse
        {
            public int Id { get; set; }
            public string RoleName { get; set; } = "";
            public string DashboardRoute { get; set; } = "";
        }

        [HttpGet("roles")]
        public async Task<IActionResult> GetRoles()
        {
            using var connection = new SqlConnection(_connectionString);
            var roles = await connection.QueryAsync<RoleResponse>("SELECT Id, RoleName, DashboardRoute FROM SystemRoles");
            return Ok(roles);
        }

        public class PermissionResponse
        {
            public int Id { get; set; }
            public string PermissionKey { get; set; } = "";
            public string ModuleName { get; set; } = "";
            public string Description { get; set; } = "";
        }

        [HttpGet("permissions")]
        public async Task<IActionResult> GetPermissions()
        {
            using var connection = new SqlConnection(_connectionString);
            var permissions = await connection.QueryAsync<PermissionResponse>("SELECT Id, PermissionKey, ModuleName, Description FROM SystemPermissions");
            return Ok(permissions);
        }

        [HttpGet("roles/{roleId}/permissions")]
        public async Task<IActionResult> GetRolePermissions(int roleId)
        {
            using var connection = new SqlConnection(_connectionString);
            var permissions = await connection.QueryAsync<int>(
                "SELECT PermissionId FROM RolePermissions WHERE RoleId = @RoleId",
                new { RoleId = roleId });
            return Ok(permissions);
        }

        public class CreateRoleRequest
        {
            public string RoleName { get; set; } = "";
            public string DashboardRoute { get; set; } = "";
            public List<int> PermissionIds { get; set; } = new List<int>();
        }

        [HttpPost("roles")]
        public async Task<IActionResult> CreateRole([FromBody] CreateRoleRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.RoleName) || string.IsNullOrWhiteSpace(request.DashboardRoute))
                return BadRequest("RoleName and DashboardRoute are required.");

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                var roleId = await connection.QuerySingleAsync<int>(
                    "INSERT INTO SystemRoles (RoleName, DashboardRoute) OUTPUT INSERTED.Id VALUES (@RoleName, @DashboardRoute)",
                    new { request.RoleName, request.DashboardRoute },
                    transaction);

                if (request.PermissionIds != null && request.PermissionIds.Count > 0)
                {
                    foreach (var permId in request.PermissionIds)
                    {
                        await connection.ExecuteAsync(
                            "INSERT INTO RolePermissions (RoleId, PermissionId) VALUES (@RoleId, @PermissionId)",
                            new { RoleId = roleId, PermissionId = permId },
                            transaction);
                    }
                }

                transaction.Commit();
                return Ok(new { id = roleId });
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                return StatusCode(500, new { error = "Failed to create role", details = ex.Message });
            }
        }

        public class UpdateRoleRequest
        {
            public string RoleName { get; set; } = "";
            public string DashboardRoute { get; set; } = "";
            public List<int> PermissionIds { get; set; } = new List<int>();
        }

        [HttpPut("roles/{id}")]
        public async Task<IActionResult> UpdateRole(int id, [FromBody] UpdateRoleRequest request)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                await connection.ExecuteAsync(
                    "UPDATE SystemRoles SET RoleName = @RoleName, DashboardRoute = @DashboardRoute WHERE Id = @Id",
                    new { request.RoleName, request.DashboardRoute, Id = id },
                    transaction);

                await connection.ExecuteAsync(
                    "DELETE FROM RolePermissions WHERE RoleId = @Id",
                    new { Id = id },
                    transaction);

                if (request.PermissionIds != null && request.PermissionIds.Count > 0)
                {
                    foreach (var permId in request.PermissionIds)
                    {
                        await connection.ExecuteAsync(
                            "INSERT INTO RolePermissions (RoleId, PermissionId) VALUES (@RoleId, @PermissionId)",
                            new { RoleId = id, PermissionId = permId },
                            transaction);
                    }
                }

                transaction.Commit();
                return Ok(new { message = "Role updated successfully." });
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                return StatusCode(500, new { error = "Failed to update role", details = ex.Message });
            }
        }
    }
}
