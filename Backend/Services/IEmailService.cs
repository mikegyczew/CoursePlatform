namespace Backend.Services;

public interface IEmailService
{
    bool IsConfigured { get; }

    Task SendAsync(
        string recipient,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken
    );
}
