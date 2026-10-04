using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Backend.Data;
using Backend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Backend.Services;

public sealed class StripePaymentService(
    HttpClient httpClient,
    CourseDbContext dbContext,
    IOptions<StripeOptions> options
) : IPaymentService
{
    private const string CheckoutSessionsEndpoint =
        "https://api.stripe.com/v1/checkout/sessions";
    private static readonly TimeSpan SignatureTolerance = TimeSpan.FromMinutes(5);
    private readonly StripeOptions _options = options.Value;

    public async Task<PaidAccessStatus> GetPaidAccessAsync(
        int userId,
        CancellationToken cancellationToken
    )
    {
        var now = DateTime.UtcNow;
        var expiries = await dbContext.DropboxPaidAccess
            .AsNoTracking()
            .Where(access => access.UserId == userId)
            .Select(access => access.ExpiresAt)
            .ToListAsync(cancellationToken);

        var hasPermanentAccess = expiries.Any(expiry => expiry is null);
        var latestExpiry = expiries
            .Where(expiry => expiry > now)
            .Max();
        return new PaidAccessStatus(
            hasPermanentAccess || latestExpiry is not null,
            hasPermanentAccess ? null : latestExpiry
        );
    }

    public async Task<string> CreateCheckoutSessionAsync(
        int userId,
        DropboxCouponType type,
        CancellationToken cancellationToken
    )
    {
        if (!_options.IsCheckoutConfigured)
        {
            throw new StripeConfigurationException(
                "Stripe checkout and webhook settings must be configured."
            );
        }

        var secretKey = RequireSetting(_options.SecretKey, "SecretKey");
        var frontendBaseUrl = RequireSetting(
            _options.FrontendBaseUrl,
            "FrontendBaseUrl"
        ).TrimEnd('/');
        var priceId = GetPriceId(type);
        var planName = GetPlanName(type);
        var successUrl = $"{frontendBaseUrl}/?payment=success";
        var cancelUrl = $"{frontendBaseUrl}/?payment=cancelled";

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            CheckoutSessionsEndpoint
        );
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer",
                secretKey
            );
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        request.Content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["mode"] = "payment",
                ["line_items[0][price]"] = priceId,
                ["line_items[0][quantity]"] = "1",
                ["success_url"] = successUrl,
                ["cancel_url"] = cancelUrl,
                ["client_reference_id"] = userId.ToString(CultureInfo.InvariantCulture),
                ["metadata[user_id]"] = userId.ToString(CultureInfo.InvariantCulture),
                ["metadata[plan]"] = planName,
                ["payment_intent_data[metadata][user_id]"] =
                    userId.ToString(CultureInfo.InvariantCulture),
                ["payment_intent_data[metadata][plan]"] = planName
            }
        );

        using var response = await httpClient.SendAsync(
            request,
            cancellationToken
        );
        var responseBody = await response.Content.ReadAsStringAsync(
            cancellationToken
        );
        if (!response.IsSuccessStatusCode)
        {
            throw new StripePaymentException(
                $"Stripe checkout creation failed with HTTP {(int)response.StatusCode}."
            );
        }

        using var document = JsonDocument.Parse(responseBody);
        if (
            !document.RootElement.TryGetProperty("url", out var urlElement)
            || string.IsNullOrWhiteSpace(urlElement.GetString())
        )
        {
            throw new StripePaymentException(
                "Stripe returned a checkout session without a URL."
            );
        }

        return urlElement.GetString()!;
    }

    public async Task ProcessWebhookAsync(
        string payload,
        string signature,
        CancellationToken cancellationToken
    )
    {
        VerifyWebhookSignature(payload, signature);

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        if (
            !root.TryGetProperty("type", out var eventType)
            || eventType.GetString() != "checkout.session.completed"
        )
        {
            return;
        }

        var session = root.GetProperty("data").GetProperty("object");
        if (
            !session.TryGetProperty("payment_status", out var paymentStatus)
            || paymentStatus.GetString() != "paid"
            || !session.TryGetProperty("mode", out var mode)
            || mode.GetString() != "payment"
        )
        {
            return;
        }

        var sessionId = GetRequiredString(session, "id");
        var userIdText = session
            .GetProperty("metadata")
            .GetProperty("user_id")
            .GetString();
        var planText = session
            .GetProperty("metadata")
            .GetProperty("plan")
            .GetString();
        if (
            !int.TryParse(userIdText, out var userId)
            || !Enum.TryParse<DropboxCouponType>(
                planText,
                ignoreCase: true,
                out var type
            )
            || type is not (
                DropboxCouponType.Week
                or DropboxCouponType.Month
                or DropboxCouponType.Forever
            )
        )
        {
            throw new StripePaymentException(
                "Stripe checkout metadata is invalid."
            );
        }

        if (await dbContext.DropboxPaidAccess.AnyAsync(
                access => access.StripeCheckoutSessionId == sessionId,
                cancellationToken
            ))
        {
            return;
        }

        var paidAt = DateTime.UtcNow;
        if (
            root.TryGetProperty("created", out var eventCreated)
            && eventCreated.TryGetInt64(out var unixTime)
        )
        {
            paidAt = DateTimeOffset
                .FromUnixTimeSeconds(unixTime)
                .UtcDateTime;
        }

        var accessRecord = new DropboxPaidAccess
        {
            StripeCheckoutSessionId = sessionId,
            UserId = userId,
            Type = type,
            PaidAt = paidAt,
            ExpiresAt = GetExpiry(type, paidAt)
        };
        dbContext.DropboxPaidAccess.Add(accessRecord);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            if (!await dbContext.DropboxPaidAccess.AnyAsync(
                    access => access.StripeCheckoutSessionId == sessionId,
                    cancellationToken
                ))
            {
                throw;
            }
        }
    }

    private void VerifyWebhookSignature(string payload, string signature)
    {
        var webhookSecret = RequireSetting(
            _options.WebhookSecret,
            "WebhookSecret"
        );
        string? timestampText = null;
        var signatures = new List<string>();

        foreach (var part in signature.Split(','))
        {
            var separator = part.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var key = part[..separator].Trim();
            var value = part[(separator + 1)..].Trim();
            if (key == "t")
            {
                timestampText = value;
            }
            else if (key == "v1")
            {
                signatures.Add(value);
            }
        }

        if (
            !long.TryParse(timestampText, out var timestamp)
            || signatures.Count == 0
        )
        {
            throw new StripeWebhookSignatureException(
                "Stripe webhook signature is malformed."
            );
        }

        DateTimeOffset signedAt;
        try
        {
            signedAt = DateTimeOffset.FromUnixTimeSeconds(timestamp);
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new StripeWebhookSignatureException(
                "Stripe webhook signature timestamp is invalid."
            );
        }
        if (DateTimeOffset.UtcNow - signedAt > SignatureTolerance
            || signedAt - DateTimeOffset.UtcNow > SignatureTolerance)
        {
            throw new StripeWebhookSignatureException(
                "Stripe webhook signature has expired."
            );
        }

        var signedPayload = $"{timestampText}.{payload}";
        var expectedSignature = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(webhookSecret),
            Encoding.UTF8.GetBytes(signedPayload)
        );

        foreach (var signatureValue in signatures)
        {
            try
            {
                var actualSignature = Convert.FromHexString(signatureValue);
                if (
                    actualSignature.Length == expectedSignature.Length
                    && CryptographicOperations.FixedTimeEquals(
                        actualSignature,
                        expectedSignature
                    )
                )
                {
                    return;
                }
            }
            catch (FormatException)
            {
                continue;
            }
        }

        throw new StripeWebhookSignatureException(
            "Stripe webhook signature is invalid."
        );
    }

    private string GetPriceId(DropboxCouponType type) =>
        RequireSetting(
            type switch
            {
                DropboxCouponType.Week => _options.WeekPriceId,
                DropboxCouponType.Month => _options.MonthPriceId,
                DropboxCouponType.Forever => _options.ForeverPriceId,
                _ => throw new ArgumentOutOfRangeException(nameof(type))
            },
            $"{type}PriceId"
        );

    private static string GetPlanName(DropboxCouponType type) =>
        type switch
        {
            DropboxCouponType.Week => "week",
            DropboxCouponType.Month => "month",
            DropboxCouponType.Forever => "forever",
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };

    private static DateTime? GetExpiry(
        DropboxCouponType type,
        DateTime paidAt
    ) =>
        type switch
        {
            DropboxCouponType.Week => paidAt.AddDays(7),
            DropboxCouponType.Month => paidAt.AddDays(30),
            DropboxCouponType.Forever => null,
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };

    private static string GetRequiredString(
        JsonElement element,
        string propertyName
    ) =>
        element.TryGetProperty(propertyName, out var property)
            && !string.IsNullOrWhiteSpace(property.GetString())
                ? property.GetString()!
                : throw new StripePaymentException(
                    $"Stripe webhook is missing '{propertyName}'."
                );

    private static string RequireSetting(string? value, string name) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new StripeConfigurationException(
                $"Stripe setting '{name}' is not configured."
            )
            : value;
}

public sealed class StripeConfigurationException(string message)
    : Exception(message);

public sealed class StripePaymentException(string message)
    : Exception(message);

public sealed class StripeWebhookSignatureException(string message)
    : Exception(message);
