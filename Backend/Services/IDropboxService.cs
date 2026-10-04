using Backend.DTOs;

namespace Backend.Services;

public interface IDropboxService
{
    string CreateAuthorizationUrl(string state);

    Task<string> ExchangeAuthorizationCodeAsync(
        string code,
        CancellationToken cancellationToken
    );

    Task<DropboxAccountResponse> GetAccountAsync(
        CancellationToken cancellationToken
    );

    Task<IReadOnlyList<CourseDto>> GetCoursesAsync(
        CancellationToken cancellationToken
    );

    Task<DropboxCourseLocation?> GetCourseAsync(
        int courseId,
        CancellationToken cancellationToken
    );

    Task<IReadOnlyList<DropboxLessonResponse>> GetLessonsAsync(
        int courseId,
        CancellationToken cancellationToken
    );

    Task<IReadOnlyDictionary<int, string>> GetLessonPathsAsync(
        int courseId,
        CancellationToken cancellationToken
    );

    Task<DropboxLessonResponse?> GetLessonAsync(
        int courseId,
        int lessonId,
        CancellationToken cancellationToken
    );

    Task<string?> GetTemporaryLinkAsync(
        int courseId,
        int lessonId,
        int fileId,
        CancellationToken cancellationToken
    );
}

public sealed record DropboxCourseLocation(
    int Id,
    string Name,
    string Path
);
