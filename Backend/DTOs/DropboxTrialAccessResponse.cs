namespace Backend.DTOs;

public sealed record DropboxTrialAccessResponse(
    bool HasRedeemedCoupon,
    bool HasAccess,
    DateTime? ExpiresAt
);
