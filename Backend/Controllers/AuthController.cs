using Backend.DTOs;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        AuthService authService,
        ILogger<AuthController> logger
    )
    {
        _authService = authService;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(
        RegisterDto dto,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var registered = await _authService.RegisterAsync(
                dto,
                cancellationToken
            );
            if (!registered)
            {
                return Conflict(
                    "Użytkownik z takim adresem email już istnieje."
                );
            }

            return Accepted(new
            {
                message = "Zgłoszenie wysyłki linku potwierdzającego zostało przyjęte."
            });
        }
        catch (EmailDeliveryException exception)
        {
            _logger.LogError(
                exception,
                "Registration confirmation email failed."
            );
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Nie udało się wysłać wiadomości potwierdzającej."
            );
        }
    }

    [HttpPost("confirm-email")]
    public async Task<ActionResult<AuthResponseDto>> ConfirmEmail(
        [FromBody] ConfirmEmailDto dto,
        CancellationToken cancellationToken
    )
    {
        var result = await _authService.ConfirmRegistrationAsync(
            dto.Token,
            dto.Password,
            cancellationToken
        );

        if (result is not null)
        {
            Response.Headers.CacheControl = "no-store";
        }

        return result is null
            ? Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Link potwierdzający jest nieprawidłowy lub wygasł."
            )
            : Ok(result);
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordDto dto,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await _authService.RequestPasswordResetAsync(
                dto.Email,
                cancellationToken
            );
        }
        catch (EmailDeliveryException exception)
        {
            _logger.LogError(
                exception,
                "Password reset email delivery failed."
            );
        }

        return Accepted(new
        {
            message = "Jeśli konto z tym adresem istnieje, wyślemy na niego link do zmiany hasła."
        });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordDto dto,
        CancellationToken cancellationToken
    )
    {
        var reset = await _authService.ResetPasswordAsync(
            dto.Token,
            dto.Password,
            cancellationToken
        );
        return reset
            ? Ok(new { message = "Hasło zostało zmienione. Możesz się zalogować." })
            : Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Link do zmiany hasła jest nieprawidłowy lub wygasł."
            );
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login(
        LoginDto dto,
        CancellationToken cancellationToken
    )
    {
        var result = await _authService.LoginAsync(dto, cancellationToken);

        if (result is null)
        {
            return Unauthorized(
                "Nieprawidłowy email lub hasło."
            );
        }

        return Ok(result);
    }

    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
    {
        return Ok(new
        {
            Id = User.FindFirst(
                System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub
            )?.Value,

            Email = User.FindFirst(
                System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email
            )?.Value,

            Name = User.Identity?.Name
        });
    }
}
