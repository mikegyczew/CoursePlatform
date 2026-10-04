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

    Task<IReadOnlyList<DropboxLessonResponse>> GetLessonsAsync(
        CancellationToken cancellationToken
    );

    Task<IReadOnlyDictionary<int, string>> GetLessonPathsAsync(
        CancellationToken cancellationToken
    );

    Task<DropboxLessonResponse?> GetLessonAsync(
        int lessonId,
        CancellationToken cancellationToken
    );

    Task<string?> GetTemporaryLinkAsync(
        int lessonId,
        int fileId,
        CancellationToken cancellationToken
    );
}
