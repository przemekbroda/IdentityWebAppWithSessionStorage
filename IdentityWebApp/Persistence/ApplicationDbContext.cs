using IdentityWebApp.Common.Interfaces;
using IdentityWebApp.Entities;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace IdentityWebApp.Persistence
{
    public class ApplicationDbContext : IdentityDbContext<User, IdentityRole<long>, long>, IApplicationDbContext, IDataProtectionKeyContext
    {
        public DbSet<UserSession> UserSessions { get; set; }
        public DbSet<DataProtectionKey> DataProtectionKeys { get; set; }

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public Task<int> SaveChangesAsync()
        {
            return base.SaveChangesAsync();
        }
    }
}
