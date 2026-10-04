using Backend.Models;

namespace Backend.Services;

public sealed class StripeOptions
{
    public string? SecretKey { get; set; }
    public string? WebhookSecret { get; set; }
    public string? FrontendBaseUrl { get; set; }
    public string? WeekPriceId { get; set; }
    public string? MonthPriceId { get; set; }
    public string? ForeverPriceId { get; set; }

    public bool IsCheckoutConfigured =>
        !string.IsNullOrWhiteSpace(SecretKey)
        && !string.IsNullOrWhiteSpace(FrontendBaseUrl)
        && !string.IsNullOrWhiteSpace(WebhookSecret);

    public bool IsPriceConfigured(DropboxCouponType type) =>
        type switch
        {
            DropboxCouponType.Week => !string.IsNullOrWhiteSpace(WeekPriceId),
            DropboxCouponType.Month => !string.IsNullOrWhiteSpace(MonthPriceId),
            DropboxCouponType.Forever => !string.IsNullOrWhiteSpace(ForeverPriceId),
            _ => false
        };
}
