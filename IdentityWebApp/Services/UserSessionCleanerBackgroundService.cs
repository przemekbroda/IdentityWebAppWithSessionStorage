using IdentityWebApp.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityWebApp.Services
{
    public class UserSessionCleanerBackgroundService : BackgroundService
    {
        private readonly PeriodicTimer _timer = new(TimeSpan.FromMinutes(2));
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger _logger;

        public UserSessionCleanerBackgroundService(IServiceProvider serviceProvider, ILogger<UserSessionCleanerBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {

            while (await _timer.WaitForNextTickAsync(stoppingToken) && !stoppingToken.IsCancellationRequested)
            {
                await DoWork();
            }

            _logger.LogInformation("UserSessionCleanerBackgroundService execution canceled");
        }

        private async Task DoWork()
        {
            _logger.LogInformation("Work started");

            using var scope = _serviceProvider.CreateScope();
            var dataContext = scope.ServiceProvider.GetService<ApplicationDbContext>();

            if (dataContext is null) 
            {
                _logger.LogCritical("DataContext not found");
                return;
            }

            await dataContext
                .UserSessions
                .Where(userSession => userSession.ExpiresAt < DateTimeOffset.Now)
                .ExecuteDeleteAsync();

            _logger.LogInformation("Work finished");
        }
    }
}
