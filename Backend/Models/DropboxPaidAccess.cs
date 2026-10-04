namespace Backend.Models;

public class DropboxPaidAccess
{
    public int Id { get; set; }

    public required string StripeCheckoutSessionId { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public DropboxCouponType Type { get; set; }

    public DateTime PaidAt { get; set; }

    public DateTime? ExpiresAt { get; set; }
}
