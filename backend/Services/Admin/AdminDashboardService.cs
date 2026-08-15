using Backend.Data;
using Backend.DTOs.Dashboard;
using Backend.Mappings;
using Backend.Services.Admin.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services.Admin;

public class AdminDashboardService : IAdminDashboardService
{
    private readonly AppDbContext _context;

    public AdminDashboardService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<AdminDashboardDto> GetAdminDashboardAsync(CancellationToken ct = default)
    {
        var activeProjectCount = await _context.Projects.CountAsync(ct);

        // Admin trash has no retention cutoff — deleted projects stay until purged.
        var deletedProjectCount = await _context
            .Projects.IgnoreQueryFilters()
            .CountAsync(p => p.IsDeleted, ct);

        var activeUserCount = await _context.Users.CountAsync(ct);

        // Matches the users trash: anonymized accounts are hidden there.
        var deletedUserCount = await _context
            .Users.IgnoreQueryFilters()
            .CountAsync(u => u.IsDeleted && !u.IsAnonymized, ct);

        var recentProjects = await _context
            .Projects.OrderByDescending(p => p.CreatedAt)
            .Take(5)
            .MapToDto()
            .ToListAsync(ct);

        var recentUsers = await _context
            .Users.OrderByDescending(u => u.CreatedAt)
            .Take(5)
            .MapToDto()
            .ToListAsync(ct);

        return new AdminDashboardDto
        {
            ActiveProjectCount = activeProjectCount,
            DeletedProjectCount = deletedProjectCount,
            ActiveUserCount = activeUserCount,
            DeletedUserCount = deletedUserCount,
            RecentProjects = recentProjects,
            RecentUsers = recentUsers,
        };
    }
}
