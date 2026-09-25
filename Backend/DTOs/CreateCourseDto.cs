using System.ComponentModel.DataAnnotations;

namespace Backend.DTOs;

public class CreateCourseDto
{
    [Required]
    [MaxLength(200)]
    public required string Title { get; set; }

    [MaxLength(2000)]
    public string? Description { get; set; }

    [Required]
    [MaxLength(100)]
    public required string Category { get; set; }

    [Url]
    [MaxLength(500)]
    public string? ImageUrl { get; set; }
}
