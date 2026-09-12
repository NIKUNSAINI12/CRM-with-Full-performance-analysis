using Dapper;
using Microsoft.Data.SqlClient;
using System.Data;
using ticketing_system_backend.Models.Auth;

namespace ticketing_system_backend.Services
{
    /// <summary>
    /// Manages refresh token lifecycle in the database.
    /// Implements token rotation — every refresh issues a NEW token and revokes the old one.
    /// </summary>
    public sealed class TokenService : ITokenService
    {
        private readonly string _connectionString;
        private readonly ILogger<TokenService> _logger;

        public TokenService(IConfiguration config, ILogger<TokenService> logger)
        {
            _connectionString = config.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("DefaultConnection not configured.");
            _logger = logger;
        }

        // ── Save a brand-new refresh token after login / rotation ──────────────
        public async Task SaveRefreshTokenAsync(
            int userId, string role, string fullName,
            string tokenHash, DateTime expiresAt, string? ipAddress)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.ExecuteAsync(@"
                INSERT INTO RefreshTokens (UserId, UserRole, FullName, TokenHash, ExpiresAt, IpAddress)
                VALUES (@UserId, @UserRole, @FullName, @TokenHash, @ExpiresAt, @IpAddress)",
                new { UserId = userId, UserRole = role, FullName = fullName,
                      TokenHash = tokenHash, ExpiresAt = expiresAt, IpAddress = ipAddress });

            _logger.LogInformation("Refresh token saved for User {UserId} ({Role})", userId, role);
        }

        // ── Retrieve by SHA-256 hash ────────────────────────────────────────────
        public async Task<RefreshTokenRecord?> GetRefreshTokenByHashAsync(string tokenHash)
        {
            using var conn = new SqlConnection(_connectionString);
            return await conn.QueryFirstOrDefaultAsync<RefreshTokenRecord>(@"
                SELECT * FROM RefreshTokens WHERE TokenHash = @TokenHash",
                new { TokenHash = tokenHash });
        }

        // ── Revoke a token (optionally record what replaced it for audit trail) ─
        public async Task RevokeTokenAsync(string tokenHash, string? replacedByHash = null)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.ExecuteAsync(@"
                UPDATE RefreshTokens
                SET    IsRevoked = 1, RevokedAt = GETUTCDATE(), ReplacedByTokenHash = @ReplacedBy
                WHERE  TokenHash = @TokenHash",
                new { TokenHash = tokenHash, ReplacedBy = replacedByHash });

            _logger.LogInformation("Refresh token revoked. Replaced: {HasReplacement}", replacedByHash != null);
        }

        // ── Revoke ALL tokens for a user (logout-all-devices) ──────────────────
        public async Task RevokeAllUserTokensAsync(int userId)
        {
            using var conn = new SqlConnection(_connectionString);
            var count = await conn.ExecuteAsync(@"
                UPDATE RefreshTokens
                SET    IsRevoked = 1, RevokedAt = GETUTCDATE()
                WHERE  UserId = @UserId AND IsRevoked = 0",
                new { UserId = userId });

            _logger.LogInformation("Revoked {Count} refresh tokens for User {UserId}", count, userId);
        }

        // ── Housekeeping — prune expired + revoked tokens older than 30 days ───
        public async Task PruneExpiredTokensAsync()
        {
            using var conn = new SqlConnection(_connectionString);
            var deleted = await conn.ExecuteAsync(@"
                DELETE FROM RefreshTokens
                WHERE  (IsRevoked = 1 OR ExpiresAt < GETUTCDATE())
                  AND  CreatedAt < DATEADD(DAY, -30, GETUTCDATE())");

            _logger.LogInformation("Pruned {Count} old refresh tokens", deleted);
        }
    }
}
