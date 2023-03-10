using IdentityWebApp.Entities;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace IdentityWebApp.Repositories
{
    public class DataContext : IdentityDbContext<User, IdentityRole<long>, long>, IDataProtectionKeyContext
    {
        public DbSet<UserSession> UserSessions { get; set; }

        public DbSet<DataProtectionKey> DataProtectionKeys { get; set; }

        public DataContext(DbContextOptions<DataContext> options) : base(options)
        {
        }
    }
}
