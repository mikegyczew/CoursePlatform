namespace Backend.DTOs;

/// <summary>
/// Refresh token returned during the one-time Dropbox connection setup.
/// Store this value as a secret in the backend hosting environment.
/// </summary>
public sealed record DropboxRefreshTokenResponse(
    string RefreshToken,
    string Instructions
);
