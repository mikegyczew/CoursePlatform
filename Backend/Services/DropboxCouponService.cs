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
    IOptions<DropboxOptions> options,
    IDropboxService dropboxService,
    IOptions<DropboxPricingOptions> pricingOptions
)
{
    private const string LegacyCouponCourseName = "Ekonomia";
    private static readonly string CouponAlphabet =
        "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";
    private readonly DropboxOptions _options = options.Value;
    private readonly DropboxPricingOptions _pricingOptions =
        pricingOptions.Value;

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
        int courseId,
        DropboxCouponType type,
        int count,
        CancellationToken cancellationToken
    )
    {
        var course = await GetCourseLocationAsync(courseId, cancellationToken);
        var result = new List<GeneratedDropboxCoupon>(count);
        var coupons = new List<DropboxCoupon>(count);

        for (var i = 0; i < count; i++)
        {
            var code = GenerateCode();
            result.Add(new GeneratedDropboxCoupon(
                code,
                type,
                course.Id,
                course.Name
            ));
            coupons.Add(new DropboxCoupon
            {
                CodeHash = HashCode(code),
                CoursePath = course.Path,
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
        int courseId,
        CancellationToken cancellationToken
    )
    {
        var course = await GetCourseLocationAsync(courseId, cancellationToken);
        return await GetStatusAsync(userId, course, cancellationToken);
    }

    public async Task<DropboxCourseLocation> GetCourseLocationAsync(
        int courseId,
        CancellationToken cancellationToken
    ) =>
        await dropboxService.GetCourseAsync(courseId, cancellationToken)
        ?? throw new DropboxCourseNotFoundException(courseId);

    private async Task<DropboxTrialAccessResponse> GetStatusAsync(
        int userId,
        DropboxCourseLocation course,
        CancellationToken cancellationToken
    )
    {
        var redemptions = await dbContext.DropboxCouponRedemptions
            .AsNoTracking()
            .Where(item =>
                item.UserId == userId
                && (
                    item.Coupon.CoursePath == course.Path
                    || (
                        item.Coupon.CoursePath == null
                        && course.Name == LegacyCouponCourseName
                    )
                )
            )
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
        DropboxCourseLocation course,
        DropboxCouponType type,
        CancellationToken cancellationToken
    )
    {
        if (type is not (
            DropboxCouponType.Test
            or DropboxCouponType.Week
            or DropboxCouponType.Month
            or DropboxCouponType.Forever
        ))
        {
            throw new ArgumentOutOfRangeException(nameof(type));
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken);
        var redeemedAt = DateTime.UtcNow;
        var renewalStartsAt = await dbContext.DropboxCouponRedemptions
            .Where(item =>
                item.UserId == userId
                && (
                    item.Coupon.CoursePath == course.Path
                    || (
                        item.Coupon.CoursePath == null
                        && course.Name == LegacyCouponCourseName
                    )
                )
                && item.ExpiresAt > redeemedAt
            )
            .Select(item => item.ExpiresAt!.Value)
            .DefaultIfEmpty(redeemedAt)
            .MaxAsync(cancellationToken);
        var coupon = new DropboxCoupon
        {
            CodeHash = HashCode(GenerateCode()),
            CoursePath = course.Path,
            Type = type,
            IsSharedTest = false,
            IsRedeemed = true
        };
        dbContext.DropboxCoupons.Add(coupon);
        dbContext.DropboxCouponRedemptions.Add(new DropboxCouponRedemption
        {
            Coupon = coupon,
            UserId = userId,
            RedeemedAt = redeemedAt,
            ExpiresAt = GetExpiry(type, renewalStartsAt),
            NotifyOnExpiry = true
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetStatusAsync(userId, course, cancellationToken);
    }

    public async Task<DropboxCouponRedemptionResult> RedeemAsync(
        int userId,
        int courseId,
        string code,
        CancellationToken cancellationToken
    )
    {
        var course = await GetCourseLocationAsync(courseId, cancellationToken);
        var normalizedCode = code.Trim();
        var isTestCoupon = string.Equals(
            normalizedCode,
            _pricingOptions.TestCouponCode,
            StringComparison.OrdinalIgnoreCase
        );
        var coupon = isTestCoupon
            ? await dbContext.DropboxCoupons.SingleOrDefaultAsync(
                item =>
                    item.IsSharedTest
                    && (
                        item.CoursePath == course.Path
                        || (
                            item.CoursePath == null
                            && course.Name == LegacyCouponCourseName
                        )
                    ),
                cancellationToken
            )
            : await dbContext.DropboxCoupons.SingleOrDefaultAsync(
                item =>
                    item.CodeHash == HashCode(normalizedCode)
                    && (
                        item.CoursePath == course.Path
                        || (
                            item.CoursePath == null
                            && course.Name == LegacyCouponCourseName
                        )
                    ),
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
                await GetStatusAsync(userId, course, cancellationToken)
            );
        }

        var redeemedAt = DateTime.UtcNow;
        var redemption = new DropboxCouponRedemption
        {
            CouponId = coupon.Id,
            UserId = userId,
            RedeemedAt = redeemedAt,
            ExpiresAt = GetExpiry(coupon.Type, redeemedAt),
            NotifyOnExpiry = true
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
                await GetStatusAsync(userId, course, cancellationToken)
            );
        }

        return new DropboxCouponRedemptionResult(
            DropboxCouponRedemptionStatus.Redeemed,
            await GetStatusAsync(userId, course, cancellationToken)
        );
    }

    private DateTime? GetExpiry(
        DropboxCouponType type,
        DateTime redeemedAt
    ) =>
        type switch
        {
            DropboxCouponType.Test => redeemedAt.AddDays(
                _pricingOptions.TestDurationDays
            ),
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
    DropboxCouponType Type,
    int CourseId,
    string CourseName
);

public sealed class DropboxCourseNotFoundException(int courseId)
    : Exception($"Dropbox course '{courseId}' was not found.");

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
