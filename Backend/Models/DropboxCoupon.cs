namespace Backend.Models;

public class DropboxCoupon
{
    public int Id { get; set; }

    public required string CodeHash { get; set; }

    public string? CoursePath { get; set; }

    public DropboxCouponType Type { get; set; }

    public bool IsSharedTest { get; set; }

    public bool IsRedeemed { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
