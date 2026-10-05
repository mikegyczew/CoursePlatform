using Backend.Data;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace Backend.Services;

public sealed class CouponExpiryEmailService(
    IServiceScopeFactory scopeFactory,
    IEmailService emailService,
    ILogger<CouponExpiryEmailService> logger
) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(15);
    private const int BatchSize = 100;

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken
    )
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (emailService.IsConfigured)
                {
                    await SendExpiredCouponNotificationsAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Coupon expiry notification pass failed."
                );
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task SendExpiredCouponNotificationsAsync(
        CancellationToken cancellationToken
    )
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<CourseDbContext>();
        var now = DateTime.UtcNow;
        await dbContext.PendingRegistrations
            .Where(registration => registration.ExpiresAt <= now)
            .ExecuteDeleteAsync(cancellationToken);
        var lastId = 0;
        while (true)
        {
            var expired = await dbContext.DropboxCouponRedemptions
                .AsNoTracking()
                .Where(redemption =>
                    redemption.Id > lastId
                    && redemption.ExpiresAt != null
                    && redemption.ExpiresAt <= now
                    && redemption.NotifyOnExpiry
                    && redemption.ExpiryNotificationSentAt == null
                )
                .OrderBy(redemption => redemption.Id)
                .Select(redemption => new
                {
                    redemption.Id,
                    redemption.ExpiresAt,
                    redemption.Coupon.CoursePath,
                    redemption.User.Email
                })
                .Take(BatchSize)
                .ToListAsync(cancellationToken);
            if (expired.Count == 0)
            {
                return;
            }

            foreach (var redemption in expired)
            {
                lastId = redemption.Id;
                try
                {
                    var expiry = redemption.ExpiresAt!.Value;
                    await emailService.SendAsync(
                        redemption.Email,
                        "Dostęp do kursu wygasł",
                        $"<p>Dostęp do kursu <strong>{WebUtility.HtmlEncode(redemption.CoursePath ?? "Dropbox")}</strong> "
                            + $"wygasł {expiry:yyyy-MM-dd HH:mm} UTC.</p>"
                            + "<p>Aby kontynuować naukę, zaloguj się i wykup dostęp ponownie.</p>",
                        cancellationToken
                    );

                    await dbContext.DropboxCouponRedemptions
                        .Where(item =>
                            item.Id == redemption.Id
                            && item.ExpiryNotificationSentAt == null
                        )
                        .ExecuteUpdateAsync(
                            setters => setters.SetProperty(
                                item => item.ExpiryNotificationSentAt,
                                DateTime.UtcNow
                            ),
                            cancellationToken
                        );
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    logger.LogError(
                        exception,
                        "Could not send coupon expiry email for redemption {RedemptionId}.",
                        redemption.Id
                    );
                }
            }
        }
    }
}
