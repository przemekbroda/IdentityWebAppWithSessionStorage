using IdentityWebApp.Dtos;
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
            app.MapPost("user/authenticate", async (string email, string password, SignInManager<User> signInManager, UserManager<User> userManager, HttpContext context) =>
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

                await context.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme, 
                    new ClaimsPrincipal(
                        new ClaimsIdentity(
                            new Claim[]
                            {
                                new Claim(ClaimTypes.Name, user.FirstName),
                                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                            },
                            CookieAuthenticationDefaults.AuthenticationScheme
                        )
                    ),
                    new AuthenticationProperties
                    { 
                        IsPersistent = true,
                        AllowRefresh = true,
                    }
                );

                return Results.Ok();
            });

            app.MapPost("user/logout", async (HttpContext context) =>
            {
                await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

                return Results.Ok();
            });

            app.MapGet("/user/{userId}", async (long userId, UserManager<User> userManager, HttpContext context) =>
            {
                var user = await userManager.FindByIdAsync(userId.ToString());

                if (user is null)
                {
                    return Results.NotFound();
                }

                return Results.Ok(user);
            }).RequireAuthorization();

            app.MapPost("/user", async (CreateUserDto createUser, UserManager<User> userManager) =>
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

            app.MapGet("/user/{userId}/role", async (UserManager<User> userManager, long userId) =>
            {
                var user = await userManager.FindByIdAsync(userId.ToString());

                if (user is null)
                {
                    return Results.NotFound();
                }

                var roles = await userManager.GetRolesAsync(user);

                return Results.Ok(roles);
            });

            app.MapGet("/role", async (RoleManager<IdentityRole<long>> roleManager) =>
            {
                var roles = await roleManager.Roles.ToListAsync();
                return Results.Ok(roles);
            });

            app.MapGet("/role/{roleName}", async (string roleName, RoleManager<IdentityRole<long>> roleManager) =>
            {
                var role = await roleManager.FindByNameAsync(roleName);

                if (role is null)
                {
                    return Results.NotFound();
                }

                return Results.Ok(role);
            });

            app.MapPost("/role", async (string roleName, RoleManager<IdentityRole<long>> roleManager) =>
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
        }
    }
}
