namespace Backend.Services;

public sealed class DropboxOptions
{
    public string? AppKey { get; set; }
    public string? AppSecret { get; set; }
    public string? RedirectUri { get; set; }
    public string? RefreshToken { get; set; }
    public string RootFolder { get; set; } = "/";
    public string? CouponAdminKey { get; set; }
}
