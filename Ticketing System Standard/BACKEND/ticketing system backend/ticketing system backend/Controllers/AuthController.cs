using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using ticketing_system_backend.Models;
using ticketing_system_backend.Services;
using ticketing_system_backend.Settings;
using ticketing_system_backend.Helpers;

namespace ticketing_system_backend.Controllers
{
  [ApiController]
  [Route("api/[controller]")]
  public class AuthController : ControllerBase
  {
      private readonly IDbConnection   _connection;
      private readonly IJwtTokenGenerator _tokenGen;
      private readonly ITokenService   _tokenService;
      private readonly JwtSettings     _jwtSettings;
      private readonly ILogger<AuthController> _logger;

      private const string RefreshTokenCookie = "X-Refresh-Token";

      public AuthController(
          IDbConnection connection,
          IJwtTokenGenerator tokenGen,
          ITokenService tokenService,
          IOptions<JwtSettings> jwtSettings,
          ILogger<AuthController> logger)
      {
          _connection   = connection;
          _tokenGen     = tokenGen;
          _tokenService = tokenService;
          _jwtSettings  = jwtSettings.Value;
          _logger       = logger;
      }

      // ════════════════════════════════════════════════════════════════════
      // UNIFIED LOGIN
      // ════════════════════════════════════════════════════════════════════
      [AllowAnonymous]
      [HttpPost("login")]
      public async Task<IActionResult> UnifiedLogin([FromBody] LoginRequest request)
      {
          var userSql = @"
              SELECT 
                  u.Id,
                  u.UserNumber,
                  u.FullName,
                  u.Email,
                  u.Role,
                  u.Password,
                  u.ContactPerson,
                  u.PhoneNo,
                  u.MobileNo,
                  u.ManagerId,
                  u.DefaultAssigneeId,
                  u.IsActive
              FROM dbo.Users u
              WHERE u.UserNumber = @UserNumber";

          var user = await _connection.QueryFirstOrDefaultAsync<User>(userSql, new { request.UserNumber });

          if (user == null || user.Password == null || request.Password != user.Password)
          {
              return Unauthorized(new { error = "Invalid credentials." });
          }

          if (!user.IsActive)
          {
              return Unauthorized(new { error = "Your account has been deactivated. Please contact support." });
          }

          // 1. Fetch dynamic RBAC data
          string dashboardRoute = "/login";
          IEnumerable<string> permissions = new List<string>();

          // Normalize legacy role names to SystemRoles names
          var systemRoleName = user.Role switch {
              "PM" => "Manager",
              "Super Admin" => "SuperManager",
              "Assignee" => "Developer",
              _ => user.Role
          };

          var roleData = await _connection.QueryFirstOrDefaultAsync(
              "SELECT Id, DashboardRoute FROM SystemRoles WHERE RoleName = @RoleName",
              new { RoleName = systemRoleName });

          if (roleData != null)
          {
              dashboardRoute = roleData.DashboardRoute;
              permissions = await _connection.QueryAsync<string>(
                  @"SELECT p.PermissionKey 
                    FROM SystemPermissions p
                    JOIN RolePermissions rp ON p.Id = rp.PermissionId
                    WHERE rp.RoleId = @RoleId",
                  new { RoleId = roleData.Id });
          }

          var userPermissionsList = permissions.ToList();
          if (userPermissionsList.Contains("view_pm_workspace"))
          {
              userPermissionsList.AddRange(new[] { "view_pm_dashboard", "view_pm_performance", "view_pm_tat", "view_pm_hierarchy" });
          }
          if (userPermissionsList.Contains("view_developer_workspace") && user.Role == "Assignee")
          {
              userPermissionsList.AddRange(new[] { "view_developer_dashboard", "view_developer_tat" });
          }
          if (userPermissionsList.Contains("view_master_customers") || 
              userPermissionsList.Contains("view_master_assignees") || 
              userPermissionsList.Contains("view_master_managers"))
          {
              userPermissionsList.Add("view_master_users");
          }
          if (userPermissionsList.Contains("view_master_categories"))
          {
              userPermissionsList.Add("view_master_sources");
          }

          var prefRole = user.Role switch { "Manager" => "PM", "TL" => "PM", "Project Manager" => "PM", "Developer" => "Assignee", _ => user.Role };
          var overrides = await _connection.QueryAsync<(string PermissionKey, bool IsEnabled)>(
              "SELECT PermissionKey, IsEnabled FROM dbo.UserPermissions WHERE UserId = @UserId AND UserRole = @UserRole",
              new { UserId = user.Id, UserRole = prefRole });

          foreach (var ov in overrides)
          {
              if (ov.IsEnabled)
              {
                  if (!userPermissionsList.Contains(ov.PermissionKey))
                      userPermissionsList.Add(ov.PermissionKey);
              }
              else
              {
                  userPermissionsList.Remove(ov.PermissionKey);
              }
          }
          permissions = userPermissionsList.Distinct();

          var detailViewType = await _connection.QueryFirstOrDefaultAsync<string>(
              "SELECT DetailViewType FROM dbo.UserPreferences WHERE UserId = @UserId AND UserRole = @UserRole",
              new { UserId = user.Id, UserRole = prefRole }) ?? "Default";

          // 2. Return tokens with new payload
          return await IssueTokensAsync(user.Id, user.Role, user.FullName ?? user.UserNumber,
              new { 
                  userId = user.Id, 
                  customerId = user.Id, 
                  role = user.Role, 
                  name = user.FullName ?? user.UserNumber,
                  userNumber = user.UserNumber,
                  dashboard = dashboardRoute,
                  permissions = permissions,
                  detailViewType = detailViewType
              });
      }

