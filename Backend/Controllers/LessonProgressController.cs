using System.Security.Claims;
using Backend.DTOs;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Authorize]
[Route("api/courses/{courseId:int}/progress")]
public class LessonProgressController : ControllerBase
{
    private readonly LessonProgressService _progressService;

    public LessonProgressController(
        LessonProgressService progressService)
    {
        _progressService = progressService;
    }

    [HttpGet]
    public async Task<ActionResult<List<LessonProgressDto>>> GetProgress(
        int courseId)
    {
        var userId = GetUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var progress = await _progressService.GetProgressAsync(
            userId.Value,
            courseId);

        return Ok(progress);
    }

    [HttpPut("{lessonId:int}")]
    public async Task<IActionResult> SetProgress(
        int courseId,
        int lessonId,
        [FromBody] LessonProgressDto dto)
    {
        var userId = GetUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var lesson = await _progressService.SetCompletedAsync(
            userId.Value,
            lessonId,
            dto.IsCompleted);

        if (!lesson)
        {
            return NotFound();
        }

        return NoContent();
    }

    private int? GetUserId()
    {
        var claim = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (int.TryParse(claim, out var userId))
        {
            return userId;
        }

        return null;
    }
}
