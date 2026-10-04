using Backend.Models;

namespace Backend.Services;

public interface IPaymentService
{
    Task<bool> ProcessPaymentAsync(
        int userId,
        DropboxCouponType plan,
        CancellationToken cancellationToken
    );
}