      [HttpGet("refresh-session")]
      [Authorize]
      public async Task<IActionResult> RefreshSession()
      {
          var userIdClaim = User.FindFirst("UserId")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
          var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
          var name = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
          
          if (string.IsNullOrEmpty(userIdClaim) || string.IsNullOrEmpty(role) || !int.TryParse(userIdClaim, out int userId))
          {
              return Unauthorized();
          }

          // Fetch fresh permissions and detailViewType from DB
          string dashboardRoute = "/login";
          IEnumerable<string> permissions = new List<string>();

          var systemRoleNameMe = role switch {
              "PM" => "Manager",
              "Super Admin" => "SuperManager",
              _ => role
          };

          var roleData = await _connection.QueryFirstOrDefaultAsync(
              "SELECT Id, DashboardRoute FROM SystemRoles WHERE RoleName = @RoleName",
              new { RoleName = systemRoleNameMe });

          if (roleData != null)
          {
              dashboardRoute = roleData.DashboardRoute;
              permissions = await _connection.QueryAsync<string>(
                  @"SELECT p.PermissionKey 
                    FROM SystemPermissions p
                    JOIN RolePermissions rp ON p.Id = rp.PermissionId
                    WHERE rp.RoleId = @RoleId",
                  new { RoleId = roleData.Id });
          }

          var userPermissionsList = permissions.ToList();
          if (userPermissionsList.Contains("view_pm_workspace"))
          {
              userPermissionsList.AddRange(new[] { "view_pm_dashboard", "view_pm_performance", "view_pm_tat", "view_pm_hierarchy" });
          }
          if (userPermissionsList.Contains("view_developer_workspace") && role == "Assignee")
          {
              userPermissionsList.AddRange(new[] { "view_developer_dashboard", "view_developer_tat" });
          }
          if (userPermissionsList.Contains("view_master_customers") || 
              userPermissionsList.Contains("view_master_assignees") || 
              userPermissionsList.Contains("view_master_managers"))
          {
              userPermissionsList.Add("view_master_users");
          }
          if (userPermissionsList.Contains("view_master_categories"))
          {
              userPermissionsList.Add("view_master_sources");
          }

          var prefRole = role switch { "Manager" => "PM", "TL" => "PM", "Project Manager" => "PM", "Developer" => "Assignee", _ => role };
          var overrides = await _connection.QueryAsync<(string PermissionKey, bool IsEnabled)>(
              "SELECT PermissionKey, IsEnabled FROM dbo.UserPermissions WHERE UserId = @UserId AND UserRole = @UserRole",
              new { UserId = userId, UserRole = prefRole });

          foreach (var ov in overrides)
          {
              if (ov.IsEnabled)
              {
                  if (!userPermissionsList.Contains(ov.PermissionKey))
                      userPermissionsList.Add(ov.PermissionKey);
              }
              else
              {
                  userPermissionsList.Remove(ov.PermissionKey);
              }
          }
          permissions = userPermissionsList.Distinct();

          var detailViewType = await _connection.QueryFirstOrDefaultAsync<string>(
              "SELECT DetailViewType FROM dbo.UserPreferences WHERE UserId = @UserId AND UserRole = @UserRole",
              new { UserId = userId, UserRole = prefRole }) ?? "Default";

          return await IssueTokensAsync(userId, role, name ?? userId.ToString(),
              new { 
                  userId = userId, 
                  customerId = userId, 
                  role = role, 
                  name = name,
                  dashboard = dashboardRoute,
                  permissions = permissions,
                  detailViewType = detailViewType
              });
      }
      
