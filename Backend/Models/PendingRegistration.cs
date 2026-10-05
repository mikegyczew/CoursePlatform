namespace Backend.Models;

public sealed class PendingRegistration
{
    public int Id { get; set; }

    public required string Email { get; set; }

    public required string Name { get; set; }

    public required string ConfirmationTokenHash { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
