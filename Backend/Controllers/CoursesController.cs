using Backend.DTOs;
using Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CoursesController : ControllerBase
{
    private readonly CourseService _courseService;

    public CoursesController(CourseService courseService)
    {
        _courseService = courseService;
    }

    [HttpGet]
    public async Task<ActionResult<List<CourseDto>>> GetCourses()
    {
        var courses = await _courseService.GetCoursesAsync();

        return Ok(courses);
    }

    [HttpPost]
    public async Task<ActionResult<CourseDto>> CreateCourse(
    CreateCourseDto dto)
    {
        var course = await _courseService.CreateCourseAsync(dto);

        return CreatedAtAction(
            nameof(GetCourses),
            new { id = course.Id },
            course
        );
    }
[HttpGet("{id:int}")]
public async Task<ActionResult<CourseDto>> GetCourseById(int id)
{
    var course = await _courseService.GetCourseByIdAsync(id);

    if (course is null)
    {
        return NotFound();
    }

    return Ok(course);
}
}
