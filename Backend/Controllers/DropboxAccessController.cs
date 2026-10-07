using System.IdentityModel.Tokens.Jwt;
using System.Net;
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
    IOptions<DropboxPricingOptions> pricingOptions,
    ILogger<DropboxAccessController> logger
) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(DropboxTrialAccessResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<DropboxTrialAccessResponse>> GetAccess(
        [FromQuery] int courseId,
        CancellationToken cancellationToken
    )
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        try
        {
            return Ok(await couponService.GetStatusAsync(
                userId.Value,
                courseId,
                cancellationToken
            ));
        }
        catch (DropboxCourseNotFoundException)
        {
            return NotFound();
        }
        catch (DropboxConfigurationException exception)
        {
            return DropboxFailure(exception, "Dropbox course lookup is not configured.");
        }
        catch (HttpRequestException exception)
        {
            return DropboxFailure(exception, "Dropbox course lookup failed.");
        }
        catch (DropboxApiException exception)
        {
            return DropboxFailure(exception, "Dropbox course lookup failed.");
        }
    }

    [HttpGet("plans")]
    [ProducesResponseType(typeof(DropboxPurchasePlansResponse), StatusCodes.Status200OK)]
    public ActionResult<DropboxPurchasePlansResponse> GetPurchasePlans()
    {
        var pricing = pricingOptions.Value;
        var isSuperAdmin = User.IsInRole("SuperAdmin");
        return Ok(new DropboxPurchasePlansResponse(
            WeekAvailable: !isSuperAdmin,
            MonthAvailable: !isSuperAdmin,
            ForeverAvailable: !isSuperAdmin,
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

        DropboxCourseLocation course;
        try
        {
            course = await couponService.GetCourseLocationAsync(
                request.CourseId,
                cancellationToken
            );
        }
        catch (DropboxCourseNotFoundException)
        {
            return NotFound();
        }
        catch (DropboxConfigurationException exception)
        {
            return DropboxFailure(exception, "Dropbox course lookup is not configured.");
        }
        catch (HttpRequestException exception)
        {
            return DropboxFailure(exception, "Dropbox course lookup failed.");
        }
        catch (DropboxApiException exception)
        {
            return DropboxFailure(exception, "Dropbox course lookup failed.");
        }

        if (userId.Value == SuperAdminOptions.UserId)
        {
            Response.Headers.CacheControl = "no-store";
            return Ok(await couponService.GetStatusAsync(
                userId.Value,
                request.CourseId,
                cancellationToken
            ));
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

        try
        {
            var access = await couponService.CreateAndRedeemPurchaseCouponAsync(
                userId.Value,
                course,
                type.Value,
                cancellationToken
            );
            Response.Headers.CacheControl = "no-store";
            return Ok(access);
        }
        catch (DropboxCourseNotFoundException)
        {
            return NotFound();
        }
        catch (DropboxConfigurationException exception)
        {
            return DropboxFailure(exception, "Dropbox course lookup is not configured.");
        }
        catch (HttpRequestException exception)
        {
            return DropboxFailure(exception, "Dropbox course lookup failed.");
        }
        catch (DropboxApiException exception)
        {
            return DropboxFailure(exception, "Dropbox course lookup failed.");
        }
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

        DropboxCouponRedemptionResult result;
        try
        {
            result = await couponService.RedeemAsync(
                userId.Value,
                request.CourseId,
                request.Coupon,
                cancellationToken
            );
        }
        catch (DropboxCourseNotFoundException)
        {
            return NotFound();
        }
        catch (DropboxConfigurationException exception)
        {
            return DropboxFailure(exception, "Dropbox course lookup is not configured.");
        }
        catch (HttpRequestException exception)
        {
            return DropboxFailure(exception, "Dropbox course lookup failed.");
        }
        catch (DropboxApiException exception)
        {
            return DropboxFailure(exception, "Dropbox course lookup failed.");
        }

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

    private ObjectResult DropboxFailure(Exception exception, string operation)
    {
        logger.LogWarning(exception, "{DropboxOperation}", operation);

        if (exception is DropboxConfigurationException)
        {
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Dropbox is not configured."
            );
        }

        if (exception is HttpRequestException)
        {
            return Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "Could not connect to Dropbox."
            );
        }

        var apiException = (DropboxApiException)exception;
        if (
            apiException.ErrorSummary.Contains(
                "missing_scope",
                StringComparison.OrdinalIgnoreCase
            )
            || apiException.StatusCode == StatusCodes.Status401Unauthorized
        )
        {
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Dropbox authorization is invalid or incomplete."
            );
        }

        if (
            apiException.ErrorSummary.Contains(
                "path/not_found",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Dropbox course folder was not found."
            );
        }

        if (
            apiException.ErrorSummary.Contains(
                "no_permission",
                StringComparison.OrdinalIgnoreCase
            )
            || apiException.StatusCode == StatusCodes.Status403Forbidden
        )
        {
            return Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Dropbox does not allow access to this course."
            );
        }

        return Problem(
            statusCode: StatusCodes.Status502BadGateway,
            title: "Dropbox rejected the request."
        );
    }
}
