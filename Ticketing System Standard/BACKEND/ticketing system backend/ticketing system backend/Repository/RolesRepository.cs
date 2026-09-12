using Dapper;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using ticketing_system_backend.Interface;
using ticketing_system_backend.Models;

namespace ticketing_system_backend.Repository
{
    public class RolesRepository : IRolesRepository
    {
        private readonly IDbConnection _connection;

        public RolesRepository(IDbConnection connection)
        {
            _connection = connection;
        }

        public async Task<IEnumerable<SystemPermission>> GetPermissionsAsync()
        {
            return await _connection.QueryAsync<SystemPermission>("SELECT * FROM dbo.SystemPermissions");
        }

        public async Task<IEnumerable<SystemRole>> GetRolesAsync()
        {
            var sql = @"
                SELECT r.Id, r.RoleName, r.DashboardRoute AS DefaultDashboardRoute, r.IsActive,
                       p.Id, p.PermissionKey, p.ModuleName, p.Description
                FROM dbo.SystemRoles r
                LEFT JOIN dbo.RolePermissions rp ON r.Id = rp.RoleId
                LEFT JOIN dbo.SystemPermissions p ON rp.PermissionId = p.Id
            ";

            var roleDict = new Dictionary<int, SystemRole>();

            await _connection.QueryAsync<SystemRole, SystemPermission, SystemRole>(
                sql,
                (role, permission) =>
                {
                    if (!roleDict.TryGetValue(role.Id, out var currentRole))
                    {
                        currentRole = role;
                        currentRole.Permissions = new List<SystemPermission>();
                        roleDict.Add(currentRole.Id, currentRole);
                    }
                    if (permission != null && permission.Id > 0)
                    {
                        currentRole.Permissions.Add(permission);
                    }
                    return currentRole;
                },
                splitOn: "Id"
            );

            return roleDict.Values;
        }

        public async Task<SystemRole> GetRoleByIdAsync(int id)
        {
            var sql = @"
                SELECT r.Id, r.RoleName, r.DashboardRoute AS DefaultDashboardRoute, r.IsActive,
                       p.Id, p.PermissionKey, p.ModuleName, p.Description
                FROM dbo.SystemRoles r
                LEFT JOIN dbo.RolePermissions rp ON r.Id = rp.RoleId
                LEFT JOIN dbo.SystemPermissions p ON rp.PermissionId = p.Id
                WHERE r.Id = @Id
            ";

            SystemRole resultRole = null;

            await _connection.QueryAsync<SystemRole, SystemPermission, SystemRole>(
                sql,
                (role, permission) =>
                {
                    if (resultRole == null)
                    {
                        resultRole = role;
                        resultRole.Permissions = new List<SystemPermission>();
                    }
                    if (permission != null && permission.Id > 0)
                    {
                        resultRole.Permissions.Add(permission);
                    }
                    return resultRole;
                },
                new { Id = id },
                splitOn: "Id"
            );

            return resultRole;
        }

