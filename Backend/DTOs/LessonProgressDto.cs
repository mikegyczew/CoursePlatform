namespace Backend.DTOs;

public class LessonProgressDto
{
    public int LessonId { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
}
