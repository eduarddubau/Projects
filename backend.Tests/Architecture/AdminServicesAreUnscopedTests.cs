using Backend.Services.Admin;
using Backend.Services.Interfaces;

namespace Backend.Tests.Architecture;

/// <summary>An admin service reaches every row, so a caller to scope by could only
/// be a mistake. Enforced here rather than by a comment, which no constructor reads.</summary>
public class AdminServicesAreUnscopedTests
{
    private static IEnumerable<Type> AdminServiceTypes =>
        typeof(AdminUserService)
            .Assembly.GetTypes()
            .Where(t =>
                t.IsClass && !t.IsAbstract && t.Namespace == typeof(AdminUserService).Namespace
            );

    [Fact]
    public void NoAdminServiceTakesTheCurrentUser()
    {
        var offenders = AdminServiceTypes
            .Where(t =>
                t.GetConstructors()
                    .SelectMany(c => c.GetParameters())
                    .Any(p => p.ParameterType == typeof(ICurrentUserService))
            )
            .Select(t => t.Name)
            .ToList();

        Assert.Empty(offenders);
    }

    /// <summary>Guards the guard: a namespace filter matching nothing would pass
    /// the test above forever.</summary>
    [Fact]
    public void TheAdminNamespaceIsBeingScanned()
    {
        Assert.Contains(typeof(AdminProjectService), AdminServiceTypes);
        Assert.Contains(typeof(AdminUserService), AdminServiceTypes);
        Assert.Contains(typeof(AdminWorkspaceService), AdminServiceTypes);
        Assert.Contains(typeof(AdminDashboardService), AdminServiceTypes);
    }
}
