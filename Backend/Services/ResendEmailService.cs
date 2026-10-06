using Microsoft.Extensions.Options;
using Resend;

namespace Backend.Services;

public sealed class ResendEmailService(
    IResend resend,
    IOptions<EmailOptions> options,
    ILogger<ResendEmailService> logger
) : IEmailService
{
    private readonly EmailOptions _options = options.Value;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.ResendApiKey)
        && !string.IsNullOrWhiteSpace(_options.FromAddress);

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
                "Resend email settings are incomplete."
            );
        }

        var message = new EmailMessage
        {
            From = string.IsNullOrWhiteSpace(_options.FromName)
                ? _options.FromAddress!
                : $"{_options.FromName} <{_options.FromAddress}>",
            Subject = subject,
            HtmlBody = htmlBody
        };
        message.To.Add(recipient);

        try
        {
            var response = await resend.EmailSendAsync(
                message,
                cancellationToken
            );

            if (!response.Success)
            {
                Exception exception = response.Exception is { } responseException
                    ? responseException
                    : new InvalidOperationException(
                        "Resend returned an unsuccessful response without an error."
                    );
                logger.LogError(exception, "Resend email delivery failed.");
                throw new EmailDeliveryException(
                    "The email could not be delivered.",
                    exception
                );
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ResendException exception)
        {
            logger.LogError(exception, "Resend email delivery failed.");
            throw new EmailDeliveryException(
                "The email could not be delivered.",
                exception
            );
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "Resend email request failed.");
            throw new EmailDeliveryException(
                "The email could not be delivered.",
                exception
            );
        }
    }
}
