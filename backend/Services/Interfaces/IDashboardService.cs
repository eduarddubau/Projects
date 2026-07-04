using Backend.DTOs.Dashboard;

namespace Backend.Services.Interfaces;

public interface IDashboardService
{
    Task<UserDashboardDto> GetMyDashboardAsync(CancellationToken ct = default);
    Task<AdminDashboardDto> GetAdminDashboardAsync(CancellationToken ct = default);
}
