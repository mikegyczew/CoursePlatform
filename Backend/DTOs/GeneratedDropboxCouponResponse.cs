namespace Backend.DTOs;

public sealed record GeneratedDropboxCouponResponse(
    string Code,
    string Type,
    int CourseId,
    string CourseName
);
