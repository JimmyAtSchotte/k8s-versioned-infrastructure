using Admin.Components;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Seq("http://seq.default.svc.cluster.local:5341")
    .MinimumLevel.Information()
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddSerilog();

builder.Configuration.AddEnvironmentVariables();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpClient("ADMIN_API", (provider, client) =>
{
    var configuration = provider.GetService<IConfiguration>();

    client.BaseAddress = new Uri(configuration.GetValue<string>("ADMIN_API_URL"));
}); 

builder.Services.AddHttpClient("REGISTRY_API", (provider, client) =>
{
    var configuration = provider.GetService<IConfiguration>();

    client.BaseAddress = new Uri(configuration.GetValue<string>("REGISTRY_API_URL"));
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();