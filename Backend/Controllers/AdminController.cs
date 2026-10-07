using Backend.DTOs;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

/// <summary>Administrative database inspection and account management.</summary>
[ApiController]
[Authorize(Roles = "SuperAdmin")]
[Route("api/admin")]
public sealed class AdminController(IAdminService adminService) : ControllerBase
{
    /// <summary>Returns users, courses, coupon access and lesson progress.</summary>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(AdminDashboardResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AdminDashboardResponse>> GetDashboard(
        CancellationToken cancellationToken
    ) =>
        Ok(await adminService.GetDashboardAsync(cancellationToken));

    /// <summary>Deletes a user and database records related to that account.</summary>
    [HttpDelete("users/{userId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteUser(
        int userId,
        CancellationToken cancellationToken
    )
    {
        if (userId <= 0)
        {
            return NotFound();
        }

        return await adminService.DeleteUserAsync(userId, cancellationToken)
            ? NoContent()
            : NotFound();
    }

    /// <summary>Revokes access granted by a coupon redemption.</summary>
    [HttpDelete("access-grants/{redemptionId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeAccess(
        int redemptionId,
        CancellationToken cancellationToken
    )
    {
        if (redemptionId <= 0)
        {
            return NotFound();
        }

        return await adminService.RevokeAccessAsync(
            redemptionId,
            cancellationToken
        )
            ? NoContent()
            : NotFound();
    }
}
