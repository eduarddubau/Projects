using Backend.DTOs.Dashboard;

namespace Backend.Services.Admin.Interfaces;

public interface IAdminDashboardService
{
    Task<AdminDashboardDto> GetAdminDashboardAsync(CancellationToken ct = default);
}
