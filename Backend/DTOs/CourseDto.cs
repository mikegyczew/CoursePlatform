namespace Backend.DTOs;

public class CourseDto
{
    public int Id { get; set; }

    public required string Title { get; set; }

    public string? Description { get; set; }

    public required string Category { get; set; }

    public string? ImageUrl { get; set; }
}
