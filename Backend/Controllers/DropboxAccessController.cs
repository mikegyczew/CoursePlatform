using System.IdentityModel.Tokens.Jwt;
using Backend.DTOs;
using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Backend.Controllers;

[ApiController]
[Authorize]
[Route("api/dropbox/access")]
public sealed class DropboxAccessController(
    DropboxCouponService couponService,
    IPaymentService paymentService,
    IOptions<DropboxPricingOptions> pricingOptions
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

    [HttpGet("plans")]
    [ProducesResponseType(typeof(DropboxPurchasePlansResponse), StatusCodes.Status200OK)]
    public ActionResult<DropboxPurchasePlansResponse> GetPurchasePlans()
    {
        var pricing = pricingOptions.Value;
        return Ok(new DropboxPurchasePlansResponse(
            WeekAvailable: true,
            MonthAvailable: true,
            ForeverAvailable: true,
            WeekPricePln: pricing.WeeklyPricePln,
            MonthPricePln: pricing.MonthlyPricePln,
            ForeverPricePln: pricing.ForeverPricePln
        ));
    }

    [HttpPost("purchase")]
    [ProducesResponseType(typeof(DropboxTrialAccessResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status402PaymentRequired)]
    public async Task<ActionResult<DropboxTrialAccessResponse>> Purchase(
        [FromBody] DropboxPurchaseRequest request,
        CancellationToken cancellationToken
    )
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var type = request.Type?.Trim().ToLowerInvariant() switch
        {
            "week" => DropboxCouponType.Week,
            "month" => DropboxCouponType.Month,
            "forever" => DropboxCouponType.Forever,
            _ => (DropboxCouponType?)null
        };
        if (type is null)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Wybierz tydzień, miesiąc lub dostęp bezterminowy."
            );
        }

        var paymentSucceeded = await paymentService.ProcessPaymentAsync(
            userId.Value,
            type.Value,
            cancellationToken
        );
        if (!paymentSucceeded)
        {
            return Problem(
                statusCode: StatusCodes.Status402PaymentRequired,
                title: "Płatność nie została zaakceptowana."
            );
        }

        var access = await couponService.CreateAndRedeemPurchaseCouponAsync(
            userId.Value,
            type.Value,
            cancellationToken
        );
        Response.Headers.CacheControl = "no-store";
        return Ok(access);
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
