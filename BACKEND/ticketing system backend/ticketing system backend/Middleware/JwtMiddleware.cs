using System.Net;
using System.Text.Json;
using ticketing_system_backend.Helpers;

namespace ticketing_system_backend.Middleware
{
    /// <summary>
    /// Custom JWT middleware that:
    /// 1. Enriches HttpContext with the authenticated user for easy downstream access.
    /// 2. Replaces ASP.NET's default HTML 401/403 responses with clean JSON.
    /// 3. Logs every authenticated request for auditing.
    /// </summary>
    public sealed class JwtMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<JwtMiddleware> _logger;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public JwtMiddleware(RequestDelegate next, ILogger<JwtMiddleware> logger)
        {
            _next   = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, IJwtTokenGenerator tokenGenerator)
        {
            // ── 1. Extract + validate access token → enrich context ──────────────
            var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
            if (authHeader?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true)
            {
                var rawToken  = authHeader["Bearer ".Length..].Trim();
                var principal = tokenGenerator.ValidateAccessToken(rawToken);

                if (principal != null)
                {
                    // Store the full ClaimsPrincipal for downstream use
                    context.Items["CurrentUser"] = principal;

                    var uid  = principal.FindFirst("uid")?.Value  ?? "?";
                    var role = principal.FindFirst("role")?.Value ?? "?";

                    _logger.LogInformation(
                        "[JWT] Authenticated | {Method} {Path} | uid={Uid} role={Role} | IP={IP}",
                        context.Request.Method,
                        context.Request.Path,
                        uid, role,
                        context.Connection.RemoteIpAddress);
                }
                else
                {
                    _logger.LogWarning(
                        "[JWT] Invalid/expired token | {Method} {Path} | IP={IP}",
                        context.Request.Method,
                        context.Request.Path,
                        context.Connection.RemoteIpAddress);
                }
            }

            await _next(context);

            // ── 2. Intercept 401 / 403 and return JSON if not already handled ────
            if ((context.Response.StatusCode == 401 || context.Response.StatusCode == 403) &&
                !context.Response.HasStarted &&
                context.Response.ContentType?.Contains("application/json") != true)
            {
                context.Response.ContentType = "application/json";

                var (code, message) = context.Response.StatusCode == 401
                    ? ("UNAUTHORIZED",  "Authentication required. Please login.")
                    : ("FORBIDDEN",     "You do not have permission to perform this action.");

                var body = JsonSerializer.Serialize(
                    new { success = false, code, message, status = context.Response.StatusCode },
                    _jsonOptions);

                await context.Response.WriteAsync(body);
            }
        }
    }

    // Extension method for clean registration in Program.cs
    public static class JwtMiddlewareExtensions
    {
        public static IApplicationBuilder UseJwtMiddleware(this IApplicationBuilder app)
            => app.UseMiddleware<JwtMiddleware>();
    }
}
