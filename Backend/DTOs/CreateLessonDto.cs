using System.ComponentModel.DataAnnotations;

namespace Backend.DTOs;

public class CreateLessonDto
{
    [Required]
    [MaxLength(200)]
    public required string Title { get; set; }

    [MaxLength(5000)]
    public string? Description { get; set; }

    public string? Content { get; set; }

    [Range(1, 10000)]
    public int Order { get; set; }
}
