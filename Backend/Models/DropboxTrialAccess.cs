namespace Backend.Models;

public class DropboxTrialAccess
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public DateTime RedeemedAt { get; set; }
}
