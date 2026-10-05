namespace Backend.Models;

public class DropboxCouponRedemption
{
    public int Id { get; set; }

    public int CouponId { get; set; }
    public DropboxCoupon Coupon { get; set; } = null!;

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public DateTime RedeemedAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public bool NotifyOnExpiry { get; set; }

    public DateTime? ExpiryNotificationSentAt { get; set; }
}
