using IdentityWebApp.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace IdentityWebApp.Repositories
{
    public class DataContext : IdentityDbContext<User, IdentityRole<long>, long>
    {
        public DbSet<UserSession> UserSessions { get; set; }

        public DataContext(DbContextOptions<DataContext> options) : base(options)
        {
        }
    }
}
