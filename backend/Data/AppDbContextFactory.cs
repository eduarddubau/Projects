using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Backend.Data;

// Used by EF Core CLI tools to create an instance of AppDbContext at design time
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings__DefaultConnection env variable is not set.");

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

        optionsBuilder
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention();

        return new AppDbContext(optionsBuilder.Options, new DesignTimeUserService());
    }
}

// Dummy implementation to satisfy AppDbContext constructor during migrations
public class DesignTimeUserService : Services.ICurrentUserService
{
    public string UserId => "migration-runner"; // FIX #3: removed ?
    public Guid? UserGuid => Guid.Parse("00000000-0000-0000-0000-000000000001");
    public bool IsAuthenticated => false;
    public bool IsAdmin => false;
    public string? Email => null;
    public string? FirstName => null;
    public string? LastName => null;
    public string? FullName => null;
    public IEnumerable<string> Roles => Enumerable.Empty<string>();
}