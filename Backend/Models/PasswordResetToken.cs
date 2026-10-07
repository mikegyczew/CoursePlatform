namespace Backend.Models;

public sealed class PasswordResetToken
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public required string TokenHash { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}
