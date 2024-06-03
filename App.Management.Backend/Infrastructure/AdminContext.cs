using Microsoft.EntityFrameworkCore;

namespace App.Management.Backend.Infrastructure;

public class AdminContext(DbContextOptions options) : Microsoft.EntityFrameworkCore.DbContext(options)
{
    public DbSet<Application> Applications { get; init; }
}