using Backend.DTOs;

namespace Backend.Services;

public interface IAdminService
{
    Task<AdminDashboardResponse> GetDashboardAsync(
        CancellationToken cancellationToken
    );

    Task<bool> DeleteUserAsync(
        int userId,
        CancellationToken cancellationToken
    );

    Task<bool> RevokeAccessAsync(
        int redemptionId,
        CancellationToken cancellationToken
    );
}
