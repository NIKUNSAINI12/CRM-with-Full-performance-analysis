using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ticketing_system_backend.Interface;
using ticketing_system_backend.Models;
using ticketing_system_backend.Repository;

namespace ticketing_system_backend.Controllers
{
    [ApiController]
    [AllowAnonymous]
    [Route("api/external/sync")]
    public class ExternalSyncController : ControllerBase
    {
        private readonly IExternalSyncRepository _syncRepo;
        private readonly IConfiguration _config;
        private readonly ILogger<ExternalSyncController> _logger;

        public ExternalSyncController(
            IConfiguration config,
            ILogger<ExternalSyncController> logger,
            IServiceProvider serviceProvider)
        {
            _config = config;
            _logger = logger;

            // Attempt to resolve from DI, otherwise instantiate directly
            var repo = serviceProvider.GetService(typeof(IExternalSyncRepository)) as IExternalSyncRepository;
            if (repo != null)
            {
                _syncRepo = repo;
            }
            else
            {
                var defaultConn = serviceProvider.GetService(typeof(IDbConnection)) as IDbConnection 
                    ?? new SqlConnection(config.GetConnectionString("DefaultConnection") ?? "");
                var repoLogger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger<ExternalSyncRepository>();
                _syncRepo = new ExternalSyncRepository(config, defaultConn, repoLogger);
            }
        }

        // =====================================================================
        // AUTHENTICATION HELPER (Validates Username & Password directly)
        // =====================================================================
        private bool IsAuthorized(SyncAuthFields? bodyAuth, out string failureReason)
        {
            failureReason = string.Empty;

            var requireAuth = _config.GetValue<bool>("ExternalSyncApi:RequireAuth", true);
            if (!requireAuth) return true;

            var configuredUser = _config["ExternalSyncApi:DefaultUsername"] ?? "cjdarcl_sync_service";
            var configuredPass = _config["ExternalSyncApi:DefaultPassword"] ?? "";

            // 1. Check Username & Password in JSON Request Body
            if (bodyAuth != null && !string.IsNullOrWhiteSpace(bodyAuth.Username) && !string.IsNullOrWhiteSpace(bodyAuth.Password))
            {
                if (bodyAuth.Username == configuredUser && bodyAuth.Password == configuredPass)
                {
                    return true;
                }
            }

            // 2. Check Custom X-Username and X-Password Headers
            if (Request.Headers.TryGetValue("X-Username", out var hUser) &&
                Request.Headers.TryGetValue("X-Password", out var hPass))
            {
                if (hUser == configuredUser && hPass == configuredPass)
                {
                    return true;
                }
            }

            // 3. Check Basic Auth Header (Authorization: Basic <base64(user:pass)>)
            if (Request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                var authStr = authHeader.ToString();
                if (authStr.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var encodedCreds = authStr.Substring(6).Trim();
                        var decodedBytes = Convert.FromBase64String(encodedCreds);
                        var creds = Encoding.UTF8.GetString(decodedBytes).Split(':', 2);
                        if (creds.Length == 2 && creds[0] == configuredUser && creds[1] == configuredPass)
                        {
                            return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse Basic Auth header");
                    }
                }
            }

            // 4. Check Query Parameters for username & password (?username=...&password=...)
            if (Request.Query.TryGetValue("username", out var qUser) &&
                Request.Query.TryGetValue("password", out var qPass))
            {
                if (qUser == configuredUser && qPass == configuredPass)
                {
                    return true;
                }
            }

            failureReason = "Unauthorized: Invalid or missing username/password. Please provide valid username and password in JSON body, Basic Auth, or X-Username/X-Password headers configured in appsettings.json.";
            return false;
        }

        // =====================================================================
        // 1. CUSTOMER MASTER SYNC (CL_Master_Customer & Detail)
        // =====================================================================
        [HttpPost("customer")]
        public async Task<IActionResult> SyncCustomer([FromBody] CustomerSyncDto customer)
        {
            if (!IsAuthorized(customer, out var failureReason)) 
                return Unauthorized(new { success = false, message = failureReason });

            if (customer.CustomerId <= 0 || string.IsNullOrWhiteSpace(customer.CustomerName))
            {
                return BadRequest(new { success = false, message = "CustomerId and CustomerName are required." });
            }

            var success = await _syncRepo.UpsertCustomerAsync(customer);
            return Ok(new { success, message = $"Customer '{customer.CustomerName}' (ID {customer.CustomerId}) synced successfully." });
        }

