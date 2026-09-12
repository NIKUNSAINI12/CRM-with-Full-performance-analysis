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
            var claimsList = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub,  userId),
                new Claim(JwtRegisteredClaimNames.Jti,  Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Iat,
                          DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                          ClaimValueTypes.Integer64),
                new Claim(ClaimTypes.NameIdentifier,    userId),
                new Claim(ClaimTypes.Name,              fullName),
                new Claim("uid",  userId),
                new Claim("name", fullName),
            };

            if (role.Equals("TL", StringComparison.OrdinalIgnoreCase))
            {
                claimsList.Add(new Claim(ClaimTypes.Role, "TL"));
                claimsList.Add(new Claim(ClaimTypes.Role, "PM"));
                claimsList.Add(new Claim("role", "TL"));
            }
            else if (role.Equals("Manager", StringComparison.OrdinalIgnoreCase) || role.Equals("PM", StringComparison.OrdinalIgnoreCase))
            {
                claimsList.Add(new Claim(ClaimTypes.Role, "Manager"));
                claimsList.Add(new Claim(ClaimTypes.Role, "PM"));
                claimsList.Add(new Claim("role", "Manager"));
            }
            else if (role.Equals("SuperManager", StringComparison.OrdinalIgnoreCase) || role.Equals("Super Admin", StringComparison.OrdinalIgnoreCase))
            {
                claimsList.Add(new Claim(ClaimTypes.Role, "SuperManager"));
                claimsList.Add(new Claim(ClaimTypes.Role, "Super Admin"));
                claimsList.Add(new Claim(ClaimTypes.Role, "SuperManager"));
                claimsList.Add(new Claim("role", "SuperManager"));
            }
            else if (role.Equals("Developer", StringComparison.OrdinalIgnoreCase) || role.Equals("Assignee", StringComparison.OrdinalIgnoreCase))
            {
                claimsList.Add(new Claim(ClaimTypes.Role, "Developer"));
                claimsList.Add(new Claim(ClaimTypes.Role, "Assignee"));
                claimsList.Add(new Claim("role", "Developer"));
            }
            else
            {
                claimsList.Add(new Claim(ClaimTypes.Role, role));
                claimsList.Add(new Claim("role", role));
            }

            var claims = claimsList.ToArray();

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