        public async Task<int> CreateRoleAsync(CreateRoleRequest request)
        {
            if (_connection.State != ConnectionState.Open) _connection.Open();
            using var transaction = _connection.BeginTransaction();
            try
            {
                var insertRoleSql = @"
                    INSERT INTO dbo.SystemRoles (RoleName, DashboardRoute, IsActive)
                    OUTPUT INSERTED.Id
                    VALUES (@RoleName, @DashboardRoute, 1);
                ";
                var roleId = await _connection.ExecuteScalarAsync<int>(insertRoleSql, new
                {
                    request.RoleName,
                    DashboardRoute = request.DefaultDashboardRoute ?? "/dashboard"
                }, transaction);

                if (request.PermissionIds != null && request.PermissionIds.Any())
                {
                    var insertPermsSql = "INSERT INTO dbo.RolePermissions (RoleId, PermissionId) VALUES (@RoleId, @PermissionId)";
                    var permParams = request.PermissionIds.Select(p => new { RoleId = roleId, PermissionId = p }).ToList();
                    await _connection.ExecuteAsync(insertPermsSql, permParams, transaction);
                }

                transaction.Commit();
                return roleId;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public async Task UpdateRoleAsync(int id, UpdateRoleRequest request)
        {
            if (_connection.State != ConnectionState.Open) _connection.Open();
            using var transaction = _connection.BeginTransaction();
            try
            {
                var updateRoleSql = @"
                    UPDATE dbo.SystemRoles 
                    SET RoleName = @RoleName, 
                        DashboardRoute = @DashboardRoute,
                        IsActive = @IsActive
                    WHERE Id = @Id;
                ";
                await _connection.ExecuteAsync(updateRoleSql, new
                {
                    request.RoleName,
                    DashboardRoute = request.DefaultDashboardRoute ?? "/dashboard",
                    request.IsActive,
                    Id = id
                }, transaction);

                // Re-sync permissions
                await _connection.ExecuteAsync("DELETE FROM dbo.RolePermissions WHERE RoleId = @Id", new { Id = id }, transaction);
                
                if (request.PermissionIds != null && request.PermissionIds.Any())
                {
                    var insertPermsSql = "INSERT INTO dbo.RolePermissions (RoleId, PermissionId) VALUES (@RoleId, @PermissionId)";
                    var permParams = request.PermissionIds.Select(p => new { RoleId = id, PermissionId = p }).ToList();
                    await _connection.ExecuteAsync(insertPermsSql, permParams, transaction);
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public async Task DeleteRoleAsync(int id)
        {
            if (_connection.State != ConnectionState.Open) _connection.Open();
            using var transaction = _connection.BeginTransaction();
            try
            {
                await _connection.ExecuteAsync("DELETE FROM dbo.RolePermissions WHERE RoleId = @Id", new { Id = id }, transaction);
                await _connection.ExecuteAsync("DELETE FROM dbo.SystemRoles WHERE Id = @Id", new { Id = id }, transaction);
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public async Task<IEnumerable<StatusWorkflow>> GetStatusWorkflowsAsync()
        {
            var dtos = await _connection.QueryAsync<dynamic>("SELECT * FROM dbo.StatusWorkflows");
            return dtos.Select(d => new StatusWorkflow
            {
                Id = (int)d.Id,
                RoleName = (string)d.RoleName,
                CurrentStatus = (string)d.CurrentStatus,
                NextStatuses = string.IsNullOrEmpty((string)d.NextStatuses) ? new List<string>() : System.Text.Json.JsonSerializer.Deserialize<List<string>>((string)d.NextStatuses),
                Condition = (string)d.Condition,
                IsActive = (bool)d.IsActive,
                IsBlocked = (bool)d.IsBlocked,
                SendEmail = d.SendEmail != null && (bool)d.SendEmail
            });
        }

        public async Task<int> CreateStatusWorkflowAsync(StatusWorkflow workflow)
        {
            var sql = @"
                INSERT INTO dbo.StatusWorkflows (RoleName, CurrentStatus, NextStatuses, Condition, IsActive, IsBlocked, SendEmail)
                OUTPUT INSERTED.Id
                VALUES (@RoleName, @CurrentStatus, @NextStatusesJson, @Condition, @IsActive, @IsBlocked, @SendEmail);
            ";
            var nextStatusesJson = System.Text.Json.JsonSerializer.Serialize(workflow.NextStatuses ?? new List<string>());
            return await _connection.ExecuteScalarAsync<int>(sql, new
            {
                workflow.RoleName,
                workflow.CurrentStatus,
                NextStatusesJson = nextStatusesJson,
                workflow.Condition,
                workflow.IsActive,
                workflow.IsBlocked,
                workflow.SendEmail
            });
        }

        public async Task DeleteStatusWorkflowAsync(int id)
        {
            await _connection.ExecuteAsync("DELETE FROM dbo.StatusWorkflows WHERE Id = @Id", new { Id = id });
        }

        public async Task UpdateStatusWorkflowAsync(int id, StatusWorkflow workflow)
        {
            var nextStatusesJson = System.Text.Json.JsonSerializer.Serialize(workflow.NextStatuses ?? new List<string>());
            var sql = @"
                UPDATE dbo.StatusWorkflows
                SET RoleName = @RoleName,
                    CurrentStatus = @CurrentStatus,
                    NextStatuses = @NextStatusesJson,
                    Condition = @Condition,
                    IsActive = @IsActive,
                    IsBlocked = @IsBlocked,
                    SendEmail = @SendEmail
                WHERE Id = @Id;
            ";
            await _connection.ExecuteAsync(sql, new
            {
                Id = id,
                workflow.RoleName,
                workflow.CurrentStatus,
                NextStatusesJson = nextStatusesJson,
                workflow.Condition,
                workflow.IsActive,
                workflow.IsBlocked,
                workflow.SendEmail
            });
        }

        public async Task<IEnumerable<AssignmentWorkflow>> GetAssignmentWorkflowsAsync()
        {
            return await _connection.QueryAsync<AssignmentWorkflow>("SELECT * FROM dbo.AssignmentWorkflows");
        }

        public async Task<int> CreateAssignmentWorkflowAsync(AssignmentWorkflow workflow)
        {
            var sql = @"
                INSERT INTO dbo.AssignmentWorkflows (AssignerRole, AssignableToRole)
                OUTPUT INSERTED.Id
                VALUES (@AssignerRole, @AssignableToRole);
            ";
            return await _connection.ExecuteScalarAsync<int>(sql, workflow);
        }

        public async Task DeleteAssignmentWorkflowAsync(int id)
        {
            await _connection.ExecuteAsync("DELETE FROM dbo.AssignmentWorkflows WHERE Id = @Id", new { Id = id });
        }

        public async Task UpdateAssignmentWorkflowAsync(int id, AssignmentWorkflow workflow)
        {
            var sql = @"
                UPDATE dbo.AssignmentWorkflows
                SET AssignerRole = @AssignerRole,
                    AssignableToRole = @AssignableToRole
                WHERE Id = @Id;
            ";
            await _connection.ExecuteAsync(sql, new { 
                Id = id, 
                workflow.AssignerRole, 
                workflow.AssignableToRole 
            });
        }
    }
}
