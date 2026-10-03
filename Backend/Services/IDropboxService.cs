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
}
