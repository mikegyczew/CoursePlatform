namespace Backend.DTOs;

public sealed record DropboxPurchasePlansResponse(
    bool WeekAvailable,
    bool MonthAvailable,
    bool ForeverAvailable
);
