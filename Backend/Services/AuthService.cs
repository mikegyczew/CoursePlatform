using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using Backend.Data;
using Backend.DTOs;
using Backend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Backend.Services;

public class AuthService
{
    private readonly CourseDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly IEmailService _emailService;

    public AuthService(
        CourseDbContext dbContext,
        IConfiguration configuration,
        IEmailService emailService)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _emailService = emailService;
    }

    public async Task<bool> RegisterAsync(
        RegisterDto dto,
        CancellationToken cancellationToken
    )
    {
        var email = dto.Email.Trim().ToLowerInvariant();
        if (IsSuperAdminEmail(email))
        {
            return false;
        }

        var exists = await _dbContext.Users
            .AnyAsync(user => user.Email == email, cancellationToken);

        if (exists)
        {
            return false;
        }

        await _dbContext.PendingRegistrations
            .Where(item => item.ExpiresAt <= DateTime.UtcNow)
            .ExecuteDeleteAsync(cancellationToken);

        var token = CreateConfirmationToken();
        var registration = await _dbContext.PendingRegistrations
            .SingleOrDefaultAsync(
                item => item.Email == email,
                cancellationToken
            );
        if (registration is null)
        {
            registration = new PendingRegistration
            {
                Email = email,
                Name = dto.Name.Trim(),
                ConfirmationTokenHash = HashToken(token),
                ExpiresAt = DateTime.UtcNow.AddHours(24)
            };
            _dbContext.PendingRegistrations.Add(registration);
        }
        else
        {
            registration.Name = dto.Name.Trim();
            registration.ConfirmationTokenHash = HashToken(token);
            registration.ExpiresAt = DateTime.UtcNow.AddHours(24);
            registration.CreatedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var frontendUrl = _configuration["Email:FrontendUrl"];
        if (string.IsNullOrWhiteSpace(frontendUrl))
        {
            throw new EmailDeliveryException(
                "The frontend URL for email confirmation is not configured."
            );
        }

        var confirmationUrl =
            $"{frontendUrl.TrimEnd('/')}/#confirmEmail={Uri.EscapeDataString(token)}";
        await _emailService.SendAsync(
            email,
            "Potwierdź adres email",
            $"<p>Cześć {System.Net.WebUtility.HtmlEncode(registration.Name)},</p>"
                + "<p>Aby dokończyć rejestrację, potwierdź swój adres email:</p>"
                + $"<p><a href=\"{System.Net.WebUtility.HtmlEncode(confirmationUrl)}\">Potwierdź adres email</a></p>"
                + "<p>Link jest ważny przez 24 godziny.</p>",
            cancellationToken
        );

        return true;
    }

    public async Task<AuthResponseDto?> ConfirmRegistrationAsync(
        string token,
        string password,
        CancellationToken cancellationToken
    )
    {
        var tokenHash = HashToken(token);
        await using var transaction = await _dbContext.Database
            .BeginTransactionAsync(cancellationToken);
        var registration = await _dbContext.PendingRegistrations
            .SingleOrDefaultAsync(
                item =>
                    item.ConfirmationTokenHash == tokenHash
                    && item.ExpiresAt > DateTime.UtcNow,
                cancellationToken
            );
        if (registration is null)
        {
            return null;
        }

        if (await _dbContext.Users.AnyAsync(
                user => user.Email == registration.Email,
                cancellationToken
            ))
        {
            _dbContext.PendingRegistrations.Remove(registration);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        var user = new User
        {
            Email = registration.Email,
            Name = registration.Name,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            EmailConfirmed = true
        };

        _dbContext.Users.Add(user);
        _dbContext.PendingRegistrations.Remove(registration);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return CreateAuthResponse(user);
    }

    public async Task RequestPasswordResetAsync(
        string emailAddress,
        CancellationToken cancellationToken
    )
    {
        var email = emailAddress.Trim().ToLowerInvariant();
        if (IsSuperAdminEmail(email))
        {
            return;
        }

        var user = await _dbContext.Users.SingleOrDefaultAsync(
            item => item.Email == email && item.EmailConfirmed,
            cancellationToken
        );
        if (user is null)
        {
            return;
        }

        await _dbContext.PasswordResetTokens
            .Where(token => token.ExpiresAt <= DateTime.UtcNow)
            .ExecuteDeleteAsync(cancellationToken);
        await _dbContext.PasswordResetTokens
            .Where(token => token.UserId == user.Id)
            .ExecuteDeleteAsync(cancellationToken);

        var token = CreateConfirmationToken();
        _dbContext.PasswordResetTokens.Add(new PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = HashToken(token),
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        });
        await _dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var frontendUrl = _configuration["Email:FrontendUrl"];
            if (string.IsNullOrWhiteSpace(frontendUrl))
            {
                throw new EmailDeliveryException(
                    "The frontend URL for password reset is not configured."
                );
            }

            var resetUrl =
                $"{frontendUrl.TrimEnd('/')}/#resetPassword={Uri.EscapeDataString(token)}";
            await _emailService.SendAsync(
                email,
                "Zresetuj hasło do CoursePlatform",
                $"<p>Cześć {System.Net.WebUtility.HtmlEncode(user.Name)},</p>"
                    + "<p>Otrzymaliśmy prośbę o zmianę hasła do Twojego konta.</p>"
                    + $"<p><a href=\"{System.Net.WebUtility.HtmlEncode(resetUrl)}\">Ustaw nowe hasło</a></p>"
                    + "<p>Link jest ważny przez godzinę. Jeśli to nie Ty wysłałeś prośbę, zignoruj tę wiadomość.</p>",
                cancellationToken
            );
        }
        catch (Exception exception) when (
            exception is EmailDeliveryException or OperationCanceledException
        )
        {
            await _dbContext.PasswordResetTokens
                .Where(resetToken => resetToken.UserId == user.Id)
                .ExecuteDeleteAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<bool> ResetPasswordAsync(
        string token,
        string password,
        CancellationToken cancellationToken
    )
    {
        var tokenHash = HashToken(token);
        var now = DateTime.UtcNow;
        var resetToken = await _dbContext.PasswordResetTokens
            .Where(item =>
                item.TokenHash == tokenHash && item.ExpiresAt > now
            )
            .Select(item => new { item.Id, item.UserId })
            .SingleOrDefaultAsync(cancellationToken);
        if (resetToken is null)
        {
            return false;
        }

        await using var transaction = await _dbContext.Database
            .BeginTransactionAsync(cancellationToken);
        var deleted = await _dbContext.PasswordResetTokens
            .Where(item =>
                item.Id == resetToken.Id && item.ExpiresAt > DateTime.UtcNow
            )
            .ExecuteDeleteAsync(cancellationToken);
        if (deleted != 1)
        {
            return false;
        }

        var user = await _dbContext.Users.SingleOrDefaultAsync(
            item => item.Id == resetToken.UserId && item.EmailConfirmed,
            cancellationToken
        );
        if (user is null)
        {
            return false;
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<AuthResponseDto?> LoginAsync(
        LoginDto dto,
        CancellationToken cancellationToken
    )
    {
        var email = dto.Email.Trim().ToLowerInvariant();
        var adminEmail = _configuration["SuperAdmin:Email"]?
            .Trim()
            .ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(adminEmail) && email == adminEmail)
        {
            var adminPassword = _configuration["SuperAdmin:Password"];
            if (!IsSecretEqual(dto.Password, adminPassword))
            {
                return null;
            }

            return CreateAuthResponse(
                new User
                {
                    Id = SuperAdminOptions.UserId,
                    Email = adminEmail,
                    Name = "Superadmin",
                    PasswordHash = string.Empty
                },
                "SuperAdmin"
            );
        }

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(
                user => user.Email == email && user.EmailConfirmed,
                cancellationToken
            );

        if (user is null)
        {
            return null;
        }

        var passwordValid = BCrypt.Net.BCrypt.Verify(
            dto.Password,
            user.PasswordHash
        );

        if (!passwordValid)
        {
            return null;
        }

        return CreateAuthResponse(user);
    }

    private AuthResponseDto CreateAuthResponse(
        User user,
        string role = "User"
    )
    {
        var key = _configuration["Jwt:Key"]
            ?? throw new InvalidOperationException(
                "JWT key is not configured."
            );

        var issuer = _configuration["Jwt:Issuer"]
            ?? "CoursePlatform";

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(
                JwtRegisteredClaimNames.Email,
                user.Email
            ),
            new Claim(ClaimTypes.Name, user.Name),
            new Claim("role", role)
        };

        var securityKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(key)
        );

        var credentials = new SigningCredentials(
            securityKey,
            SecurityAlgorithms.HmacSha256
        );

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: issuer,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(12),
            signingCredentials: credentials
        );

        return new AuthResponseDto
        {
            Token = new JwtSecurityTokenHandler()
                .WriteToken(token),

            UserId = user.Id,
            Email = user.Email,
            Name = user.Name,
            Role = role
        };
    }

    private bool IsSuperAdminEmail(string email) =>
        string.Equals(
            email,
            _configuration["SuperAdmin:Email"]?.Trim(),
            StringComparison.OrdinalIgnoreCase
        );

    private static string CreateConfirmationToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static string HashToken(string token) =>
        Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(token))
        );

    private static bool IsSecretEqual(string supplied, string? configured)
    {
        if (string.IsNullOrEmpty(configured))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(supplied),
            Encoding.UTF8.GetBytes(configured)
        );
    }
}
