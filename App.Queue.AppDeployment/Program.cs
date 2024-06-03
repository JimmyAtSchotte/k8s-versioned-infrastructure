using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Admin.Events.AppVersionListner;

public class Program
{
    public static async Task Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Seq("http://seq.default.svc.cluster.local:5341")
            .MinimumLevel.Information()
            .CreateLogger();
        
        var host = CreateHostBuilder(args).Build();
        await host.RunAsync();
        
        Log.Information("Exit");
    }

    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((hostingContext, config) =>
            {
                config.AddEnvironmentVariables();
            })
            .ConfigureLogging(logger => logger
                .AddSerilog()
                .AddConsole()
            )
            .ConfigureServices((hostContext, services) =>
            {
                services.Configure<WorkerOptions>(options =>
                {
                    options.QueueHost =  hostContext.Configuration["QUEUE_HOST"];
                    options.Queue =  hostContext.Configuration["QUEUE_NAME"];
                    options.Username =  hostContext.Configuration["QUEUE_USERNAME"];
                    options.Password =  hostContext.Configuration["QUEUE_PASSWORD"];
                });
                
                services.AddOptions<WorkerOptions>();
                services.AddHostedService<Worker>();
            });
}