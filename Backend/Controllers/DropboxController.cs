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
        catch (DropboxConfigurationException exception)
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
        catch (DropboxConfigurationException exception)
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
        catch (DropboxApiException exception)
        {
            return DropboxFailure(exception, "Dropbox token exchange failed.");
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
        catch (DropboxConfigurationException exception)
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
        catch (DropboxApiException exception)
        {
            return DropboxFailure(exception, "Dropbox connection check failed.");
        }
    }

    /// <summary>Lists lesson folders inside the configured Dropbox course.</summary>
    [Authorize]
    [HttpGet("courses/{courseId:int}/lessons")]
    [ProducesResponseType(typeof(IReadOnlyList<DropboxLessonResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<IReadOnlyList<DropboxLessonResponse>>> GetLessons(
        int courseId,
        CancellationToken cancellationToken
    )
    {
        if (courseId != -1)
        {
            return NotFound();
        }

        try
        {
            return Ok(
                await dropboxService.GetLessonsAsync(cancellationToken)
            );
        }
        catch (DropboxConfigurationException exception)
        {
            logger.LogWarning(exception, "Dropbox lessons are not configured.");
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Dropbox lessons are not configured."
            );
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Dropbox lesson listing failed.");
            return Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "Could not list Dropbox lessons."
            );
        }
        catch (DropboxApiException exception)
        {
            return DropboxFailure(exception, "Dropbox lesson listing failed.");
        }
    }

    /// <summary>Returns a Dropbox lesson and its text and media materials.</summary>
    [Authorize]
    [HttpGet("courses/{courseId:int}/lessons/{lessonId:int}")]
    [ProducesResponseType(typeof(DropboxLessonResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<DropboxLessonResponse>> GetLesson(
        int courseId,
        int lessonId,
        CancellationToken cancellationToken
    )
    {
        if (courseId != -1)
        {
            return NotFound();
        }

        try
        {
            var lesson = await dropboxService.GetLessonAsync(
                lessonId,
                cancellationToken
            );
            return lesson is null ? NotFound() : Ok(lesson);
        }
        catch (DropboxConfigurationException exception)
        {
            logger.LogWarning(exception, "Dropbox lesson is not configured.");
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Dropbox lesson is not configured."
            );
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Dropbox lesson retrieval failed.");
            return Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "Could not retrieve the Dropbox lesson."
            );
        }
        catch (DropboxApiException exception)
        {
            return DropboxFailure(exception, "Dropbox lesson retrieval failed.");
        }
        catch (DropboxLessonContentException exception)
        {
            logger.LogWarning(
                exception,
                "Dropbox lesson content could not be read."
            );
            return Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "Lesson PDF could not be converted to text.",
                detail: "Check that content.pdf is a valid PDF with selectable text. Scanned PDFs need OCR."
            );
        }
    }

    /// <summary>Gets a short-lived URL to display or download a lesson file.</summary>
    [Authorize]
    [HttpGet("courses/{courseId:int}/lessons/{lessonId:int}/files/{fileId:int}/link")]
    [ProducesResponseType(typeof(DropboxTemporaryLinkResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<DropboxTemporaryLinkResponse>> GetFileLink(
        int courseId,
        int lessonId,
        int fileId,
        CancellationToken cancellationToken
    )
    {
        if (courseId != -1)
        {
            return NotFound();
        }

        try
        {
            var url = await dropboxService.GetTemporaryLinkAsync(
                lessonId,
                fileId,
                cancellationToken
            );
            return url is null
                ? NotFound()
                : Ok(new DropboxTemporaryLinkResponse(url));
        }
        catch (DropboxConfigurationException exception)
        {
            logger.LogWarning(exception, "Dropbox file link is not configured.");
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Dropbox file access is not configured."
            );
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Dropbox file link request failed.");
            return Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "Could not get a Dropbox file link."
            );
        }
        catch (DropboxApiException exception)
        {
            return DropboxFailure(exception, "Dropbox file link request failed.");
        }
    }

    private ObjectResult DropboxFailure(
        DropboxApiException exception,
        string logMessage
    )
    {
        logger.LogWarning(exception, "{DropboxOperation}", logMessage);

        if (
            exception.ErrorSummary.Contains(
                "missing_scope",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Dropbox app permissions are incomplete.",
                detail: "Enable the required Dropbox API scope, authorize the app again, and update Dropbox__RefreshToken in the hosting environment."
            );
        }

        if (exception.StatusCode == StatusCodes.Status401Unauthorized)
        {
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Dropbox authorization is invalid or expired.",
                detail: "Reconnect the Dropbox app and update Dropbox__RefreshToken in the hosting environment."
            );
        }

        if (
            exception.ErrorSummary.Contains(
                "path/not_found",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Dropbox course folder was not found.",
                detail: "Check that Dropbox__RootFolder points to an existing folder inside the connected app folder; for example, /Ekonomia."
            );
        }

        if (
            exception.ErrorSummary.Contains(
                "no_permission",
                StringComparison.OrdinalIgnoreCase
            )
            || exception.StatusCode == StatusCodes.Status403Forbidden
        )
        {
            return Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Dropbox does not allow access to this folder.",
                detail: "Check the Dropbox app access type and folder sharing permissions."
            );
        }

        return Problem(
            statusCode: StatusCodes.Status502BadGateway,
            title: "Dropbox rejected the request.",
            detail: "Check the backend logs for the Dropbox error and request ID."
        );
    }
}
