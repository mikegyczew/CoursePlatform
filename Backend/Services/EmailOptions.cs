namespace Backend.Services;

public sealed class EmailOptions
{
    public string? ResendApiKey { get; set; }

    public string? FromAddress { get; set; }

    public string FromName { get; set; } = "CoursePlatform";

    public string? FrontendUrl { get; set; }
}
