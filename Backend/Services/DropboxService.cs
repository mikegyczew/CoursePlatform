using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Backend.DTOs;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Core;

namespace Backend.Services;

public sealed class DropboxService(
    HttpClient httpClient,
    IMemoryCache memoryCache,
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
    private const int MaxTextFileBytes = 1_048_576;
    private const int MaxLessonTextBytes = 3_145_728;
    private const int MaxContentPdfBytes = 10_485_760;
    private static readonly TimeSpan FolderCacheDuration =
        TimeSpan.FromSeconds(30);
    private static readonly TimeSpan TokenExpiryBuffer =
        TimeSpan.FromSeconds(60);
    private static readonly ConcurrentDictionary<string, SemaphoreSlim>
        CacheLocks = new(StringComparer.Ordinal);

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly DropboxOptions _options = options.Value;
    private readonly IMemoryCache _memoryCache = memoryCache;

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
        await EnsureDropboxSuccessStatusCodeAsync(
            response,
            cancellationToken
        );

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

    public async Task<IReadOnlyList<CourseDto>> GetCoursesAsync(
        CancellationToken cancellationToken
    )
    {
        var courseFolders = await GetCourseFoldersAsync(cancellationToken);
        var courses = courseFolders
            .Select(folder => new CourseDto
            {
                Id = GetCourseId(folder.PathDisplay),
                Title = folder.Name,
                Description = "Kurs i materiały udostępnione w Dropboxie.",
                Category = "Dropbox"
            })
            .ToArray();
        if (courses.Select(course => course.Id).Distinct().Count() != courses.Length)
        {
            throw new InvalidOperationException(
                "Dropbox course folder IDs collided. Rename one of the course folders."
            );
        }

        return courses;
    }

    public async Task<DropboxCourseLocation?> GetCourseAsync(
        int courseId,
        CancellationToken cancellationToken
    )
    {
        var courseFolders = await GetCourseFoldersAsync(cancellationToken);
        var folder = courseFolders.SingleOrDefault(
            item => GetCourseId(item.PathDisplay) == courseId
        );
        return folder is null
            ? null
            : new DropboxCourseLocation(
                courseId,
                folder.Name,
                folder.PathDisplay
            );
    }

    public async Task<IReadOnlyList<DropboxLessonResponse>> GetLessonsAsync(
        int courseId,
        CancellationToken cancellationToken
    )
    {
        var lessonFolders = await GetLessonFoldersAsync(
            courseId,
            cancellationToken
        );

        return lessonFolders
            .Select((folder, index) => new DropboxLessonResponse(
                index + 1,
                folder.Name,
                null,
                null,
                index + 1,
                courseId,
                Array.Empty<DropboxMaterialDto>()
            ))
            .ToArray();
    }

    public async Task<IReadOnlyDictionary<int, string>> GetLessonPathsAsync(
        int courseId,
        CancellationToken cancellationToken
    )
    {
        var lessonFolders = await GetLessonFoldersAsync(
            courseId,
            cancellationToken
        );

        return lessonFolders
            .Select((folder, index) => new
            {
                LessonId = index + 1,
                folder.PathDisplay
            })
            .ToDictionary(item => item.LessonId, item => item.PathDisplay);
    }

    public async Task<DropboxLessonResponse?> GetLessonAsync(
        int courseId,
        int lessonId,
        CancellationToken cancellationToken
    )
    {
        if (lessonId < 1)
        {
            return null;
        }

        var lessonFolders = await GetLessonFoldersAsync(
            courseId,
            cancellationToken
        );

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
            .Where(item =>
                IsTextFile(item.entry.Name)
                && !IsContentPdf(item.entry.Name)
            )
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

        var contentPdf = files.FirstOrDefault(
            entry => IsContentPdf(entry.Name)
        );
        if (contentPdf is not null)
        {
            var pdfBytes = await DownloadFileAsync(
                contentPdf.PathDisplay,
                cancellationToken,
                MaxContentPdfBytes
            );
            var pdfText = ExtractPdfText(pdfBytes);
            if (pdfText.Length == 0)
            {
                contentParts.Insert(
                    0,
                    "Plik `content.pdf` nie zawiera tekstu możliwego do odczytania. "
                        + "Jeśli to skan, potrzebne jest rozpoznawanie OCR."
                );
            }
            else
            {
                var combinedTextLength =
                    contentParts.Sum(part => part.Length)
                    + pdfText.Length
                    + (contentParts.Count * 2);
                if (combinedTextLength > MaxLessonTextBytes)
                {
                    throw new DropboxLessonContentException(
                        "The combined lesson text exceeds the supported size limit."
                    );
                }

                contentParts.Insert(0, pdfText);
            }
        }

        var materials = files
            .Select((entry, index) => (entry, id: index + 1))
            .Where(item =>
                !IsTextFile(item.entry.Name)
                && !IsContentPdf(item.entry.Name)
            )
            .Select(item =>
            {
                var (kind, contentType) = GetFilePresentation(item.entry.Name);
                return new DropboxMaterialDto(
                    item.id,
                    item.entry.Name,
                    kind,
                    contentType,
                    $"/api/dropbox/courses/{courseId}/lessons/{lessonId}/files/{item.id}/link"
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
            courseId,
            materials
        );
    }

    public async Task<string?> GetTemporaryLinkAsync(
        int courseId,
        int lessonId,
        int fileId,
        CancellationToken cancellationToken
    )
    {
        var file = await GetLessonFileAsync(
            courseId,
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
        await EnsureDropboxSuccessStatusCodeAsync(
            response,
            cancellationToken
        );

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
        var appKey = RequireSetting(_options.AppKey, "AppKey");
        var refreshToken = RequireSetting(
            _options.RefreshToken,
            "RefreshToken"
        );
        var cacheKey =
            $"dropbox-access-token:{HashCacheKey(appKey, refreshToken)}";
        if (_memoryCache.TryGetValue(cacheKey, out string? cachedToken))
        {
            return cachedToken!;
        }

        var cacheLock = CacheLocks.GetOrAdd(
            cacheKey,
            static _ => new SemaphoreSlim(1, 1)
        );
        await cacheLock.WaitAsync(cancellationToken);
        try
        {
            if (_memoryCache.TryGetValue(cacheKey, out cachedToken))
            {
                return cachedToken!;
            }

            var token = await RequestTokenAsync(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = refreshToken
                },
                cancellationToken
            );

            if (token.ExpiresIn > TokenExpiryBuffer.TotalSeconds)
            {
                _memoryCache.Set(
                    cacheKey,
                    token.AccessToken,
                    TimeSpan.FromSeconds(token.ExpiresIn)
                        - TokenExpiryBuffer
                );
            }

            return token.AccessToken;
        }
        finally
        {
            cacheLock.Release();
        }
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
        await EnsureDropboxSuccessStatusCodeAsync(
            response,
            cancellationToken
        );

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
        var cacheKey = $"dropbox-folder:{path}";
        if (_memoryCache.TryGetValue(
                cacheKey,
                out List<DropboxEntry>? cachedEntries
            ))
        {
            return cachedEntries!;
        }

        var cacheLock = CacheLocks.GetOrAdd(
            cacheKey,
            static _ => new SemaphoreSlim(1, 1)
        );
        await cacheLock.WaitAsync(cancellationToken);
        try
        {
            if (_memoryCache.TryGetValue(
                    cacheKey,
                    out cachedEntries
                ))
            {
                return cachedEntries!;
            }

            var entries = await ListFolderFromDropboxAsync(
                path,
                cancellationToken
            );
            _memoryCache.Set(cacheKey, entries, FolderCacheDuration);
            return entries;
        }
        finally
        {
            cacheLock.Release();
        }
    }

    private async Task<List<DropboxEntry>> ListFolderFromDropboxAsync(
        string path,
        CancellationToken cancellationToken
    )
    {
        var accessToken = await GetAccessTokenAsync(cancellationToken);
        var entries = new List<DropboxEntry>();
        var endpoint = ListFolderEndpoint;
        object requestBody = new
        {
            path,
            recursive = false,
            limit = 2000,
            include_non_downloadable_files = true
        };

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
            await EnsureDropboxSuccessStatusCodeAsync(
                response,
                cancellationToken
            );

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
        CancellationToken cancellationToken,
        int maxBytes = MaxTextFileBytes
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
        await EnsureDropboxSuccessStatusCodeAsync(
            response,
            cancellationToken
        );

        if (response.Content.Headers.ContentLength is long contentLength
            && contentLength > maxBytes)
        {
            throw new DropboxLessonContentException(
                "Dropbox file exceeds the supported size limit."
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
            if (buffer.Length + bytesRead > maxBytes)
            {
                throw new DropboxLessonContentException(
                    "Dropbox file exceeds the supported size limit."
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
        int courseId,
        int lessonId,
        int fileId,
        CancellationToken cancellationToken
    )
    {
        if (lessonId < 1 || fileId < 1)
        {
            return null;
        }

        var lessonFolders = await GetLessonFoldersAsync(
            courseId,
            cancellationToken
        );

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

    private async Task<List<DropboxEntry>> GetCourseFoldersAsync(
        CancellationToken cancellationToken
    ) =>
        (await ListFolderAsync(
                GetConfiguredRootPath(),
                cancellationToken
            ))
            .Where(entry => entry.Tag == "folder")
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static int GetCourseId(string path)
    {
        var hash = SHA256.HashData(
            Encoding.UTF8.GetBytes(path.ToUpperInvariant())
        );
        var value = BinaryPrimitives.ReadUInt32BigEndian(hash);
        return -((int)(value % int.MaxValue)) - 1;
    }

    private async Task<List<DropboxEntry>> GetLessonFoldersAsync(
        int courseId,
        CancellationToken cancellationToken
    )
    {
        var course = await GetCourseAsync(courseId, cancellationToken);
        if (course is null)
        {
            return [];
        }

        return (await ListFolderAsync(
                course.Path,
                cancellationToken
            ))
            .Where(entry => entry.Tag == "folder")
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private string GetConfiguredRootPath()
    {
        var rootFolder = RequireSetting(
            _options.RootFolder,
            "RootFolder"
        ).Trim().Trim('/');
        return rootFolder.Length == 0 ? string.Empty : $"/{rootFolder}";
    }

    private static string HashCacheKey(string appKey, string refreshToken)
    {
        var value = Encoding.UTF8.GetBytes($"{appKey}:{refreshToken}");
        return Convert.ToHexString(SHA256.HashData(value));
    }

    private static bool IsContentPdf(string name) =>
        string.Equals(name, "content.pdf", StringComparison.OrdinalIgnoreCase);

    private static string ExtractPdfText(byte[] pdfBytes)
    {
        if (
            pdfBytes.Length < 5
            || !pdfBytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8)
        )
        {
            throw new DropboxLessonContentException(
                "The content.pdf file is not a valid PDF."
            );
        }

        try
        {
            using var document = PdfDocument.Open(pdfBytes);
            var content = new StringBuilder();

            foreach (var page in document.GetPages())
            {
                if (content.Length > 0)
                {
                    content.AppendLine().AppendLine();
                }

                content.Append(page.Text);
                if (content.Length > MaxLessonTextBytes)
                {
                    throw new DropboxLessonContentException(
                        "The extracted PDF text exceeds the supported size limit."
                    );
                }
            }

            return content.ToString().Trim();
        }
        catch (PdfDocumentFormatException exception)
        {
            throw new DropboxLessonContentException(
                "The content.pdf file could not be read.",
                exception
            );
        }
    }

    private static async Task EnsureDropboxSuccessStatusCodeAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken
    )
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string? errorSummary = null;
        var errorContent = await response.Content.ReadAsStringAsync(
            cancellationToken
        );

        try
        {
            using var document = JsonDocument.Parse(errorContent);
            if (
                document.RootElement.TryGetProperty(
                    "error_summary",
                    out var summary
                )
            )
            {
                errorSummary = summary.GetString();
            }
        }
        catch (JsonException)
        {
            errorSummary = null;
        }

        var requestId = response.Headers.TryGetValues(
            "Dropbox-API-Request-Id",
            out var requestIds
        )
            ? requestIds.FirstOrDefault()
            : null;
        var bodyDetails = string.IsNullOrWhiteSpace(errorContent)
            ? "empty response body"
            : errorContent.Length <= 1000
                ? errorContent
                : $"{errorContent[..1000]} [truncated]";
        var details = errorSummary ?? bodyDetails;
        throw new DropboxApiException(
            (int)response.StatusCode,
            details,
            requestId
        );
    }

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
        [property: JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: JsonPropertyName("expires_in")] long ExpiresIn
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
