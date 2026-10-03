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

    private static string RequireSetting(string? value, string name) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException(
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
}
