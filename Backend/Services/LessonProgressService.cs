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
        var courseExists = await _dbContext.Courses
            .AnyAsync(course => course.Id == courseId);

        if (!courseExists)
        {
            return [];
        }

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
        int courseId,
        int lessonId,
        bool completed)
    {
        // Sprawdzamy, czy użytkownik istnieje.
        var userExists = await _dbContext.Users
            .AnyAsync(user => user.Id == userId);

        if (!userExists)
        {
            return false;
        }

        // Pobieramy lekcję razem z informacją, do którego kursu należy.
        var lesson = await _dbContext.Lessons
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == lessonId);

        if (lesson is null)
        {
            return false;
        }

        // Najważniejsza kontrola:
        // lekcja musi należeć do kursu przekazanego w URL.
        if (lesson.CourseId != courseId)
        {
            return false;
        }

        // Sprawdzamy, czy użytkownik ma już progress dla tej lekcji.
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
