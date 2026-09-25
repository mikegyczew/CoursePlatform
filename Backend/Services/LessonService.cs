using Backend.Data;
using Backend.DTOs;
using Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public class LessonService
{
    private readonly CourseDbContext _dbContext;

    public LessonService(CourseDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<LessonDto>> GetLessonsAsync(int courseId)
    {
        return await _dbContext.Lessons
            .AsNoTracking()
            .Where(lesson => lesson.CourseId == courseId)
            .OrderBy(lesson => lesson.Order)
            .Select(lesson => new LessonDto
            {
                Id = lesson.Id,
                Title = lesson.Title,
                Description = lesson.Description,
                Content = lesson.Content,
                Order = lesson.Order,
                CourseId = lesson.CourseId
            })
            .ToListAsync();
    }

    public async Task<LessonDto?> CreateLessonAsync(
        int courseId,
        CreateLessonDto dto)
    {
        var courseExists = await _dbContext.Courses
            .AnyAsync(course => course.Id == courseId);

        if (!courseExists)
        {
            return null;
        }

        var lesson = new Lesson
        {
            Title = dto.Title.Trim(),
            Description = dto.Description?.Trim(),
            Content = dto.Content?.Trim(),
            Order = dto.Order,
            CourseId = courseId
        };

        _dbContext.Lessons.Add(lesson);

        await _dbContext.SaveChangesAsync();

        return new LessonDto
        {
            Id = lesson.Id,
            Title = lesson.Title,
            Description = lesson.Description,
            Content = lesson.Content,
            Order = lesson.Order,
            CourseId = lesson.CourseId
        };
    }

    public async Task<LessonDto?> GetLessonByIdAsync(int courseId, int lessonId)
    {
        return await _dbContext.Lessons
            .AsNoTracking()
            .Where(lesson =>
                lesson.Id == lessonId &&
                lesson.CourseId == courseId)
            .Select(lesson => new LessonDto
            {
                Id = lesson.Id,
                Title = lesson.Title,
                Description = lesson.Description,
                Content = lesson.Content,
                Order = lesson.Order,
                CourseId = lesson.CourseId
            })
            .FirstOrDefaultAsync();
    }
}

