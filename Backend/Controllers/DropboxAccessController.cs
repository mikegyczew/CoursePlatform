using System.IdentityModel.Tokens.Jwt;
using Backend.DTOs;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Authorize]
[Route("api/dropbox/access")]
public sealed class DropboxAccessController(
    DropboxCouponService couponService
) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(DropboxTrialAccessResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<DropboxTrialAccessResponse>> GetAccess(
        CancellationToken cancellationToken
    )
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        return Ok(await couponService.GetStatusAsync(
            userId.Value,
            cancellationToken
        ));
    }

    [HttpPost("redeem")]
    [ProducesResponseType(typeof(DropboxTrialAccessResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DropboxTrialAccessResponse>> RedeemCoupon(
        [FromBody] DropboxCouponRequest request,
        CancellationToken cancellationToken
    )
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.Coupon))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Wprowadź kod kuponu."
            );
        }

        var result = await couponService.RedeemAsync(
            userId.Value,
            request.Coupon,
            cancellationToken
        );

        return result.Status switch
        {
            DropboxCouponRedemptionStatus.Redeemed => Ok(result.Access),
            DropboxCouponRedemptionStatus.AlreadyRedeemed => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Kupon został już wykorzystany."
            ),
            _ => Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Kupon jest nieprawidłowy."
            )
        };
    }

    private int? GetUserId()
    {
        var claim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return int.TryParse(claim, out var userId) ? userId : null;
    }
}
