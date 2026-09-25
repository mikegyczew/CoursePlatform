namespace Backend.DTOs;

public class LessonDto
{
    public int Id { get; set; }

    public required string Title { get; set; }

    public string? Description { get; set; }

    public string? Content { get; set; }

    public int Order { get; set; }

    public int CourseId { get; set; }
}

