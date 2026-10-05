using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace Backend.Services;

public sealed class SmtpEmailService(
    IOptions<EmailOptions> options,
    ILogger<SmtpEmailService> logger
) : IEmailService
{
    private readonly EmailOptions _options = options.Value;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.SmtpHost)
        && !string.IsNullOrWhiteSpace(_options.FromAddress)
        && _options.SmtpPort is >= 1 and <= 65535;

    public async Task SendAsync(
        string recipient,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken
    )
    {
        if (!IsConfigured)
        {
            throw new EmailDeliveryException(
                "SMTP email settings are incomplete."
            );
        }

        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(_options.FromAddress!, _options.FromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            message.To.Add(new MailAddress(recipient));

            using var client = new SmtpClient(
                _options.SmtpHost!,
                _options.SmtpPort
            )
            {
                EnableSsl = _options.EnableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false
            };
            if (!string.IsNullOrWhiteSpace(_options.SmtpUsername))
            {
                client.Credentials = new NetworkCredential(
                    _options.SmtpUsername,
                    _options.SmtpPassword
                );
            }

            await client.SendMailAsync(message, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is SmtpException or FormatException
        )
        {
            logger.LogError(exception, "SMTP email delivery failed.");
            throw new EmailDeliveryException(
                "The email could not be delivered.",
                exception
            );
        }
    }
}
