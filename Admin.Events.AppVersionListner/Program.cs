using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Admin.Events.AppVersionListner;

public class Program
{
    public static async Task Main(string[] args)
    {
        var host = CreateHostBuilder(args).Build();
        await host.RunAsync();
        
        Console.WriteLine("Exit");
    }

    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((hostingContext, config) =>
            {
                config.AddEnvironmentVariables();
            })
            .ConfigureServices((hostContext, services) =>
            {
                var configuration = hostContext.Configuration;
                var queueHost = configuration.GetValue<string>("QUEUE_HOST");
                var queue = configuration.GetValue<string>("QUEUE_NAME");
                var username = configuration.GetValue<string>("QUEUE_USERNAME");
                var password = configuration.GetValue<string>("QUEUE_PASSWORD");

                services.AddHostedService<Worker>(_ => new Worker(queueHost, queue, username, password));
            });
}