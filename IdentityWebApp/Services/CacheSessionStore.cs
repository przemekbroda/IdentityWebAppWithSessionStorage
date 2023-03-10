using IdentityWebApp.Entities;
using IdentityWebApp.Repositories;
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

            var json = JsonSerializer.Serialize(new AuthenticationTicketData(ticket.Properties, ticket.AuthenticationScheme, claims));

            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpiration = expiresUtc,
            };

            await _distributedCache.SetStringAsync(key, json, options);
        }

        public async Task<AuthenticationTicket?> RetrieveAsync(string key)
        {
            var json = await _distributedCache.GetStringAsync(key);

            if (json is null)
            {
                return null;
            }

            var authenticationTicketData = JsonSerializer.Deserialize<AuthenticationTicketData>(json);

            if (authenticationTicketData is null)
            {
                return null;
            }

            var claims = authenticationTicketData.Claims.Select(claim => new Claim(claim.Type, claim.Value));
            var claimsIdentity = new ClaimsIdentity(claims, authenticationTicketData.AuthenticationScheme);
            var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);
            var ticket = new AuthenticationTicket(claimsPrincipal, authenticationTicketData.AuthenticationScheme);
            ticket.Properties.ExpiresUtc = authenticationTicketData.AuthenticationProperties.ExpiresUtc;
            ticket.Properties.IssuedUtc = authenticationTicketData.AuthenticationProperties.IssuedUtc;
            ticket.Properties.AllowRefresh = authenticationTicketData.AuthenticationProperties.AllowRefresh;
            ticket.Properties.IsPersistent = authenticationTicketData.AuthenticationProperties.IsPersistent;

            return ticket;
        }

        public async Task<string> StoreAsync(AuthenticationTicket ticket)
        {
            var bytes = RandomNumberGenerator.GetBytes(100);
            var key = KeyPrefix + Convert.ToBase64String(bytes);
            await RenewAsync(key, ticket);
            return key;
        }

        private async Task SaveOrUpdateUserSessionData(string key, AuthenticationTicket ticket)
        {
            var scope = _serviceProvider.CreateScope();
            var dataContext = scope.ServiceProvider.GetService<DataContext>();

            if (dataContext is null)
            {
                throw new Exception("DataContext not found");
            }

            var userSessionWithSameSessionId = await dataContext
                .UserSessions
                .FirstOrDefaultAsync(userSession => userSession.SessionId == key);

            if (userSessionWithSameSessionId is null)
            {
                var userId = ticket.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (userId is null)
                {
                    throw new Exception("User id is not present in claims");
                }

                userSessionWithSameSessionId = new UserSession
                {
                    ExpiresAt = ticket.Properties.ExpiresUtc,
                    SessionId = key,
                    UserId = long.Parse(userId),
                };

                await dataContext.AddAsync(userSessionWithSameSessionId);
            }
            else
            {
                userSessionWithSameSessionId.ExpiresAt = ticket.Properties.ExpiresUtc;
            }

            await dataContext.SaveChangesAsync();
        }

        private record ClaimsData(string Type, string Value);
        private record AuthenticationTicketData(
            AuthenticationProperties AuthenticationProperties,
            string AuthenticationScheme,
            IEnumerable<ClaimsData> Claims
        );
    }
}
