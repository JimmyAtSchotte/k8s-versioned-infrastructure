using Admin.Api;
using Admin.Api.Infrastructure;
using Admin.Api.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Seq("http://seq.default.svc.cluster.local:5341")
    .MinimumLevel.Information()
    .CreateLogger();

var configuration = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .Build();

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddSerilog();

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddAdminDbContext(configuration.GetValue<string>("DB_USERNAME"),
configuration.GetValue<string>("DB_PASSWORD"),
configuration.GetValue<string>("DB_SERVER"),
configuration.GetValue<string>("DB_DATABASE"));

Console.WriteLine();

builder.Services.AddSingleton<RabbitMqService>(sp => new RabbitMqService(
configuration.GetValue<string>("QUEUE_HOST"), 
configuration.GetValue<string>("QUEUE_NAME"),
configuration.GetValue<string>("QUEUE_USERNAME"),
configuration.GetValue<string>("QUEUE_PASSWORD")));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/applications", (AdminContext db) => db.Applications.ToList().Select(x => new AppplicationResponse()
    {
        Name = x.Name,
        Image = x.Image
    }))
    .WithName("GetApplications")
    .WithOpenApi();

app.MapPost("/applications", async (AddApplicationCommand command, AdminContext db, RabbitMqService rabbitMqService, ILogger<Program> logger) =>
{
    logger.LogInformation("Create app {appName} using version {version}", command.Name, command.Image);
    
    var app = new Application()
    {
        Image = command.Image,
        Name = command.Name
    };

    db.Applications.Add(app);
    await db.SaveChangesAsync();
    
    var queueMessage = new QueueMessage<Application>()
    {
        Action = "Create",
        Version = "1",
        Data = app
    };
    
    rabbitMqService.SendMessage(System.Text.Json.JsonSerializer.Serialize(queueMessage));
    
    
    return new AppplicationResponse()
    {
        Name = app.Name,
        Image = app.Image
    };
});

app.MapPost("/applications/{name}", async (string name, UpdateImageCommand command, AdminContext db, RabbitMqService rabbitMqService, ILogger<Program> logger) =>
{
    logger.LogInformation("Update app {appName} using version {version}", name, command.Image);
    
    var app = await  db.Applications.FirstOrDefaultAsync(x => x.Name == name);
    
    if (app == null)
    {
        logger.LogInformation("{appName} was not found", name);
        return Results.NotFound(new { Message = "Application not found" });
    }
    
    app.Image = command.Image;
    
    await db.SaveChangesAsync();

    var queueMessage = new QueueMessage<Application>()
    {
        Action = "Update",
        Version = "1",
        Data = app
    };
    
    rabbitMqService.SendMessage(System.Text.Json.JsonSerializer.Serialize(queueMessage));
    
    return Results.Ok(new AppplicationResponse()
    {
        Name = app.Name,
        Image = app.Image
    });
});


app.MapDelete("/applications/{name}", async (string name, AdminContext db, RabbitMqService rabbitMqService, ILogger<Program> logger) =>
{
    logger.LogInformation("Delete app {appName} ", name);
    
    var app = await db.Applications.FirstOrDefaultAsync(x => x.Name == name);
    
    if (app == null)
    {
        logger.LogInformation("{appName} was not found", name);
        return Results.NotFound(new { Message = "Application not found" });
    }

    db.Applications.Remove(app);
    await db.SaveChangesAsync();

    var queueMessage = new QueueMessage<Application>
    {
        Action = "Delete",
        Version = "1",
        Data = app
    };

    rabbitMqService.SendMessage(System.Text.Json.JsonSerializer.Serialize(queueMessage));

    return Results.NoContent();
});


app.Run();

namespace Admin.Api
{
    public class AppplicationResponse
    {
        public string Name { get; set; }
        public string Image { get; set; }
    }

    public class AddApplicationCommand
    {
        public string Name { get; set; }
        public string Image { get; set; }
    }

    public class UpdateImageCommand
    {
        public string Image { get; set; }
    }

    public class QueueMessage<T>
    {
        public string Action { get; set; }
        public string Version { get; set; }
        public T Data { get; set; }
    }
}