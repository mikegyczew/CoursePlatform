namespace Backend.Services;

public interface IPaymentService
{
    Task<bool> HasPaidAccessAsync(
        int userId,
        CancellationToken cancellationToken
    );
}
