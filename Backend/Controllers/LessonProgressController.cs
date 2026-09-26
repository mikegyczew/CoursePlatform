using System.IdentityModel.Tokens.Jwt;
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

        // Dodatkowe zabezpieczenie:
        // lessonId z URL musi być zgodne z lessonId w body.
        if (dto.LessonId != 0 && dto.LessonId != lessonId)
        {
            return BadRequest(
                "LessonId w adresie URL i danych żądania musi być taki sam."
            );
        }

        var success = await _progressService.SetCompletedAsync(
            userId.Value,
            courseId,
            lessonId,
            dto.IsCompleted);

        if (!success)
        {
            return NotFound(
                "Kurs, lekcja lub użytkownik nie istnieje albo lekcja nie należy do tego kursu."
            );
        }

        return NoContent();
    }

    private int? GetUserId()
    {
        var claim = User.FindFirst(
            JwtRegisteredClaimNames.Sub
        )?.Value;

        if (int.TryParse(claim, out var userId))
        {
            return userId;
        }

        return null;
    }
}
