namespace Backend.Services;

public sealed class DropboxPricingOptions
{
    public decimal WeeklyPricePln { get; set; } = 50m;

    public string TestCouponCode { get; set; } = "kuponTest";

    public int TestDurationDays { get; set; } = 24;

    public decimal MonthlyPricePln => WeeklyPricePln * 4m;

    public decimal ForeverPricePln => WeeklyPricePln * 20m;
}
