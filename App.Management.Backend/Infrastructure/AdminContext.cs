using Microsoft.EntityFrameworkCore;

namespace Admin.Api.Infrastructure;

public class AdminContext(DbContextOptions options) : Microsoft.EntityFrameworkCore.DbContext(options)
{
    public DbSet<Application> Applications { get; init; }
}