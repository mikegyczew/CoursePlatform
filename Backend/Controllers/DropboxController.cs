using System.Security.Cryptography;
using System.Text;
using Backend.DTOs;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Route("api/dropbox")]
public sealed class DropboxController(
    IDropboxService dropboxService,
    ILogger<DropboxController> logger
) : ControllerBase
{
    private const string StateCookieName = "DropboxOAuthState";

    /// <summary>Starts the one-time Dropbox authorization flow.</summary>
    [AllowAnonymous]
    [HttpGet("connect")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public IActionResult Connect(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var state = Convert.ToBase64String(
                RandomNumberGenerator.GetBytes(32)
            )
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        Response.Cookies.Append(
            StateCookieName,
            state,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                MaxAge = TimeSpan.FromMinutes(10),
                Path = "/api/dropbox"
            }
        );

        try
        {
            return Redirect(dropboxService.CreateAuthorizationUrl(state));
        }
        catch (InvalidOperationException exception)
        {
            logger.LogWarning(exception, "Dropbox OAuth is not configured.");
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Dropbox is not configured."
            );
        }
    }

    /// <summary>
    /// Completes Dropbox authorization and returns a refresh token once.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("callback")]
    [ProducesResponseType(typeof(DropboxRefreshTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<DropboxRefreshTokenResponse>> Callback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        CancellationToken cancellationToken
    )
    {
        Response.Cookies.Delete(
            StateCookieName,
            new CookieOptions { Path = "/api/dropbox" }
        );

        if (!string.IsNullOrWhiteSpace(error))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Dropbox authorization was not completed."
            );
        }

        var expectedState = Request.Cookies[StateCookieName];
        if (
            string.IsNullOrWhiteSpace(code)
            || string.IsNullOrWhiteSpace(state)
            || string.IsNullOrWhiteSpace(expectedState)
            || !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(state),
                Encoding.UTF8.GetBytes(expectedState)
            )
        )
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Dropbox authorization state is invalid or expired."
            );
        }

        try
        {
            var refreshToken =
                await dropboxService.ExchangeAuthorizationCodeAsync(
                    code,
                    cancellationToken
                );

            Response.Headers.CacheControl = "no-store";
            Response.Headers.Pragma = "no-cache";

            return Ok(new DropboxRefreshTokenResponse(
                refreshToken,
                "Copy the refresh token to the Dropbox__RefreshToken secret in Render. Do not share it."
            ));
        }
        catch (InvalidOperationException exception)
        {
            logger.LogWarning(
                exception,
                "Dropbox OAuth could not be completed."
            );
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Dropbox OAuth is not configured correctly."
            );
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(
                exception,
                "Dropbox token exchange failed."
            );
            return Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "Dropbox token exchange failed."
            );
        }
    }

    /// <summary>Checks the Dropbox account connected to this backend.</summary>
    [Authorize]
    [HttpGet("connection")]
    [ProducesResponseType(typeof(DropboxAccountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<DropboxAccountResponse>> GetConnection(
        CancellationToken cancellationToken
    )
    {
        try
        {
            return Ok(
                await dropboxService.GetAccountAsync(cancellationToken)
            );
        }
        catch (InvalidOperationException exception)
        {
            logger.LogWarning(
                exception,
                "Dropbox connection is not configured."
            );
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Dropbox connection is not configured."
            );
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(
                exception,
                "Dropbox connection check failed."
            );
            return Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "Dropbox connection check failed."
            );
        }
    }
}
