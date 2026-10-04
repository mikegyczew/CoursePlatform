using Backend.Models;

namespace Backend.Services;

public interface IPaymentService
{
    Task<PaidAccessStatus> GetPaidAccessAsync(
        int userId,
        CancellationToken cancellationToken
    );

    Task<string> CreateCheckoutSessionAsync(
        int userId,
        DropboxCouponType type,
        CancellationToken cancellationToken
    );

    Task ProcessWebhookAsync(
        string payload,
        string signature,
        CancellationToken cancellationToken
    );
}

public sealed record PaidAccessStatus(bool HasAccess, DateTime? ExpiresAt);
