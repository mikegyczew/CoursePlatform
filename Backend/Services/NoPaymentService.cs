namespace Backend.Services;

public sealed class NoPaymentService : IPaymentService
{
    public Task<bool> HasPaidAccessAsync(
        int userId,
        CancellationToken cancellationToken
    ) => Task.FromResult(false);
}
