using ticketing_system_backend.Models.Auth;

namespace ticketing_system_backend.Services
{
    public interface ITokenService
    {
        Task SaveRefreshTokenAsync(int userId, string role, string fullName, string tokenHash, DateTime expiresAt, string? ipAddress);
        Task<RefreshTokenRecord?> GetRefreshTokenByHashAsync(string tokenHash);
        Task RevokeTokenAsync(string tokenHash, string? replacedByHash = null);
        Task RevokeAllUserTokensAsync(int userId);
        Task PruneExpiredTokensAsync();
    }
}
