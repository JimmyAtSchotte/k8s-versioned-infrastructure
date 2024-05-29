using Admin.Api.Services;
using Admin.DbContext;

var configuration = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .Build();

var builder = WebApplication.CreateBuilder(args);

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

app.MapPost("/applications", (AddApplicationCommand command, AdminContext db, RabbitMqService rabbitMqService) =>
{
    var app = new Application()
        {
            Image = command.Image,
            Name = command.Name
        };

    db.Applications.Add(app);
    db.SaveChanges();
    
    rabbitMqService.SendMessage(System.Text.Json.JsonSerializer.Serialize(app));
    
    return new AppplicationResponse()
    {
        Name = app.Name,
        Image = app.Image
    };
});

app.MapPost("/applications/{name}", (string name, UpdateImageCommand command, AdminContext db, RabbitMqService rabbitMqService) =>
{
    var app = db.Applications.FirstOrDefault(x => x.Name == name);
    app.Image = command.Image;
    db.SaveChanges();
    
    rabbitMqService.SendMessage(System.Text.Json.JsonSerializer.Serialize(app));
    
    return new AppplicationResponse()
    {
        Name = app.Name,
        Image = app.Image
    };
});


app.Run();

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