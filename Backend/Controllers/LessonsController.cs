using Backend.DTOs;
using Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Route("api/courses/{courseId:int}/lessons")]
public class LessonsController : ControllerBase
{
    private readonly LessonService _lessonService;

    public LessonsController(LessonService lessonService)
    {
        _lessonService = lessonService;
    }

    [HttpGet]
    public async Task<ActionResult<List<LessonDto>>> GetLessons(
        int courseId)
    {
        var lessons = await _lessonService.GetLessonsAsync(courseId);

        return Ok(lessons);
    }

    [HttpPost]
    public async Task<ActionResult<LessonDto>> CreateLesson(
        int courseId,
        CreateLessonDto dto)
    {
        var lesson = await _lessonService.CreateLessonAsync(
            courseId,
            dto);

        if (lesson is null)
        {
            return NotFound($"Kurs o ID {courseId} nie istnieje.");
        }

        return Created(
            $"/api/courses/{courseId}/lessons/{lesson.Id}",
            lesson);
    }

    [HttpGet("{lessonId:int}")]
    public async Task<ActionResult<LessonDto>> GetLessonById(int courseId, int lessonId)
    {
        var lesson = await _lessonService.GetLessonByIdAsync(
            courseId,
            lessonId);

        if (lesson is null)
        {
            return NotFound();
        }

        return Ok(lesson);
    }
}

