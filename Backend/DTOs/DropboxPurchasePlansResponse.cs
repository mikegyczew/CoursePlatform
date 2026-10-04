namespace Backend.DTOs;

public sealed record DropboxPurchasePlansResponse(
    bool WeekAvailable,
    bool MonthAvailable,
    bool ForeverAvailable,
    decimal WeekPricePln,
    decimal MonthPricePln,
    decimal ForeverPricePln
);
