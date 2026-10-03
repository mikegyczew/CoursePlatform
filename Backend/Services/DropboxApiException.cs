namespace Backend.Services;

public sealed class DropboxApiException(
    int statusCode,
    string errorSummary,
    string? requestId
) : Exception(
    $"Dropbox API returned {statusCode}: {errorSummary}"
        + (requestId is null ? string.Empty : $" Request ID: {requestId}.")
)
{
    public int StatusCode { get; } = statusCode;
    public string ErrorSummary { get; } = errorSummary;
    public string? RequestId { get; } = requestId;
}
