namespace Backend.DTOs;

public sealed record GenerateDropboxCouponsRequest(
    string Type,
    int Count,
    int CourseId
);
