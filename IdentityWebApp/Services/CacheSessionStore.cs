using IdentityWebApp.Common.Interfaces;
using IdentityWebApp.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;

namespace IdentityWebApp.Services
{
    public class CacheSessionStore : ITicketStore
    {
        private const string KeyPrefix = "AuthSessionStore-";
        private readonly IDistributedCache _distributedCache;
        private readonly IServiceProvider _serviceProvider;

        public CacheSessionStore(IDistributedCache distributedCache, IServiceProvider serviceProvider)
        {
            _distributedCache = distributedCache;
            _serviceProvider = serviceProvider;
        }

        public async Task RemoveAsync(string key)
        {
            await _distributedCache.RemoveAsync(key);
        }

        public async Task RenewAsync(string key, AuthenticationTicket ticket)
        {
            var expiresUtc = ticket.Properties.ExpiresUtc;
            var claims = ticket.Principal
                .Claims
                .Select(claim => new ClaimsData(claim.Type, claim.Value));

            await SaveOrUpdateUserSessionData(key, ticket);

            var authenticationTicketDataBytes = JsonSerializer.SerializeToUtf8Bytes(new AuthenticationTicketData(ticket.AuthenticationScheme, ticket.Properties, claims));

            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpiration = expiresUtc,
            };

            await _distributedCache.SetAsync(key, authenticationTicketDataBytes, options);
        }

        public async Task<AuthenticationTicket?> RetrieveAsync(string key)
        {
            var authenticationTicketDataBytes = await _distributedCache.GetAsync(key);

            if (authenticationTicketDataBytes is null)
            {
                return null;
            }

            var authenticationTicketData = JsonSerializer.Deserialize<AuthenticationTicketData>(authenticationTicketDataBytes);

            if (authenticationTicketData is null)
            {
                return null;
            }

            var claims = authenticationTicketData.Claims.Select(claim => new Claim(claim.Type, claim.Value));
            var claimsIdentity = new ClaimsIdentity(claims, authenticationTicketData.AuthenticationScheme);
            var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);
            var ticket = new AuthenticationTicket(claimsPrincipal, authenticationTicketData.AuthenticationProperties, authenticationTicketData.AuthenticationScheme);

            return ticket;
        }

        public async Task<string> StoreAsync(AuthenticationTicket ticket)
        {
            var bytes = RandomNumberGenerator.GetBytes(100);
            var key = KeyPrefix + Convert.ToBase64String(bytes);
            await RenewAsync(key, ticket);
            return key;
        }

        private async Task SaveOrUpdateUserSessionData(string sessionId, AuthenticationTicket ticket)
        {
            var scope = _serviceProvider.CreateScope();
            var dataContext = scope.ServiceProvider.GetService<IApplicationDbContext>();

            if (dataContext is null)
            {
                throw new Exception("DataContext not found");
            }

            var userSessionInfo = await dataContext
                .UserSessions
                .FirstOrDefaultAsync(userSession => userSession.SessionId == sessionId);

            if (userSessionInfo is null)
            {
                var userId = ticket.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var userAgent = ticket.Principal.FindFirst(ClaimTypes.System)?.Value;

                if (userId is null)
                {
                    throw new Exception("User id is not present in claims");
                }

                userSessionInfo = new UserSession
                {
                    ExpiresAt = ticket.Properties.ExpiresUtc,
                    SessionId = sessionId,
                    UserId = long.Parse(userId),
                    UserAgent = userAgent,
                };

                await dataContext.UserSessions.AddAsync(userSessionInfo);
            }
            else
            {
                userSessionInfo.ExpiresAt = ticket.Properties.ExpiresUtc;
            }

            await dataContext.SaveChangesAsync();
        }

        private record ClaimsData(string Type, string Value);

        private record AuthenticationTicketData(
            string AuthenticationScheme,
            AuthenticationProperties AuthenticationProperties,
            IEnumerable<ClaimsData> Claims
        );
    }
}
