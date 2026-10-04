using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.Json;
using Backend.DTOs;
using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Backend.Controllers;

[ApiController]
[Route("api/payments/stripe")]
public sealed class DropboxPaymentsController(
    IPaymentService paymentService,
    IOptions<StripeOptions> stripeOptions,
    ILogger<DropboxPaymentsController> logger
) : ControllerBase
{
    [Authorize]
    [HttpPost("checkout")]
    [ProducesResponseType(typeof(DropboxCheckoutResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<DropboxCheckoutResponse>> CreateCheckout(
        [FromBody] DropboxCheckoutRequest request,
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

        try
        {
            var checkoutUrl = await paymentService.CreateCheckoutSessionAsync(
                userId.Value,
                type.Value,
                cancellationToken
            );
            Response.Headers.CacheControl = "no-store";
            return Ok(new DropboxCheckoutResponse(checkoutUrl));
        }
        catch (StripeConfigurationException exception)
        {
            logger.LogWarning(exception, "Stripe checkout is not configured.");
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Zakup nie jest jeszcze skonfigurowany."
            );
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Stripe checkout request failed.");
            return Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "Nie udało się połączyć ze Stripe."
            );
        }
        catch (StripePaymentException exception)
        {
            logger.LogWarning(exception, "Stripe checkout could not be created.");
            return Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "Stripe nie utworzył sesji płatności."
            );
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Stripe returned invalid checkout data.");
            return Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "Stripe zwrócił nieprawidłową odpowiedź."
            );
        }
    }

    [Authorize]
    [HttpGet("plans")]
    [ProducesResponseType(typeof(DropboxPurchasePlansResponse), StatusCodes.Status200OK)]
    public ActionResult<DropboxPurchasePlansResponse> GetPlans()
    {
        var options = stripeOptions.Value;
        var checkoutReady = options.IsCheckoutConfigured;
        return Ok(new DropboxPurchasePlansResponse(
            checkoutReady && options.IsPriceConfigured(DropboxCouponType.Week),
            checkoutReady && options.IsPriceConfigured(DropboxCouponType.Month),
            checkoutReady && options.IsPriceConfigured(DropboxCouponType.Forever)
        ));
    }

    [AllowAnonymous]
    [HttpPost("webhook")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Webhook(CancellationToken cancellationToken)
    {
        if (!Request.Headers.TryGetValue("Stripe-Signature", out var signature))
        {
            return BadRequest();
        }

        using var reader = new StreamReader(
            Request.Body,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            leaveOpen: true
        );
        var payload = await reader.ReadToEndAsync(cancellationToken);

        try
        {
            await paymentService.ProcessWebhookAsync(
                payload,
                signature.ToString(),
                cancellationToken
            );
            return Ok();
        }
        catch (StripeWebhookSignatureException exception)
        {
            logger.LogWarning(exception, "Rejected invalid Stripe webhook.");
            return BadRequest();
        }
        catch (StripeConfigurationException exception)
        {
            logger.LogError(exception, "Stripe webhook secret is not configured.");
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Stripe webhook is not configured."
            );
        }
        catch (StripePaymentException exception)
        {
            logger.LogWarning(exception, "Stripe webhook payload was invalid.");
            return BadRequest();
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Stripe webhook JSON was invalid.");
            return BadRequest();
        }
    }

    private int? GetUserId()
    {
        var claim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return int.TryParse(claim, out var userId) ? userId : null;
    }
}
