using Backend.Data;
using Backend.DTOs;
using Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public class LessonProgressService
{
    private readonly CourseDbContext _dbContext;

    public LessonProgressService(CourseDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<LessonProgressDto>> GetProgressAsync(
        int userId,
        int courseId)
    {
        return await _dbContext.LessonProgress
            .AsNoTracking()
            .Where(progress =>
                progress.UserId == userId &&
                progress.Lesson.CourseId == courseId)
            .Select(progress => new LessonProgressDto
            {
                LessonId = progress.LessonId,
                IsCompleted = progress.IsCompleted,
                CompletedAt = progress.CompletedAt
            })
            .ToListAsync();
    }

    public async Task<bool> SetCompletedAsync(
        int userId,
        int lessonId,
        bool completed)
    {
        var lessonExists = await _dbContext.Lessons
            .AnyAsync(lesson => lesson.Id == lessonId);

        if (!lessonExists)
        {
            return false;
        }

        var progress = await _dbContext.LessonProgress
            .FirstOrDefaultAsync(item =>
                item.UserId == userId &&
                item.LessonId == lessonId);

        if (progress is null)
        {
            progress = new LessonProgress
            {
                UserId = userId,
                LessonId = lessonId,
                IsCompleted = completed,
                CompletedAt = completed
                    ? DateTime.UtcNow
                    : null
            };

            _dbContext.LessonProgress.Add(progress);
        }
        else
        {
            progress.IsCompleted = completed;
            progress.CompletedAt = completed
                ? DateTime.UtcNow
                : null;
        }

        await _dbContext.SaveChangesAsync();

        return true;
    }
}
