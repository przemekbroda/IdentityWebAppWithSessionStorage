using Microsoft.AspNetCore.Identity;

namespace IdentityWebApp.Entities
{
    public class User : IdentityUser<long>
    {
        public required string FirstName { get; set; }
        public required string LastName { get; set; }
        public DateTime? BirthDay { get; set; }
        public IEnumerable<UserSession>? UserSessions { get; set; }
    }
}
