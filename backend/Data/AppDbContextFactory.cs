using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Backend.Data;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
{
    var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
    
    // Get the connection string from the environment variable set in the Dockerfile
    var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");

    optionsBuilder.UseNpgsql(connectionString);

    // Provide the dummy service so the constructor is satisfied
    return new AppDbContext(optionsBuilder.Options, new DesignTimeUserService());
}
}

// Dummy service just for the CLI to not crash
public class DesignTimeUserService : backend.Services.ICurrentUserService
{
    public string? UserId => "Migration-Runner";
}