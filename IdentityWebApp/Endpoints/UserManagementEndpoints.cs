﻿using IdentityWebApp.Common.Interfaces;
using IdentityWebApp.Common.Models;
using IdentityWebApp.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace IdentityWebApp.Endpoints
{
    internal static class UserManagementEndpoints
    {
        internal static void AddUserManagementEndpoints(this WebApplication app) 
        {
            app.MapGroup("/user").MapUserApi();
            app.MapGroup("/role").MapRoleApi();

            app.MapPost("/authenticate", async (string email, string password, SignInManager<User> signInManager, UserManager<User> userManager, HttpContext context) =>
            {
                var user = await userManager.FindByNameAsync(email);

                if (user is null)
                {
                    return Results.BadRequest();
                }

                var result = await signInManager.CheckPasswordSignInAsync(user, password, true);

                if (!result.Succeeded)
                {
                    return Results.BadRequest();
                }

                var claims = await userManager.GetClaimsAsync(user);

                var userClaims = await GenerateUserClaimsListAsync(user, userManager);

                await context.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(
                        new ClaimsIdentity(
                            userClaims,
                            CookieAuthenticationDefaults.AuthenticationScheme
                        )
                    ),
                    new AuthenticationProperties
                    {
                        IsPersistent = true, //session token or with end date 
                        AllowRefresh = true,
                    }
                );

                return Results.Ok();
            });
            app.MapPost("/logout", async (HttpContext context) =>
            {
                await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

                return Results.Ok();
            });

            app.MapGet("/admins-only", () =>
            {
                return Results.Ok("Hello admin");
            }).RequireAuthorization("admin");
        }

        private static RouteGroupBuilder MapUserApi(this RouteGroupBuilder builder)
        {
            builder.MapGet("/{userId}", async (long userId, UserManager<User> userManager, HttpContext context) =>
            {
                var user = await userManager.FindByIdAsync(userId.ToString());

                if (user is null)
                {
                    return Results.NotFound();
                }

                return Results.Ok(user);
            });

            builder.MapGet("", async (long userId, IApplicationDbContext dbContext, HttpContext context) =>
            {
                var users = await dbContext.Users.ToListAsync();

                return Results.Ok(users);
            }).RequireAuthorization();

            builder.MapPost("/user", async (CreateUserDto createUser, UserManager<User> userManager) =>
            {
                var userInDb = await userManager.FindByEmailAsync(createUser.Email);

                var newUser = new User
                {
                    UserName = createUser.Email,
                    FirstName = createUser.FirstName,
                    LastName = createUser.LastName,
                    Email = createUser.Email,
                    PhoneNumber = createUser.PhoneNumber,
                };

                var result = await userManager.CreateAsync(newUser, createUser.Password);

                if (!result.Succeeded)
                {
                    return Results.BadRequest(result.Errors.Select(err => err.Description));
                }

                await userManager.AddToRoleAsync(newUser, "user");

                return Results.Ok(newUser);
            });

            builder.MapGet("/{userId}/role", async (UserManager<User> userManager, long userId) =>
            {
                var user = await userManager.FindByIdAsync(userId.ToString());

                if (user is null)
                {
                    return Results.NotFound();
                }

                var roles = await userManager.GetRolesAsync(user);

                return Results.Ok(roles);
            });

            builder.MapPost("/{userId}/role", async (long userId, string role, IApplicationDbContext dataContext, UserManager<User> userManager, ITicketStore ticketStore) => 
            {
                var user = await dataContext
                    .Users
                    .Where(user => user.Id == userId)
                    .Include(user => user.UserSessions)
                    .FirstOrDefaultAsync();

                if (user is null)
                {
                    return Results.NotFound();
                }

                var result = await userManager.AddToRoleAsync(user, role);

                if (!result.Succeeded)
                {
                    return Results.BadRequest();
                }

                await UpdateUserSessionsWithNewClaimsAsync(ticketStore, user.UserSessions, await GenerateUserClaimsListAsync(user, userManager));

                return Results.NoContent();

            });

            builder.MapDelete("/{userId}/role", async (long userId, string[] roles, IApplicationDbContext dataContext, UserManager<User> userManager, ITicketStore ticketStore) =>
            {
                var userWithSessions = await dataContext
                    .Users
                    .Where(user => user.Id == userId)
                    .Include(user => user.UserSessions)
                    .FirstOrDefaultAsync();

                if (userWithSessions is null)
                {
                    return Results.NotFound();
                }

                var result = await userManager.RemoveFromRolesAsync(userWithSessions, roles);

                if (!result.Succeeded)
                {
                    return Results.BadRequest();
                }

                var userSessions = userWithSessions.UserSessions;

                if (userSessions is not null && userSessions.Any())
                {
                    await UpdateUserSessionsWithNewClaimsAsync(ticketStore, userSessions, await GenerateUserClaimsListAsync(userWithSessions, userManager));
                }

                return Results.NoContent();
            });

            builder.MapGet("/{userId}/session", async (long userId, IApplicationDbContext dataContext, ITicketStore ticketStore) =>
            {
                var userWithSessions = await dataContext
                    .Users
                    .Where(user => user.Id == userId)
                    .Include(user => user.UserSessions.Where(userSession => userSession.ExpiresAt >= DateTimeOffset.Now))
                    .AsNoTracking()
                    .FirstOrDefaultAsync();

                if (userWithSessions is null)
                {
                    return Results.NotFound();
                }

                return Results.Ok(userWithSessions.UserSessions);
            });

            builder.MapDelete("/{userId}/session", async (long userId, IApplicationDbContext dataContext, ITicketStore ticketStore) =>
            {
                var userWithSessions = await dataContext
                    .Users
                    .Where(user => user.Id == userId)
                    .Include(user => user.UserSessions)
                    .AsNoTracking()
                    .FirstOrDefaultAsync();

                if (userWithSessions is null)
                {
                    return Results.NotFound();
                }

                if (!userWithSessions.UserSessions.Any())
                {
                    return Results.NoContent();
                }

                foreach (var userSession in userWithSessions.UserSessions)
                {
                    dataContext.UserSessions.Remove(userSession);
                }

                var chunkedUserSessions = userWithSessions
                    .UserSessions
                    .Chunk(20);

                foreach (var chunk in chunkedUserSessions)
                {
                    await Task.WhenAll(chunk.Select(userSession => ticketStore.RemoveAsync(userSession.SessionId)));
                }

                return Results.NoContent();
            });

            builder.MapDelete("/{userId}/session/{sessionId}", async (long userId, long sessionId, IApplicationDbContext dataContext, ITicketStore ticketStore) =>
            {
                var userWithSessions = await dataContext
                    .Users
                    .Where(user => user.Id == userId)
                    .Include(user => user.UserSessions.Where(userSession => userSession.Id == sessionId))
                    .FirstOrDefaultAsync();

                if (userWithSessions is null || !userWithSessions.UserSessions.Any())
                {
                    return Results.NotFound();
                }

                dataContext.UserSessions.Remove(userWithSessions.UserSessions.First());
                await dataContext.SaveChangesAsync();

                var sessionsIds = userWithSessions
                    .UserSessions
                    .Select(userSession => ticketStore.RemoveAsync(userSession.SessionId));

                if (sessionsIds is not null && sessionsIds.Any())
                {
                    await Task.WhenAll(sessionsIds);
                }

                return Results.NoContent();
            });

            return builder;
        }

        private static RouteGroupBuilder MapRoleApi(this RouteGroupBuilder builder)
        {
            builder.MapGet("", async (RoleManager<IdentityRole<long>> roleManager) =>
            {
                var roles = await roleManager.Roles.ToListAsync();
                return Results.Ok(roles);
            });

            builder.MapGet("/{roleName}", async (string roleName, RoleManager<IdentityRole<long>> roleManager) =>
            {
                var role = await roleManager.FindByNameAsync(roleName);

                if (role is null)
                {
                    return Results.NotFound();
                }

                return Results.Ok(role);
            });

            builder.MapPost("", async (string roleName, RoleManager<IdentityRole<long>> roleManager) =>
            {
                var newRole = new IdentityRole<long>
                {
                    Name = roleName,
                };

                var result = await roleManager.CreateAsync(newRole);

                if (!result.Succeeded)
                {
                    return Results.BadRequest(result.Errors.Select(err => err.Description));
                }

                await roleManager.AddClaimAsync(newRole, new Claim(ClaimTypes.Country, "Poland"));

                return Results.Ok(newRole);
            });

            return builder;
        }

        private static async Task UpdateUserSessionsWithNewClaimsAsync(ITicketStore cacheSessionStore, IList<UserSession> sessions, IList<Claim> claims)
        {
            await Task.WhenAll(sessions.Select(async session =>
            {
                var authTicket = await cacheSessionStore.RetrieveAsync(session.SessionId);

                if (authTicket is null)
                {
                    return;
                }

                var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
                var newAuthTicket = new AuthenticationTicket(principal, authTicket.Properties, authTicket.AuthenticationScheme);

                await cacheSessionStore.RenewAsync(session.SessionId, newAuthTicket);
            }));
        }

        private static async Task<IList<Claim>> GenerateUserClaimsListAsync(User user, UserManager<User> userManager)
        {
            var claims = await userManager.GetClaimsAsync(user);
            var roles = await userManager.GetRolesAsync(user);

            foreach (var role in roles) 
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            claims.Add(new Claim(ClaimTypes.Name, user.FirstName));
            claims.Add(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));

            return claims;
        }
            
    }
}
