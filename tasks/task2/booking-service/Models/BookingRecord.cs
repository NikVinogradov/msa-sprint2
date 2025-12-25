namespace BookingServiceApp.Models;

public sealed record BookingRecord(
    long Id,
    string UserId,
    string HotelId,
    string? PromoCode,
    double DiscountPercent,
    double Price,
    DateTimeOffset CreatedAt
);
