using System.Text.Json.Serialization;

namespace BookingServiceApp.Models;

public sealed record BookingCreatedEvent(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("user_id")] string UserId,
    [property: JsonPropertyName("hotel_id")] string HotelId,
    [property: JsonPropertyName("promo_code")] string? PromoCode,
    [property: JsonPropertyName("discount_percent")] double DiscountPercent,
    [property: JsonPropertyName("price")] double Price,
    [property: JsonPropertyName("created_at")] string CreatedAt
);
