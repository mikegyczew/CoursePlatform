using System.Security.Cryptography;
using System.Text;
using Backend.Data;
using Backend.DTOs;
using Backend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Backend.Services;

public sealed class DropboxCouponService(
    CourseDbContext dbContext,
    IOptions<DropboxOptions> options
)
{
    private const string TestCouponCode = "kuponTest";
    private static readonly TimeSpan TestDuration = TimeSpan.FromHours(24);
    private static readonly string CouponAlphabet =
        "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";
    private readonly DropboxOptions _options = options.Value;

    public bool IsAdminKeyConfigured =>
        !string.IsNullOrWhiteSpace(_options.CouponAdminKey);

    public bool IsAdminKeyValid(string? suppliedKey)
    {
        var configuredKey = _options.CouponAdminKey;
        if (
            string.IsNullOrWhiteSpace(configuredKey)
            || string.IsNullOrWhiteSpace(suppliedKey)
        )
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(suppliedKey),
            Encoding.UTF8.GetBytes(configuredKey)
        );
    }

    public async Task<IReadOnlyList<GeneratedDropboxCoupon>> GenerateAsync(
        DropboxCouponType type,
        int count,
        CancellationToken cancellationToken
    )
    {
        var result = new List<GeneratedDropboxCoupon>(count);
        var coupons = new List<DropboxCoupon>(count);

        for (var i = 0; i < count; i++)
        {
            var code = GenerateCode();
            result.Add(new GeneratedDropboxCoupon(code, type));
            coupons.Add(new DropboxCoupon
            {
                CodeHash = HashCode(code),
                Type = type,
                IsSharedTest = false
            });
        }

        dbContext.DropboxCoupons.AddRange(coupons);
        await dbContext.SaveChangesAsync(cancellationToken);
        return result;
    }

    public async Task<DropboxTrialAccessResponse> GetStatusAsync(
        int userId,
        CancellationToken cancellationToken
    )
    {
        var redemptions = await dbContext.DropboxCouponRedemptions
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            .Select(item => new
            {
                item.ExpiresAt
            })
            .ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var hasActiveCoupon = redemptions.Any(
            item => item.ExpiresAt is null || item.ExpiresAt > now
        );
        var couponExpiry = redemptions
            .Where(item => item.ExpiresAt > now)
            .MaxBy(item => item.ExpiresAt)
            ?.ExpiresAt;
        var hasPermanentCoupon = redemptions.Any(
            item => item.ExpiresAt is null
        );

        return new DropboxTrialAccessResponse(
            redemptions.Count > 0,
            hasActiveCoupon,
            hasPermanentCoupon ? null : couponExpiry
        );
    }

    public async Task<DropboxTrialAccessResponse> CreateAndRedeemPurchaseCouponAsync(
        int userId,
        DropboxCouponType type,
        CancellationToken cancellationToken
    )
    {
        if (type is not (
            DropboxCouponType.Week
            or DropboxCouponType.Month
            or DropboxCouponType.Forever
        ))
        {
            throw new ArgumentOutOfRangeException(nameof(type));
        }

        var code = GenerateCode();
        var redeemedAt = DateTime.UtcNow;
        var coupon = new DropboxCoupon
        {
            CodeHash = HashCode(code),
            Type = type,
            IsSharedTest = false,
            IsRedeemed = true
        };
        dbContext.DropboxCoupons.Add(coupon);

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken);
        dbContext.DropboxCouponRedemptions.Add(new DropboxCouponRedemption
        {
            Coupon = coupon,
            UserId = userId,
            RedeemedAt = redeemedAt,
            ExpiresAt = GetExpiry(type, redeemedAt)
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetStatusAsync(userId, cancellationToken);
    }

    public async Task<DropboxCouponRedemptionResult> RedeemAsync(
        int userId,
        string code,
        CancellationToken cancellationToken
    )
    {
        var normalizedCode = code.Trim();
        var isTestCoupon = string.Equals(
            normalizedCode,
            TestCouponCode,
            StringComparison.OrdinalIgnoreCase
        );
        var coupon = isTestCoupon
            ? await dbContext.DropboxCoupons.SingleOrDefaultAsync(
                item => item.IsSharedTest,
                cancellationToken
            )
            : await dbContext.DropboxCoupons.SingleOrDefaultAsync(
                item => item.CodeHash == HashCode(normalizedCode),
                cancellationToken
            );

        if (coupon is null)
        {
            return new DropboxCouponRedemptionResult(
                DropboxCouponRedemptionStatus.Invalid,
                null
            );
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken);

        if (!coupon.IsSharedTest)
        {
            var claimed = await dbContext.DropboxCoupons
                .Where(item => item.Id == coupon.Id && !item.IsRedeemed)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        item => item.IsRedeemed,
                        true
                    ),
                    cancellationToken
                );
            if (claimed == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new DropboxCouponRedemptionResult(
                    DropboxCouponRedemptionStatus.AlreadyRedeemed,
                    null
                );
            }
        }

        var alreadyRedeemed = await dbContext.DropboxCouponRedemptions
            .AnyAsync(
                item => item.CouponId == coupon.Id && item.UserId == userId,
                cancellationToken
            );
        if (alreadyRedeemed)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new DropboxCouponRedemptionResult(
                DropboxCouponRedemptionStatus.AlreadyRedeemed,
                await GetStatusAsync(userId, cancellationToken)
            );
        }

        var redeemedAt = DateTime.UtcNow;
        var redemption = new DropboxCouponRedemption
        {
            CouponId = coupon.Id,
            UserId = userId,
            RedeemedAt = redeemedAt,
            ExpiresAt = GetExpiry(coupon.Type, redeemedAt)
        };
        dbContext.DropboxCouponRedemptions.Add(redemption);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            var wasRedeemed = await dbContext.DropboxCouponRedemptions
                .AsNoTracking()
                .AnyAsync(
                    item =>
                        item.CouponId == coupon.Id
                        && item.UserId == userId,
                    cancellationToken
                );
            if (!wasRedeemed)
            {
                throw;
            }

            return new DropboxCouponRedemptionResult(
                DropboxCouponRedemptionStatus.AlreadyRedeemed,
                await GetStatusAsync(userId, cancellationToken)
            );
        }

        return new DropboxCouponRedemptionResult(
            DropboxCouponRedemptionStatus.Redeemed,
            await GetStatusAsync(userId, cancellationToken)
        );
    }

    private static DateTime? GetExpiry(
        DropboxCouponType type,
        DateTime redeemedAt
    ) =>
        type switch
        {
            DropboxCouponType.Test => redeemedAt + TestDuration,
            DropboxCouponType.Week => redeemedAt.AddDays(7),
            DropboxCouponType.Month => redeemedAt.AddDays(30),
            DropboxCouponType.Forever => null,
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };

    private static string GenerateCode()
    {
        var code = new StringBuilder("CP-");
        for (var i = 0; i < 16; i++)
        {
            code.Append(
                CouponAlphabet[RandomNumberGenerator.GetInt32(CouponAlphabet.Length)]
            );
            if (i == 7)
            {
                code.Append('-');
            }
        }

        return code.ToString();
    }

    private static string HashCode(string code) =>
        Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(code.ToUpperInvariant()))
        );
}

public sealed record GeneratedDropboxCoupon(
    string Code,
    DropboxCouponType Type
);

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