      // ════════════════════════════════════════════════════════════════════
      // REFRESH TOKEN  →  issues new access + refresh token (rotation)
      // ════════════════════════════════════════════════════════════════════
      [AllowAnonymous]
      [HttpPost("refresh")]
      public async Task<IActionResult> Refresh()
      {
          var rawRefreshToken = Request.Cookies[RefreshTokenCookie];
          if (string.IsNullOrEmpty(rawRefreshToken))
              return Unauthorized(new { error = "No refresh token provided." });

          var tokenHash = _tokenGen.HashRefreshToken(rawRefreshToken);
          var stored    = await _tokenService.GetRefreshTokenByHashAsync(tokenHash);

          if (stored == null || !stored.IsActive)
          {
              if (stored?.IsRevoked == true)
              {
                  _logger.LogWarning("[JWT] Refresh token reuse detected for User {UserId}! Revoking all tokens.", stored.UserId);
                  await _tokenService.RevokeAllUserTokensAsync(stored.UserId);
              }
              ClearRefreshCookie();
              return Unauthorized(new { error = "Invalid or expired refresh token. Please login again." });
          }

          var newRefreshToken = _tokenGen.GenerateRefreshToken();
          var newRefreshHash  = _tokenGen.HashRefreshToken(newRefreshToken);
          var newRefreshExpiry = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiryDays);

          await _tokenService.RevokeTokenAsync(tokenHash, replacedByHash: newRefreshHash);
          await _tokenService.SaveRefreshTokenAsync(
              stored.UserId, stored.UserRole, stored.FullName ?? "",
              newRefreshHash, newRefreshExpiry,
              ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

          var newAccessToken = _tokenGen.GenerateAccessToken(
              stored.UserId.ToString(), stored.UserRole, stored.FullName ?? "");

          SetRefreshCookie(newRefreshToken, newRefreshExpiry);

          // 1. Fetch dynamic RBAC data for refresh
          string dashboardRoute = "/login";
          IEnumerable<string> permissions = new List<string>();

          var systemRoleNameRefresh = stored.UserRole switch {
              "PM" => "Manager",
              "Super Admin" => "SuperManager",
              _ => stored.UserRole
          };

          var roleData = await _connection.QueryFirstOrDefaultAsync(
              "SELECT Id, DashboardRoute FROM SystemRoles WHERE RoleName = @RoleName",
              new { RoleName = systemRoleNameRefresh });

          if (roleData != null)
          {
              dashboardRoute = roleData.DashboardRoute;
              permissions = await _connection.QueryAsync<string>(
                  @"SELECT p.PermissionKey 
                    FROM SystemPermissions p
                    JOIN RolePermissions rp ON p.Id = rp.PermissionId
                    WHERE rp.RoleId = @RoleId",
                  new { RoleId = roleData.Id });
          }

          string? userNumber = await _connection.QueryFirstOrDefaultAsync<string>(
              "SELECT UserNumber FROM dbo.Users WHERE Id = @UserId",
              new { UserId = stored.UserId });

          var userPermissionsList = permissions.ToList();
          if (userPermissionsList.Contains("view_pm_workspace"))
          {
              userPermissionsList.AddRange(new[] { "view_pm_dashboard", "view_pm_performance", "view_pm_tat", "view_pm_hierarchy" });
          }
          if (userPermissionsList.Contains("view_developer_workspace") && stored.UserRole == "Assignee")
          {
              userPermissionsList.AddRange(new[] { "view_developer_dashboard", "view_developer_tat" });
          }
          if (userPermissionsList.Contains("view_master_customers") || 
              userPermissionsList.Contains("view_master_assignees") || 
              userPermissionsList.Contains("view_master_managers"))
          {
              userPermissionsList.Add("view_master_users");
          }
          if (userPermissionsList.Contains("view_master_categories"))
          {
              userPermissionsList.Add("view_master_sources");
          }

          var overrides = await _connection.QueryAsync<(string PermissionKey, bool IsEnabled)>(
              "SELECT PermissionKey, IsEnabled FROM dbo.UserPermissions WHERE UserId = @UserId AND UserRole = @UserRole",
              new { UserId = stored.UserId, UserRole = stored.UserRole });

          foreach (var ov in overrides)
          {
              if (ov.IsEnabled)
              {
                  if (!userPermissionsList.Contains(ov.PermissionKey))
                      userPermissionsList.Add(ov.PermissionKey);
              }
              else
              {
                  userPermissionsList.Remove(ov.PermissionKey);
              }
          }
          permissions = userPermissionsList.Distinct();

          var detailViewType = await _connection.QueryFirstOrDefaultAsync<string>(
              "SELECT DetailViewType FROM dbo.UserPreferences WHERE UserId = @UserId AND UserRole = @UserRole",
              new { UserId = stored.UserId, UserRole = stored.UserRole }) ?? "Default";

          return Ok(new
          {
              accessToken  = newAccessToken,
              expiresAt    = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpiryMinutes),
              userId       = stored.UserId,
              role         = stored.UserRole,
              name         = stored.FullName,
              userNumber   = userNumber,
              dashboard    = dashboardRoute,
              permissions  = permissions,
              detailViewType = detailViewType
          });
      }

