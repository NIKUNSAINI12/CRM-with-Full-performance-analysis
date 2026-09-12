namespace ticketing_system_backend.Models.Auth
{
    /// <summary>Maps to the RefreshTokens table in the database.</summary>
    public class RefreshTokenRecord
    {
        public int      Id                  { get; set; }
        public int      UserId              { get; set; }
        public string   UserRole            { get; set; } = string.Empty;
        public string?  FullName            { get; set; }
        public string   TokenHash           { get; set; } = string.Empty;
        public DateTime ExpiresAt           { get; set; }
        public bool     IsRevoked           { get; set; }
        public DateTime CreatedAt           { get; set; }
        public DateTime? RevokedAt          { get; set; }
        public string?  ReplacedByTokenHash { get; set; }
        public string?  IpAddress           { get; set; }

        public bool IsExpired  => DateTime.UtcNow >= ExpiresAt;
        public bool IsActive   => !IsRevoked && !IsExpired;
    }
}
