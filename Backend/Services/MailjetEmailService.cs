using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Backend.Services;

public sealed class MailjetEmailService(
    IHttpClientFactory httpClientFactory,
    IOptions<EmailOptions> options,
    ILogger<MailjetEmailService> logger
) : IEmailService
{
    private static readonly Uri SendEndpoint = new(
        "https://api.mailjet.com/v3.1/send"
    );

    private readonly EmailOptions _options = options.Value;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.MailjetApiKey)
        && !string.IsNullOrWhiteSpace(_options.MailjetApiSecret)
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
                "Mailjet email settings are incomplete."
            );
        }

        var client = httpClientFactory.CreateClient();
        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes(
                $"{_options.MailjetApiKey}:{_options.MailjetApiSecret}"
            )
        );
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            SendEndpoint
        );
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            credentials
        );
        request.Content = JsonContent.Create(new MailjetSendRequest
        {
            Messages =
            [
                new MailjetMessage
                {
                    From = new MailjetAddress
                    {
                        Email = _options.FromAddress!,
                        Name = _options.FromName
                    },
                    To = [new MailjetAddress { Email = recipient }],
                    Subject = subject,
                    HtmlPart = htmlBody
                }
            ]
        });

        try
        {
            using var response = await client.SendAsync(
                request,
                cancellationToken
            );
            var responseBody = await response.Content.ReadAsStringAsync(
                cancellationToken
            );

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError(
                    "Mailjet returned HTTP {StatusCode}: {ResponseBody}",
                    (int)response.StatusCode,
                    responseBody
                );
                throw new EmailDeliveryException(
                    $"Mailjet rejected the email request with HTTP {(int)response.StatusCode}."
                );
            }

            var result = JsonSerializer.Deserialize<MailjetSendResponse>(
                responseBody
            );
            if (result?.Messages is not { Count: > 0 } messages
                || messages.Any(message => message.Status != "success"))
            {
                logger.LogError(
                    "Mailjet returned an unsuccessful email result: {ResponseBody}",
                    responseBody
                );
                throw new EmailDeliveryException(
                    "Mailjet did not accept the email for delivery."
                );
            }

            var recipientResult = messages[0].To?.FirstOrDefault();
            logger.LogInformation(
                "Mailjet accepted email for processing. MessageId={MessageId}, MessageUuid={MessageUuid}. This does not confirm final delivery.",
                recipientResult?.MessageId,
                recipientResult?.MessageUuid
            );
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "Mailjet email request failed.");
            throw new EmailDeliveryException(
                "The email could not be delivered.",
                exception
            );
        }
        catch (JsonException exception)
        {
            logger.LogError(exception, "Mailjet returned an invalid response.");
            throw new EmailDeliveryException(
                "Mailjet returned an invalid response.",
                exception
            );
        }
    }

    private sealed class MailjetSendRequest
    {
        [JsonPropertyName("Messages")]
        public required List<MailjetMessage> Messages { get; init; }
    }

    private sealed class MailjetMessage
    {
        [JsonPropertyName("From")]
        public required MailjetAddress From { get; init; }

        [JsonPropertyName("To")]
        public required List<MailjetAddress> To { get; init; }

        [JsonPropertyName("Subject")]
        public required string Subject { get; init; }

        [JsonPropertyName("HTMLPart")]
        public required string HtmlPart { get; init; }
    }

    private sealed class MailjetAddress
    {
        [JsonPropertyName("Email")]
        public required string Email { get; init; }

        [JsonPropertyName("Name")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Name { get; init; }
    }

    private sealed class MailjetSendResponse
    {
        [JsonPropertyName("Messages")]
        public List<MailjetMessageResult>? Messages { get; init; }
    }

    private sealed class MailjetMessageResult
    {
        [JsonPropertyName("Status")]
        public string? Status { get; init; }

        [JsonPropertyName("To")]
        public List<MailjetRecipientResult>? To { get; init; }
    }

    private sealed class MailjetRecipientResult
    {
        [JsonPropertyName("MessageID")]
        public long? MessageId { get; init; }

        [JsonPropertyName("MessageUUID")]
        public string? MessageUuid { get; init; }
    }
}
