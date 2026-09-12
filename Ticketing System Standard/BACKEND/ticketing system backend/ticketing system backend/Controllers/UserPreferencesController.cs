using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace ticketing_system_backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserPreferencesController : ControllerBase
    {
        private readonly IDbConnection _connection;

        public UserPreferencesController(IDbConnection connection)
        {
            _connection = connection;
        }

        public class PermissionOverride
        {
            public string PermissionKey { get; set; } = string.Empty;
            public bool IsEnabled { get; set; }
        }

        public class UserPreferencesRequest
        {
            public int UserId { get; set; }
            public string UserRole { get; set; } = string.Empty;
            public string DetailViewType { get; set; } = "Default";
            public List<PermissionOverride> Permissions { get; set; } = new();
        }

        [HttpGet("{userId}/{userRole}")]
        public async Task<IActionResult> GetUserPreferences(int userId, string userRole)
        {
            var detailViewType = await _connection.QueryFirstOrDefaultAsync<string>(
                "SELECT DetailViewType FROM dbo.UserPreferences WHERE UserId = @UserId AND UserRole = @UserRole",
                new { UserId = userId, UserRole = userRole }) ?? "Default";

            var overrides = await _connection.QueryAsync<PermissionOverride>(
                "SELECT PermissionKey, IsEnabled FROM dbo.UserPermissions WHERE UserId = @UserId AND UserRole = @UserRole",
                new { UserId = userId, UserRole = userRole });

            return Ok(new
            {
                userId = userId,
                userRole = userRole,
                detailViewType = detailViewType,
                permissions = overrides
            });
        }

        [HttpPost]
        public async Task<IActionResult> SaveUserPreferences([FromBody] UserPreferencesRequest request)
        {
            // 1. Save detail view type
            var affected = await _connection.ExecuteAsync(@"
                MERGE dbo.UserPreferences AS target
                USING (SELECT @UserId AS UserId, @UserRole AS UserRole) AS source
                ON (target.UserId = source.UserId AND target.UserRole = source.UserRole)
                WHEN MATCHED THEN
                    UPDATE SET target.DetailViewType = @DetailViewType
                WHEN NOT MATCHED THEN
                    INSERT (UserId, UserRole, DetailViewType)
                    VALUES (@UserId, @UserRole, @DetailViewType);",
                new { request.UserId, request.UserRole, request.DetailViewType });

            // 2. Save permission overrides
            if (request.Permissions != null)
            {
                foreach (var p in request.Permissions)
                {
                    await _connection.ExecuteAsync(@"
                        MERGE dbo.UserPermissions AS target
                        USING (SELECT @UserId AS UserId, @UserRole AS UserRole, @PermissionKey AS PermissionKey) AS source
                        ON (target.UserId = source.UserId AND target.UserRole = source.UserRole AND target.PermissionKey = source.PermissionKey)
                        WHEN MATCHED THEN
                            UPDATE SET target.IsEnabled = @IsEnabled
                        WHEN NOT MATCHED THEN
                            INSERT (UserId, UserRole, PermissionKey, IsEnabled)
                            VALUES (@UserId, @UserRole, @PermissionKey, @IsEnabled);",
                        new { UserId = request.UserId, UserRole = request.UserRole, PermissionKey = p.PermissionKey, IsEnabled = p.IsEnabled });
                }
            }

            return Ok(new { message = "User preferences saved successfully." });
        }
    }
}
