using Booking;

namespace BookingServiceApp.Models;

public static class BookingExtensions
{
    public static BookingResponse ToGrpc(this BookingRecord record)
    {
        return new BookingResponse
        {
            Id = record.Id.ToString(),
            UserId = record.UserId,
            HotelId = record.HotelId,
            PromoCode = record.PromoCode ?? string.Empty,
            DiscountPercent = record.DiscountPercent,
            Price = record.Price,
            CreatedAt = record.CreatedAt.UtcDateTime.ToString("O")
        };
    }
}
