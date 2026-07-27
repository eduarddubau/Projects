using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Backend.Data;

// Used by EF Core CLI tools to create an instance of AppDbContext at design time
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        // Resolves the connection the same way Program.cs does, so design time and
        // runtime can't drift; the container's env var still wins over the JSON.
        var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? "Development";

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddUserSecrets<AppDbContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "No DefaultConnection configured. Set it once with: dotnet user-secrets set "
                + "\"ConnectionStrings:DefaultConnection\" \"<connection string>\"");

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

        optionsBuilder
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention();

        return new AppDbContext(optionsBuilder.Options, new DesignTimeUserService());
    }
}

// Dummy implementation to satisfy AppDbContext constructor during migrations
public class DesignTimeUserService : Services.Interfaces.ICurrentUserService
{
    public string UserId => "migration-runner";
    public Guid? UserGuid => Guid.Parse("00000000-0000-0000-0000-000000000001");
    public bool IsAuthenticated => false;
    public bool IsAdmin => false;
    public string? Email => null;
    public string? FirstName => null;
    public string? LastName => null;
    public string? FullName => null;
    public IEnumerable<string> Roles => Enumerable.Empty<string>();
}
