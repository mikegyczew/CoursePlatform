using Backend.Models;

namespace Backend.Services;

public sealed class MockPaymentService : IPaymentService
{
    public Task<bool> ProcessPaymentAsync(
        int userId,
        DropboxCouponType plan,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(true);
    }
}
