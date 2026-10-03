using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Backend.DTOs;
using Microsoft.Extensions.Options;

namespace Backend.Services;

public sealed class DropboxService(
    HttpClient httpClient,
    IOptions<DropboxOptions> options
) : IDropboxService
{
    private const string AuthorizationEndpoint =
        "https://www.dropbox.com/oauth2/authorize";
    private const string TokenEndpoint =
        "https://api.dropboxapi.com/oauth2/token";
    private const string CurrentAccountEndpoint =
        "https://api.dropboxapi.com/2/users/get_current_account";
    private const string ListFolderEndpoint =
        "https://api.dropboxapi.com/2/files/list_folder";
    private const string ContinueListFolderEndpoint =
        "https://api.dropboxapi.com/2/files/list_folder/continue";
    private const string DownloadEndpoint =
        "https://content.dropboxapi.com/2/files/download";
    private const string TemporaryLinkEndpoint =
        "https://api.dropboxapi.com/2/files/get_temporary_link";
    private const int DropboxCourseId = -1;
    private const int MaxTextFileBytes = 1_048_576;
    private const int MaxLessonTextBytes = 3_145_728;

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly DropboxOptions _options = options.Value;

    public string CreateAuthorizationUrl(string state)
    {
        var appKey = RequireSetting(_options.AppKey, "AppKey");
        var redirectUri = RequireSetting(_options.RedirectUri, "RedirectUri");

        var query = new Dictionary<string, string>
        {
            ["client_id"] = appKey,
            ["response_type"] = "code",
            ["token_access_type"] = "offline",
            ["force_reapprove"] = "true",
            ["redirect_uri"] = redirectUri,
            ["state"] = state,
            ["scope"] =
                "account_info.read files.metadata.read files.content.read"
        };

        return $"{AuthorizationEndpoint}?{string.Join(
            "&",
            query.Select(pair =>
                $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"
            )
        )}";
    }

    public async Task<string> ExchangeAuthorizationCodeAsync(
        string code,
        CancellationToken cancellationToken
    )
    {
        var token = await RequestTokenAsync(
            new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = RequireSetting(
                    _options.RedirectUri,
                    "RedirectUri"
                )
            },
            cancellationToken
        );

        return token.RefreshToken
            ?? throw new InvalidOperationException(
                "Dropbox did not return an offline refresh token."
            );
    }

    public async Task<DropboxAccountResponse> GetAccountAsync(
        CancellationToken cancellationToken
    )
    {
        var accessToken = await GetAccessTokenAsync(cancellationToken);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            CurrentAccountEndpoint
        );
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await httpClient.SendAsync(
            request,
            cancellationToken
        );
        response.EnsureSuccessStatusCode();

        var account = await response.Content.ReadFromJsonAsync<
            DropboxCurrentAccount
        >(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException(
                "Dropbox returned an empty account response."
            );

        return new DropboxAccountResponse(
            account.AccountId,
            account.Name.DisplayName,
            account.Email
        );
    }

    public async Task<IReadOnlyList<DropboxLessonResponse>> GetLessonsAsync(
        CancellationToken cancellationToken
    )
    {
        var lessonFolders = (await ListFolderAsync(
                RequireSetting(_options.RootFolder, "RootFolder"),
                cancellationToken
            ))
            .Where(entry => entry.Tag == "folder")
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return lessonFolders
            .Select((folder, index) => new DropboxLessonResponse(
                index + 1,
                folder.Name,
                null,
                null,
                index + 1,
                DropboxCourseId,
                Array.Empty<DropboxMaterialDto>()
            ))
            .ToArray();
    }

    public async Task<DropboxLessonResponse?> GetLessonAsync(
        int lessonId,
        CancellationToken cancellationToken
    )
    {
        if (lessonId < 1)
        {
            return null;
        }

        var lessonFolders = (await ListFolderAsync(
                RequireSetting(_options.RootFolder, "RootFolder"),
                cancellationToken
            ))
            .Where(entry => entry.Tag == "folder")
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (lessonId > lessonFolders.Count)
        {
            return null;
        }

        var folder = lessonFolders[lessonId - 1];
        var files = (await ListFolderAsync(folder.PathDisplay, cancellationToken))
            .Where(entry => entry.Tag == "file")
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var textFiles = files
            .Select((entry, index) => (entry, id: index + 1))
            .Where(item => IsTextFile(item.entry.Name))
            .ToArray();
        var totalTextBytes = 0;
        var contentParts = new List<string>();

        foreach (var (entry, _) in textFiles)
        {
            var bytes = await DownloadFileAsync(
                entry.PathDisplay,
                cancellationToken
            );
            if (
                bytes.Length > MaxTextFileBytes
                || totalTextBytes + bytes.Length > MaxLessonTextBytes
            )
            {
                throw new InvalidOperationException(
                    "Dropbox lesson text exceeds the supported size limit."
                );
            }

            totalTextBytes += bytes.Length;
            var text = System.Text.Encoding.UTF8.GetString(bytes);
            contentParts.Add($"## {entry.Name}\n\n{text}");
        }

        var materials = files
            .Select((entry, index) => (entry, id: index + 1))
            .Where(item => !IsTextFile(item.entry.Name))
            .Select(item =>
            {
                var (kind, contentType) = GetFilePresentation(item.entry.Name);
                return new DropboxMaterialDto(
                    item.id,
                    item.entry.Name,
                    kind,
                    contentType,
                    $"/api/dropbox/courses/{DropboxCourseId}/lessons/{lessonId}/files/{item.id}/link"
                );
            })
            .ToArray();

        return new DropboxLessonResponse(
            lessonId,
            folder.Name,
            null,
            contentParts.Count > 0
                ? string.Join("\n\n---\n\n", contentParts)
                : null,
            lessonId,
            DropboxCourseId,
            materials
        );
    }

    public async Task<string?> GetTemporaryLinkAsync(
        int lessonId,
        int fileId,
        CancellationToken cancellationToken
    )
    {
        var file = await GetLessonFileAsync(
            lessonId,
            fileId,
            cancellationToken
        );
        if (file is null)
        {
            return null;
        }

        var accessToken = await GetAccessTokenAsync(cancellationToken);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            TemporaryLinkEndpoint
        );
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(new
        {
            path = file.PathDisplay
        });

        using var response = await httpClient.SendAsync(
            request,
            cancellationToken
        );
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<
            DropboxTemporaryLink
        >(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException(
                "Dropbox returned an empty temporary-link response."
            );

        return result.Link;
    }

    private async Task<string> GetAccessTokenAsync(
        CancellationToken cancellationToken
    )
    {
        var token = await RequestTokenAsync(
            new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = RequireSetting(
                    _options.RefreshToken,
                    "RefreshToken"
                )
            },
            cancellationToken
        );

        return token.AccessToken;
    }

    private async Task<DropboxTokenResponse> RequestTokenAsync(
        Dictionary<string, string> form,
        CancellationToken cancellationToken
    )
    {
        form["client_id"] = RequireSetting(_options.AppKey, "AppKey");
        form["client_secret"] = RequireSetting(
            _options.AppSecret,
            "AppSecret"
        );

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            TokenEndpoint
        )
        {
            Content = new FormUrlEncodedContent(form)
        };
        using var response = await httpClient.SendAsync(
            request,
            cancellationToken
        );
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<DropboxTokenResponse>(
                JsonOptions,
                cancellationToken
            )
            ?? throw new InvalidOperationException(
                "Dropbox returned an empty token response."
            );
    }

    private async Task<List<DropboxEntry>> ListFolderAsync(
        string path,
        CancellationToken cancellationToken
    )
    {
        var accessToken = await GetAccessTokenAsync(cancellationToken);
        var entries = new List<DropboxEntry>();
        var endpoint = ListFolderEndpoint;
        object requestBody = new { path, recursive = false, limit = 2000 };

        while (true)
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                endpoint
            );
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);
            request.Content = JsonContent.Create(requestBody);

            using var response = await httpClient.SendAsync(
                request,
                cancellationToken
            );
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<
                DropboxListFolderResponse
            >(JsonOptions, cancellationToken)
                ?? throw new InvalidOperationException(
                    "Dropbox returned an empty folder listing."
                );
            entries.AddRange(result.Entries);

            if (!result.HasMore || string.IsNullOrWhiteSpace(result.Cursor))
            {
                return entries;
            }

            endpoint = ContinueListFolderEndpoint;
            requestBody = new { cursor = result.Cursor };
        }
    }

    private async Task<byte[]> DownloadFileAsync(
        string path,
        CancellationToken cancellationToken
    )
    {
        var accessToken = await GetAccessTokenAsync(cancellationToken);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            DownloadEndpoint
        );
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.TryAddWithoutValidation(
            "Dropbox-API-Arg",
            JsonSerializer.Serialize(new { path })
        );

        using var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken
        );
        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentLength is > MaxTextFileBytes)
        {
            throw new InvalidOperationException(
                "Dropbox text file exceeds the supported size limit."
            );
        }

        await using var source = await response.Content.ReadAsStreamAsync(
            cancellationToken
        );
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int bytesRead;

        while (
            (bytesRead = await source.ReadAsync(
                chunk,
                cancellationToken
            )) > 0
        )
        {
            if (buffer.Length + bytesRead > MaxTextFileBytes)
            {
                throw new InvalidOperationException(
                    "Dropbox text file exceeds the supported size limit."
                );
            }

            await buffer.WriteAsync(
                chunk.AsMemory(0, bytesRead),
                cancellationToken
            );
        }

        return buffer.ToArray();
    }

    private async Task<DropboxEntry?> GetLessonFileAsync(
        int lessonId,
        int fileId,
        CancellationToken cancellationToken
    )
    {
        if (lessonId < 1 || fileId < 1)
        {
            return null;
        }

        var lessonFolders = (await ListFolderAsync(
                RequireSetting(_options.RootFolder, "RootFolder"),
                cancellationToken
            ))
            .Where(entry => entry.Tag == "folder")
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (lessonId > lessonFolders.Count)
        {
            return null;
        }

        var files = (await ListFolderAsync(
                lessonFolders[lessonId - 1].PathDisplay,
                cancellationToken
            ))
            .Where(entry => entry.Tag == "file")
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return fileId <= files.Count
            ? files[fileId - 1]
            : null;
    }

    private static bool IsTextFile(string name) =>
        Path.GetExtension(name).ToLowerInvariant() is ".txt" or ".md" or ".markdown";

    private static (string Kind, string ContentType) GetFilePresentation(
        string name
    ) =>
        Path.GetExtension(name).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => ("image", "image/jpeg"),
            ".png" => ("image", "image/png"),
            ".gif" => ("image", "image/gif"),
            ".webp" => ("image", "image/webp"),
            ".mp4" => ("video", "video/mp4"),
            ".webm" => ("video", "video/webm"),
            ".mov" => ("video", "video/quicktime"),
            ".pdf" => ("file", "application/pdf"),
            _ => ("file", "application/octet-stream")
        };

    private static string RequireSetting(string? value, string name) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new DropboxConfigurationException(
                $"Dropbox setting '{name}' is not configured."
            )
            : value;

    private sealed record DropboxTokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken
    );

    private sealed record DropboxCurrentAccount(
        [property: JsonPropertyName("account_id")] string AccountId,
        string Email,
        DropboxAccountName Name
    );

    private sealed record DropboxAccountName(
        [property: JsonPropertyName("display_name")] string DisplayName
    );

    private sealed record DropboxListFolderResponse(
        List<DropboxEntry> Entries,
        string Cursor,
        [property: JsonPropertyName("has_more")] bool HasMore
    );

    private sealed record DropboxEntry(
        [property: JsonPropertyName(".tag")] string Tag,
        string Name,
        [property: JsonPropertyName("path_display")] string PathDisplay
    );

    private sealed record DropboxTemporaryLink(
        [property: JsonPropertyName("link")] string Link
    );
}
