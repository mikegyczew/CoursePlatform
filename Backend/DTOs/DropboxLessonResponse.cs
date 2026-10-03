namespace Backend.DTOs;

/// <summary>A lesson and its text and media stored in Dropbox.</summary>
public sealed record DropboxLessonResponse(
    int Id,
    string Title,
    string? Description,
    string? Content,
    int Order,
    int CourseId,
    IReadOnlyList<DropboxMaterialDto> Materials
);
