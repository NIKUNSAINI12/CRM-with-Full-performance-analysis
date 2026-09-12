using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ticketing_system_backend.Settings;

namespace ticketing_system_backend.Helpers
{
    /// <summary>
    /// Generates signed JWT access tokens and cryptographically secure refresh tokens.
    /// </summary>
    public interface IJwtTokenGenerator
    {
        string        GenerateAccessToken(string userId, string role, string fullName);
        string        GenerateRefreshToken();
        string        HashRefreshToken(string rawToken);
        ClaimsPrincipal? ValidateAccessToken(string token);
    }

    public sealed class JwtTokenGenerator : IJwtTokenGenerator
    {
        private readonly JwtSettings        _settings;
        private readonly SymmetricSecurityKey _key;
        private readonly TokenValidationParameters _validationParams;

        public JwtTokenGenerator(IOptions<JwtSettings> settings)
        {
            _settings = settings.Value;
            _key      = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key));

            _validationParams = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey         = _key,
                ValidateIssuer           = true,
                ValidIssuer              = _settings.Issuer,
                ValidateAudience         = true,
                ValidAudience            = _settings.Audience,
                ValidateLifetime         = true,
                ClockSkew                = TimeSpan.Zero   // No tolerance — expires exactly at ExpiryMinutes
            };
        }

        /// <summary>Creates a signed JWT access token (short-lived).</summary>
        public string GenerateAccessToken(string userId, string role, string fullName)
        {
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub,  userId),
                new Claim(JwtRegisteredClaimNames.Jti,  Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Iat,
                          DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                          ClaimValueTypes.Integer64),
                new Claim(ClaimTypes.NameIdentifier,    userId),
                new Claim(ClaimTypes.Name,              fullName),
                new Claim(ClaimTypes.Role,              role),
                // Custom short-name claims for easy client-side reading
                new Claim("uid",  userId),
                new Claim("role", role),
                new Claim("name", fullName),
            };

            var token = new JwtSecurityToken(
                issuer:             _settings.Issuer,
                audience:           _settings.Audience,
                claims:             claims,
                notBefore:          DateTime.UtcNow,
                expires:            DateTime.UtcNow.AddMinutes(_settings.AccessTokenExpiryMinutes),
                signingCredentials: new SigningCredentials(_key, SecurityAlgorithms.HmacSha256));

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        /// <summary>Generates a cryptographically secure random refresh token (opaque bytes → base64url).</summary>
        public string GenerateRefreshToken()
        {
            var bytes = new byte[64];
            RandomNumberGenerator.Fill(bytes);
            return Convert.ToBase64String(bytes)
                   .Replace('+', '-').Replace('/', '_').TrimEnd('='); // base64url safe
        }

        /// <summary>SHA-256 hash of raw refresh token — stored in DB, never the plaintext.</summary>
        public string HashRefreshToken(string rawToken)
        {
            var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
            return Convert.ToHexString(hashBytes).ToLower();
        }

        /// <summary>Validates an access token without throwing — returns null if invalid/expired.</summary>
        public ClaimsPrincipal? ValidateAccessToken(string token)
        {
            try
            {
                var handler    = new JwtSecurityTokenHandler();
                var principal  = handler.ValidateToken(token, _validationParams, out _);
                return principal;
            }
            catch
            {
                return null;
            }
        }
    }
}
