using System.ComponentModel.DataAnnotations;

namespace Backend.DTOs;

public sealed record ForgotPasswordDto
{
    [Required]
    [EmailAddress]
    [MaxLength(320)]
    public required string Email { get; init; }
}
