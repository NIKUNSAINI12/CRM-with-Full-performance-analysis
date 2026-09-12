namespace ticketing_system_backend.Settings
{
    /// <summary>
    /// Strongly-typed JWT configuration bound from appsettings.json "Jwt" section.
    /// </summary>
    public class JwtSettings
    {
        public const string SectionName = "Jwt";

        public string Key                   { get; set; } = string.Empty;
        public string Issuer                { get; set; } = "TicketingAPI";
        public string Audience              { get; set; } = "TicketingApp";
        public int    AccessTokenExpiryMinutes { get; set; } = 15;
        public int    RefreshTokenExpiryDays   { get; set; } = 7;
    }
}
