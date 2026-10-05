namespace Backend.Services;

public sealed class EmailOptions
{
    public string? SmtpHost { get; set; }

    public int SmtpPort { get; set; } = 587;

    public string? SmtpUsername { get; set; }

    public string? SmtpPassword { get; set; }

    public bool EnableSsl { get; set; } = true;

    public string? FromAddress { get; set; }

    public string FromName { get; set; } = "CoursePlatform";

    public string? FrontendUrl { get; set; }
}