        [HttpPost("customers/batch")]
        public async Task<IActionResult> SyncCustomersBatch([FromBody] object payload)
        {
            // Supports both: List<CustomerSyncDto> OR CustomerBatchSyncRequest wrapper
            List<CustomerSyncDto>? customerList = null;
            SyncAuthFields? authFields = null;

            var rawJson = payload?.ToString() ?? "{}";
            try
            {
                if (rawJson.TrimStart().StartsWith("["))
                {
                    customerList = System.Text.Json.JsonSerializer.Deserialize<List<CustomerSyncDto>>(rawJson, 
                        new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                else
                {
                    var wrapper = System.Text.Json.JsonSerializer.Deserialize<CustomerBatchSyncRequest>(rawJson, 
                        new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (wrapper != null)
                    {
                        customerList = wrapper.Customers;
                        authFields = wrapper;
                    }
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = $"Invalid JSON payload format: {ex.Message}" });
            }

            if (!IsAuthorized(authFields, out var failureReason)) 
                return Unauthorized(new { success = false, message = failureReason });

            if (customerList == null || customerList.Count == 0)
            {
                return BadRequest(new { success = false, message = "Customer batch list cannot be empty." });
            }

            var result = await _syncRepo.UpsertCustomersBatchAsync(customerList);
            return Ok(result);
        }

        // =====================================================================
        // 2. USER / EMPLOYEE MASTER SYNC (CL_Master_User)
        // =====================================================================
        [HttpPost("user")]
        public async Task<IActionResult> SyncUser([FromBody] UserSyncDto user)
        {
            if (!IsAuthorized(user, out var failureReason)) 
                return Unauthorized(new { success = false, message = failureReason });

            if (user.UserId <= 0 || string.IsNullOrWhiteSpace(user.Name))
            {
                return BadRequest(new { success = false, message = "UserId and Name are required." });
            }

            var success = await _syncRepo.UpsertUserAsync(user);
            return Ok(new { success, message = $"User '{user.Name}' (ID {user.UserId}) synced successfully." });
        }

        [HttpPost("users/batch")]
        public async Task<IActionResult> SyncUsersBatch([FromBody] object payload)
        {
            List<UserSyncDto>? userList = null;
            SyncAuthFields? authFields = null;

            var rawJson = payload?.ToString() ?? "{}";
            try
            {
                if (rawJson.TrimStart().StartsWith("["))
                {
                    userList = System.Text.Json.JsonSerializer.Deserialize<List<UserSyncDto>>(rawJson, 
                        new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                else
                {
                    var wrapper = System.Text.Json.JsonSerializer.Deserialize<UserBatchSyncRequest>(rawJson, 
                        new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (wrapper != null)
                    {
                        userList = wrapper.Users;
                        authFields = wrapper;
                    }
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = $"Invalid JSON payload format: {ex.Message}" });
            }

            if (!IsAuthorized(authFields, out var failureReason)) 
                return Unauthorized(new { success = false, message = failureReason });

            if (userList == null || userList.Count == 0)
            {
                return BadRequest(new { success = false, message = "User batch list cannot be empty." });
            }

            var result = await _syncRepo.UpsertUsersBatchAsync(userList);
            return Ok(result);
        }

        // =====================================================================
        // 3. DOCKET CONSIGNMENT SYNC (CL_Docket)
        // =====================================================================
        [HttpPost("docket")]
        public async Task<IActionResult> SyncDocket([FromBody] DocketSyncDto docket)
        {
            if (!IsAuthorized(docket, out var failureReason)) 
                return Unauthorized(new { success = false, message = failureReason });

            if (string.IsNullOrWhiteSpace(docket.DocketNo))
            {
                return BadRequest(new { success = false, message = "DocketNo is required." });
            }

            var success = await _syncRepo.UpsertDocketAsync(docket);
            return Ok(new { success, message = $"Docket '{docket.DocketNo}' synced successfully." });
        }

        [HttpPost("dockets/batch")]
        public async Task<IActionResult> SyncDocketsBatch([FromBody] object payload)
        {
            List<DocketSyncDto>? docketList = null;
            SyncAuthFields? authFields = null;

            var rawJson = payload?.ToString() ?? "{}";
            try
            {
                if (rawJson.TrimStart().StartsWith("["))
                {
                    docketList = System.Text.Json.JsonSerializer.Deserialize<List<DocketSyncDto>>(rawJson, 
                        new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                else
                {
                    var wrapper = System.Text.Json.JsonSerializer.Deserialize<DocketBatchSyncRequest>(rawJson, 
                        new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (wrapper != null)
                    {
                        docketList = wrapper.Dockets;
                        authFields = wrapper;
                    }
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = $"Invalid JSON payload format: {ex.Message}" });
            }

            if (!IsAuthorized(authFields, out var failureReason)) 
                return Unauthorized(new { success = false, message = failureReason });

            if (docketList == null || docketList.Count == 0)
            {
                return BadRequest(new { success = false, message = "Docket batch list cannot be empty." });
            }

            var result = await _syncRepo.UpsertDocketsBatchAsync(docketList);
            return Ok(result);
        }

        // =====================================================================
        // 4. UNIFIED BATCH SYNC (Customers + Users + Dockets)
        // =====================================================================
        [HttpPost("all")]
        public async Task<IActionResult> SyncUnifiedBatch([FromBody] UnifiedSyncRequest request)
        {
            if (!IsAuthorized(request, out var failureReason)) 
                return Unauthorized(new { success = false, message = failureReason });

            if (request == null)
            {
                return BadRequest(new { success = false, message = "Unified sync payload is required." });
            }

            var result = await _syncRepo.UpsertUnifiedBatchAsync(request);
            return Ok(result);
        }
    }
}
