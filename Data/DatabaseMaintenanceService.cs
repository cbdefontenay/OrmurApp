namespace Ormur.Data;

public class DatabaseMaintenanceService(
    IServiceProvider services,
    ILogger<DatabaseMaintenanceService> logger)
    : BackgroundService
{
    private readonly TimeSpan _vacuumInterval = TimeSpan.FromHours(6);
    private readonly TimeSpan _vacuumDelayAfterDeletion = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = services.CreateScope();
                var dbConnector = scope.ServiceProvider.GetRequiredService<SqliteConnector>();
                
                if (await ShouldPerformVacuum(dbConnector))
                {
                    await dbConnector.VacuumDatabaseAsync();
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during database maintenance");
            }
            
            await Task.Delay(_vacuumInterval, stoppingToken);
        }
    }

    private Task<bool> ShouldPerformVacuum(SqliteConnector dbConnector)
    {
        const int deletionThreshold = 20; // Vacuum after this many deletions
        return Task.FromResult(dbConnector.GetPendingVacuumCount() >= deletionThreshold);
    }
}