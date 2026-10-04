using System.Security.Cryptography;
using System.Text;
using Backend.Data;
using Backend.DTOs;
using Backend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Backend.Services;

public sealed class DropboxTrialAccessService(
    CourseDbContext dbContext,
    IOptions<DropboxOptions> options
)
{
    private static readonly TimeSpan TrialDuration = TimeSpan.FromHours(24);
    private readonly DropboxOptions _options = options.Value;

    public async Task<DropboxTrialAccessResponse> GetStatusAsync(
        int userId,
        CancellationToken cancellationToken
    )
    {
        var access = await dbContext.DropboxTrialAccess
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.UserId == userId,
                cancellationToken
            );

        return ToResponse(access);
    }

    public async Task<DropboxCouponRedemptionResult> RedeemAsync(
        int userId,
        string coupon,
        CancellationToken cancellationToken
    )
    {
        if (!IsValidCoupon(coupon))
        {
            return new DropboxCouponRedemptionResult(
                DropboxCouponRedemptionStatus.Invalid,
                null
            );
        }

        var existing = await dbContext.DropboxTrialAccess
            .SingleOrDefaultAsync(
                item => item.UserId == userId,
                cancellationToken
            );
        if (existing is not null)
        {
            return new DropboxCouponRedemptionResult(
                DropboxCouponRedemptionStatus.AlreadyRedeemed,
                ToResponse(existing)
            );
        }

        var access = new DropboxTrialAccess
        {
            UserId = userId,
            RedeemedAt = DateTime.UtcNow
        };
        dbContext.DropboxTrialAccess.Add(access);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            var concurrentRedemption = await dbContext.DropboxTrialAccess
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.UserId == userId,
                    cancellationToken
                );
            if (concurrentRedemption is null)
            {
                throw;
            }

            return new DropboxCouponRedemptionResult(
                DropboxCouponRedemptionStatus.AlreadyRedeemed,
                ToResponse(concurrentRedemption)
            );
        }

        return new DropboxCouponRedemptionResult(
            DropboxCouponRedemptionStatus.Redeemed,
            ToResponse(access)
        );
    }

    private bool IsValidCoupon(string coupon)
    {
        var configuredCoupon = _options.TrialCoupon;
        if (string.IsNullOrWhiteSpace(configuredCoupon))
        {
            throw new DropboxConfigurationException(
                "Dropbox trial coupon is not configured."
            );
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(coupon.Trim()),
            Encoding.UTF8.GetBytes(configuredCoupon)
        );
    }

    private DropboxTrialAccessResponse ToResponse(
        DropboxTrialAccess? access
    )
    {
        if (access is null)
        {
            return new DropboxTrialAccessResponse(false, false, null);
        }

        var expiresAt = access.RedeemedAt + TrialDuration;
        return new DropboxTrialAccessResponse(
            true,
            DateTime.UtcNow < expiresAt,
            expiresAt
        );
    }
}

public enum DropboxCouponRedemptionStatus
{
    Invalid,
    AlreadyRedeemed,
    Redeemed
}

public sealed record DropboxCouponRedemptionResult(
    DropboxCouponRedemptionStatus Status,
    DropboxTrialAccessResponse? Access
);
