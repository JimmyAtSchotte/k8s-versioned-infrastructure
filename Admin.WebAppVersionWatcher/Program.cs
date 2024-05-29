using Admin.DbContext;
using Admin.WebAppVersionWatcher;
using MongoDB.Driver;

var configuration = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .Build();

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddEnvironmentVariables();

builder.Services.AddHostedService<Worker>();
builder.Services.AddAdminDbContext(configuration.GetValue<string>("DB_USERNAME"),
configuration.GetValue<string>("DB_PASSWORD"),
configuration.GetValue<string>("DB_SERVER"),
configuration.GetValue<string>("DB_DATABASE"));

builder.Services.AddLogging(logging => logging.AddConsole());

var host = builder.Build();
host.Run();