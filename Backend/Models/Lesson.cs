namespace Backend.Models;

public class Lesson
{
    public int Id { get; set; }

    public required string Title { get; set; }

    public string? Description { get; set; }

    public string? Content { get; set; }

    public int Order { get; set; }

    public int CourseId { get; set; }

    public Course Course { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
