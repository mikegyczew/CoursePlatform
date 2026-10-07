using Backend.Data;
using Backend.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public sealed class AdminService(CourseDbContext dbContext) : IAdminService
{
    public async Task<AdminDashboardResponse> GetDashboardAsync(
        CancellationToken cancellationToken
    )
    {
        var users = await dbContext.Users
            .AsNoTracking()
            .OrderByDescending(user => user.CreatedAt)
            .Select(user => new AdminUserResponse(
                user.Id,
                user.Email,
                user.Name,
                user.EmailConfirmed,
                new DateTimeOffset(user.CreatedAt, TimeSpan.Zero)
            ))
            .ToListAsync(cancellationToken);

        var courses = await dbContext.Courses
            .AsNoTracking()
            .OrderBy(course => course.Title)
            .Select(course => new AdminCourseResponse(
                course.Id,
                course.Title,
                course.Category,
                course.Lessons.Count
            ))
            .ToListAsync(cancellationToken);

        var pendingRegistrations = await dbContext.PendingRegistrations
            .AsNoTracking()
            .Where(registration => registration.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(registration => registration.CreatedAt)
            .Select(registration => new AdminPendingRegistrationResponse(
                registration.Email,
                registration.Name,
                new DateTimeOffset(registration.CreatedAt, TimeSpan.Zero),
                new DateTimeOffset(registration.ExpiresAt, TimeSpan.Zero)
            ))
            .ToListAsync(cancellationToken);

        var accessGrants = await dbContext.DropboxCouponRedemptions
            .AsNoTracking()
            .OrderByDescending(redemption => redemption.RedeemedAt)
            .Select(redemption => new AdminAccessResponse(
                redemption.Id,
                redemption.UserId,
                redemption.User.Email,
                redemption.Coupon.CoursePath,
                redemption.Coupon.Type.ToString(),
                new DateTimeOffset(redemption.RedeemedAt, TimeSpan.Zero),
                redemption.ExpiresAt == null
                    ? null
                    : new DateTimeOffset(
                        redemption.ExpiresAt.Value,
                        TimeSpan.Zero
                    )
            ))
            .ToListAsync(cancellationToken);

        var localProgress = await dbContext.LessonProgress
            .AsNoTracking()
            .Select(progress => new AdminProgressResponse(
                progress.UserId,
                progress.User.Email,
                progress.Lesson.Course.Title,
                progress.Lesson.Title,
                progress.IsCompleted,
                progress.CompletedAt == null
                    ? null
                    : new DateTimeOffset(
                        progress.CompletedAt.Value,
                        TimeSpan.Zero
                    )
            ))
            .ToListAsync(cancellationToken);

        var dropboxProgress = await dbContext.DropboxLessonProgress
            .AsNoTracking()
            .Select(progress => new AdminProgressResponse(
                progress.UserId,
                progress.User.Email,
                "Dropbox",
                progress.LessonPath,
                progress.IsCompleted,
                progress.CompletedAt == null
                    ? null
                    : new DateTimeOffset(
                        progress.CompletedAt.Value,
                        TimeSpan.Zero
                    )
            ))
            .ToListAsync(cancellationToken);

        return new AdminDashboardResponse(
            users,
            pendingRegistrations,
            courses,
            accessGrants,
            localProgress.Concat(dropboxProgress).ToArray()
        );
    }

    public async Task<bool> DeleteUserAsync(
        int userId,
        CancellationToken cancellationToken
    )
    {
        var deleted = await dbContext.Users
            .Where(user => user.Id == userId)
            .ExecuteDeleteAsync(cancellationToken);
        return deleted > 0;
    }

    public async Task<bool> RevokeAccessAsync(
        int redemptionId,
        CancellationToken cancellationToken
    )
    {
        var deleted = await dbContext.DropboxCouponRedemptions
            .Where(redemption => redemption.Id == redemptionId)
            .ExecuteDeleteAsync(cancellationToken);
        return deleted > 0;
    }
}