      // ════════════════════════════════════════════════════════════════════
      // LOGOUT  →  revoke current refresh token + clear cookie
      // ════════════════════════════════════════════════════════════════════
      [AllowAnonymous]
      [HttpPost("logout")]
      public async Task<IActionResult> Logout()
      {
          var rawRefreshToken = Request.Cookies[RefreshTokenCookie];
          if (!string.IsNullOrEmpty(rawRefreshToken))
          {
              var tokenHash = _tokenGen.HashRefreshToken(rawRefreshToken);
              await _tokenService.RevokeTokenAsync(tokenHash);
          }
          ClearRefreshCookie();
          return Ok(new { message = "Logged out successfully." });
      }

      // ════════════════════════════════════════════════════════════════════
      // LOGOUT ALL DEVICES  →  revoke every refresh token for the user
      // ════════════════════════════════════════════════════════════════════
      [Authorize]
      [HttpPost("logout-all")]
      public async Task<IActionResult> LogoutAll()
      {
          var userIdClaim = User.FindFirst("uid")?.Value;
          if (!int.TryParse(userIdClaim, out var userId))
              return Unauthorized(new { error = "User ID not found in token." });

          await _tokenService.RevokeAllUserTokensAsync(userId);
          ClearRefreshCookie();
          return Ok(new { message = "Logged out from all devices." });
      }

      // ════════════════════════════════════════════════════════════════════
      // VALIDATE  →  quick check if access token is still valid
      // ════════════════════════════════════════════════════════════════════
      [Authorize]
      [HttpGet("validate")]
      public IActionResult Validate() => Ok(new
      {
          valid  = true,
          userId = User.FindFirst("uid")?.Value,
          role   = User.FindFirst("role")?.Value,
          name   = User.Identity?.Name
      });

      // ════════════════════════════════════════════════════════════════════
      // PRIVATE HELPERS
      // ════════════════════════════════════════════════════════════════════

      /// <summary>
      /// Issues both an access token (returned in body) and a refresh token (set as httpOnly cookie).
      /// </summary>
      private async Task<IActionResult> IssueTokensAsync(
          int userId, string role, string fullName, object extraPayload)
      {
          var accessToken   = _tokenGen.GenerateAccessToken(userId.ToString(), role, fullName);
          var rawRefresh    = _tokenGen.GenerateRefreshToken();
          var refreshHash   = _tokenGen.HashRefreshToken(rawRefresh);
          var refreshExpiry = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiryDays);

          await _tokenService.SaveRefreshTokenAsync(
              userId, role, fullName, refreshHash, refreshExpiry,
              ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

          SetRefreshCookie(rawRefresh, refreshExpiry);

          return Ok(new
          {
              accessToken = accessToken,
              expiresAt   = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpiryMinutes),
          }.MergeWith(extraPayload));
      }

      private void SetRefreshCookie(string rawToken, DateTime expiry)
      {
          Response.Cookies.Append(RefreshTokenCookie, rawToken, new CookieOptions
          {
              HttpOnly  = true,                         
              Secure    = false,                        
              SameSite  = SameSiteMode.Lax,
              Path      = "/api/auth",
              Expires   = expiry
          });
      }

      private void ClearRefreshCookie() =>
          Response.Cookies.Delete(RefreshTokenCookie, new CookieOptions
          {
              HttpOnly = true,
              SameSite = SameSiteMode.Lax,
              Path     = "/api/auth"
          });
  }

  internal static class ObjectExtensions
  {
      internal static object MergeWith(this object primary, object secondary)
      {
          var dict = new System.Dynamic.ExpandoObject() as IDictionary<string, object?>;

          foreach (var prop in primary.GetType().GetProperties())
              dict[prop.Name] = prop.GetValue(primary);

          foreach (var prop in secondary.GetType().GetProperties())
              dict[ToCamel(prop.Name)] = prop.GetValue(secondary);

          return dict;
      }
      private static string ToCamel(string s) => char.ToLower(s[0]) + s[1..];
  }
}

