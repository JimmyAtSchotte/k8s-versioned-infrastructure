using Admin.DbContext;
using Microsoft.EntityFrameworkCore;

namespace Admin.WebAppVersionWatcher;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly AdminContext _db;

    public Worker(ILogger<Worker> logger, AdminContext db)
    {
        _logger = logger;
        _db = db;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Check for updates [{date}]", DateTime.Now);
            
            var applications = _db.Applications.AsNoTracking().ToList();

            foreach (var application in applications)
                UpdateIngressConfiguration(application);
            
            await Task.Delay(1000, stoppingToken);
        }
    }
    
    private void UpdateIngressConfiguration(Application application)
    {
        _logger.LogInformation($"Update {application.Name} to {application.Image}");
    }

}

