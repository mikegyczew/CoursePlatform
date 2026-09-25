namespace Backend.Models;

public class Course
{
    public int Id { get; set; }

    public required string Title { get; set; }

    public string? Description { get; set; }

    public required string Category { get; set; }

    public string? ImageUrl { get; set; }

    public ICollection<Lesson> Lessons { get; set; } = [];

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
