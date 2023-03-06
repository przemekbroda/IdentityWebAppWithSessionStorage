using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Caching.Distributed;
using System.Security.Claims;
using System.Text.Json;

namespace IdentityWebApp
{
    public class CacheSessionStore : ITicketStore
    {
        private const string KeyPrefix = "AuthSessionStore-";
        private readonly IDistributedCache _distributedCache;

        public CacheSessionStore(IDistributedCache distributedCache)
        {
            _distributedCache = distributedCache;
        }

        public async Task RemoveAsync(string key)
        {
            await _distributedCache.RemoveAsync(key);
        }

        public async Task RenewAsync(string key, AuthenticationTicket ticket)
        {
            var expiresUtc = ticket.Properties.ExpiresUtc;
            var claims = ticket.Principal.Claims
                .ToDictionary(
                    claim => 
                    {
                        return claim.Type;
                    },
                    claim => 
                    {
                        return claim.Value;
                    }
                );
            var json = JsonSerializer.Serialize(new ClaimsData(ticket.AuthenticationScheme, claims));

            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpiration = expiresUtc,
                SlidingExpiration = TimeSpan.FromHours(10)
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

            var claimsData = JsonSerializer.Deserialize<ClaimsData>(json);

            if (claimsData is null)
            {
                return null;
            }

            var claims = claimsData.Claims.Select(claim => new Claim(claim.Key, claim.Value));
            var claimsIdentity = new ClaimsIdentity(claims, claimsData.AuthenticationScheme);
            var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);
            var ticket = new AuthenticationTicket(claimsPrincipal, claimsData.AuthenticationScheme);

            return ticket;
        }

        public async Task<string> StoreAsync(AuthenticationTicket ticket)
        {
            var key = KeyPrefix + Guid.NewGuid().ToString();
            await RenewAsync(key, ticket);
            return key;
        }

        private record ClaimsData(string AuthenticationScheme, Dictionary<string, string> Claims);
    }
}
