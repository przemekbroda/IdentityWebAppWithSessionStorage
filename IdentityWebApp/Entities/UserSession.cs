namespace IdentityWebApp.Entities
{
    public class UserSession
    {
        public long Id { get; set; }
        public required string SessionId { get; set; }
        public DateTimeOffset? ExpiresAt { get; set; }
        public User? User { get; set; }
        public long UserId { get; set; }

    }
}
