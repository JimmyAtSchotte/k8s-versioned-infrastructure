using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Admin.DbContext;

public static class SeviceProvider
{
    public static IServiceCollection AddAdminDbContext(this IServiceCollection services, string username, string password,
        string server, string database)
    {
        var connectionString = $"mongodb://{username}:{password}@{server}/?retryWrites=true&w=majority";
        
        services.AddDbContext<AdminContext>(options =>
        {
            options.UseMongoDB(connectionString, database);
        });

        return services;
    }
}