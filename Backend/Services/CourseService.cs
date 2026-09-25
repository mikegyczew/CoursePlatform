using Backend.Data;
using Backend.DTOs;
using Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public class CourseService
{
    private readonly CourseDbContext _dbContext;

    public CourseService(CourseDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<CourseDto>> GetCoursesAsync()
    {
        return await _dbContext.Courses
            .AsNoTracking()
            .Select(course => new CourseDto
            {
                Id = course.Id,
                Title = course.Title,
                Description = course.Description,
                Category = course.Category,
                ImageUrl = course.ImageUrl
            })
            .ToListAsync();
    }

    public async Task<CourseDto> CreateCourseAsync(CreateCourseDto dto)
    {
        var course = new Course
        {
            Title = dto.Title.Trim(),
            Description = dto.Description?.Trim(),
            Category = dto.Category.Trim(),
            ImageUrl = dto.ImageUrl?.Trim()
        };

        _dbContext.Courses.Add(course);

        await _dbContext.SaveChangesAsync();

        return new CourseDto
        {
            Id = course.Id,
            Title = course.Title,
            Description = course.Description,
            Category = course.Category,
            ImageUrl = course.ImageUrl
        };
    }

public async Task<CourseDto?> GetCourseByIdAsync(int id)
{
    return await _dbContext.Courses
        .AsNoTracking()
        .Where(course => course.Id == id)
        .Select(course => new CourseDto
        {
            Id = course.Id,
            Title = course.Title,
            Description = course.Description,
            Category = course.Category,
            ImageUrl = course.ImageUrl
        })
        .FirstOrDefaultAsync();
}
}
