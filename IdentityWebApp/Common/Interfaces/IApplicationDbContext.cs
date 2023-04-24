using IdentityWebApp.Entities;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace IdentityWebApp.Common.Interfaces
{
    public interface IApplicationDbContext
    {
        DbSet<User> Users { get; }
        DbSet<UserSession> UserSessions { get; }
        DbSet<DataProtectionKey> DataProtectionKeys { get; }
        Task<int> SaveChangesAsync();
    }
}
