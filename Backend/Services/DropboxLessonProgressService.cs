using Backend.Data;
using Backend.DTOs;
using Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public sealed class DropboxLessonProgressService(
    CourseDbContext dbContext,
    IDropboxService dropboxService
)
{
    public async Task<List<LessonProgressDto>> GetProgressAsync(
        int userId,
        int courseId,
        CancellationToken cancellationToken
    )
    {
        if (userId == SuperAdminOptions.UserId)
        {
            return [];
        }

        var lessonPaths = await dropboxService.GetLessonPathsAsync(
            courseId,
            cancellationToken
        );
        if (lessonPaths.Count == 0)
        {
            return [];
        }

        var paths = lessonPaths.Values.ToArray();
        var progress = await dbContext.DropboxLessonProgress
            .AsNoTracking()
            .Where(item =>
                item.UserId == userId && paths.Contains(item.LessonPath)
            )
            .ToListAsync(cancellationToken);
        var lessonIdsByPath = lessonPaths.ToDictionary(
            item => item.Value,
            item => item.Key,
            StringComparer.Ordinal
        );

        return progress
            .Select(item => new LessonProgressDto
            {
                LessonId = lessonIdsByPath[item.LessonPath],
                IsCompleted = item.IsCompleted,
                CompletedAt = item.CompletedAt
            })
            .ToList();
    }

    public async Task<bool> SetCompletedAsync(
        int userId,
        int courseId,
        int lessonId,
        bool completed,
        CancellationToken cancellationToken
    )
    {
        if (userId == SuperAdminOptions.UserId)
        {
            return true;
        }

        if (!await dbContext.Users.AnyAsync(
                user => user.Id == userId,
                cancellationToken
            ))
        {
            return false;
        }

        var lessonPaths = await dropboxService.GetLessonPathsAsync(
            courseId,
            cancellationToken
        );
        if (!lessonPaths.TryGetValue(lessonId, out var lessonPath))
        {
            return false;
        }

        var progress = await dbContext.DropboxLessonProgress
            .FirstOrDefaultAsync(
                item =>
                    item.UserId == userId && item.LessonPath == lessonPath,
                cancellationToken
            );

        if (progress is null)
        {
            progress = new DropboxLessonProgress
            {
                UserId = userId,
                LessonPath = lessonPath,
                IsCompleted = completed,
                CompletedAt = completed ? DateTime.UtcNow : null
            };
            dbContext.DropboxLessonProgress.Add(progress);
        }
        else
        {
            progress.IsCompleted = completed;
            progress.CompletedAt = completed ? DateTime.UtcNow : null;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
