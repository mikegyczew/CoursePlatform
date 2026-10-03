namespace Backend.DTOs;

/// <summary>Dropbox account details returned after a successful connection check.</summary>
public sealed record DropboxAccountResponse(
    string AccountId,
    string Name,
    string Email
);
