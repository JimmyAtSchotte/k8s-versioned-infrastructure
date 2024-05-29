using Microsoft.EntityFrameworkCore;

namespace Admin.DbContext;

public class AdminContext(DbContextOptions options) : Microsoft.EntityFrameworkCore.DbContext(options)
{
    public DbSet<Application> Applications { get; init; }
}