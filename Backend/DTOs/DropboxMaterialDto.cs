namespace Backend.DTOs;

/// <summary>A file in a Dropbox lesson that can be displayed or downloaded.</summary>
public sealed record DropboxMaterialDto(
    int Id,
    string Name,
    string Kind,
    string ContentType,
    string Url
);
