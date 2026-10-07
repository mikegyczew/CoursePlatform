using System.ComponentModel.DataAnnotations;

namespace Backend.DTOs;

public sealed record ResetPasswordDto
{
    [Required]
    public required string Token { get; init; }

    [Required]
    [MinLength(6)]
    [MaxLength(100)]
    public required string Password { get; init; }
}
