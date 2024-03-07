using IdentityWebApp.Common.Interfaces;
using IdentityWebApp.Endpoints;
using IdentityWebApp.Entities;
using IdentityWebApp.Persistence;
using IdentityWebApp.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Net;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDistributedMemoryCache();

//builder.Services.AddStackExchangeRedisCache(options =>
//{
//    options.Configuration = builder.Configuration.GetConnectionString("Redis");
//});

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});

builder.Services.AddHttpContextAccessor();

builder.Services.AddDataProtection()
    .PersistKeysToDbContext<ApplicationDbContext>();

builder.Services.AddHostedService<UserSessionCleanerBackgroundService>();

builder.Services.AddIdentityCore<User>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.Password.RequireNonAlphanumeric = true;
})
    .AddSignInManager()
    .AddRoles<IdentityRole<long>>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
    .Configure<ITicketStore, IConfiguration>((options, store, configuration) =>
    {
        options.SessionStore = store;
        options.SlidingExpiration = true;
        options.Cookie.Name = builder.Configuration.GetValue<string>("Session:CookieName");
        options.ExpireTimeSpan = TimeSpan.FromMinutes(configuration.GetValue<double>("Session:SessionDurationInMinutes"));
        
        options.Events.OnRedirectToLogin = (context) =>
        {
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            return Task.CompletedTask;
        };

        options.Events.OnRedirectToAccessDenied = (context) =>
        {
            context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
            return Task.CompletedTask;
        };
    });

builder.Services.AddSingleton<ITicketStore, CacheSessionStore>();
builder.Services.AddScoped<IApplicationDbContext>(serviceProvider => serviceProvider.GetRequiredService<ApplicationDbContext>());

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie();

builder.Services.AddAuthorization(config => 
{
    config.AddPolicy("admin", builder => builder.RequireAuthenticatedUser().RequireRole("ADMIN"));
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();

app.UseAuthorization();

app.AddUserManagementEndpoints();

app.Run();
