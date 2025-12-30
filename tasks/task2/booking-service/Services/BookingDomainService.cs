using BookingServiceApp.Models;
using BookingServiceApp.Repositories;

namespace BookingServiceApp.Services;

public class BookingDomainService
{
    private readonly MonolithClient _monolith;
    private readonly BookingRepository _repository;
    private readonly BookingEventProducer _producer;
    private readonly ILogger<BookingDomainService> _logger;

    public BookingDomainService(
        MonolithClient monolith,
        BookingRepository repository,
        BookingEventProducer producer,
        ILogger<BookingDomainService> logger)
    {
        _monolith = monolith;
        _repository = repository;
        _producer = producer;
        _logger = logger;
    }

    public async Task<BookingRecord> CreateBookingAsync(string userId, string hotelId, string? promoCode, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ValidationException("UserId is required");
        }

        if (string.IsNullOrWhiteSpace(hotelId))
        {
            throw new ValidationException("HotelId is required");
        }

        if (!await _monolith.IsUserActiveAsync(userId, ct))
        {
            throw new ValidationException("User is inactive");
        }

        if (await _monolith.IsUserBlacklistedAsync(userId, ct))
        {
            throw new ValidationException("User is blacklisted");
        }

        if (!await _monolith.IsHotelOperationalAsync(hotelId, ct))
        {
            throw new ValidationException("Hotel is not operational");
        }

        if (!await _monolith.IsHotelTrustedAsync(hotelId, ct))
        {
            throw new ValidationException("Hotel is not trusted based on reviews");
        }

        if (await _monolith.IsHotelFullyBookedAsync(hotelId, ct))
        {
            throw new ValidationException("Hotel is fully booked");
        }

        var status = await _monolith.GetUserStatusAsync(userId, ct);
        var isVip = string.Equals(status, "VIP", StringComparison.OrdinalIgnoreCase);
        var basePrice = isVip ? 80.0 : 100.0;

        var discount = 0.0;
        if (!string.IsNullOrWhiteSpace(promoCode))
        {
            var promoDiscount = await _monolith.ValidatePromoAsync(promoCode, userId, ct);
            if (promoDiscount.HasValue)
            {
                discount = promoDiscount.Value;
            }
            else
            {
                _logger.LogInformation("Promo code {PromoCode} is invalid for user {UserId}", promoCode, userId);
            }
        }

        var finalPrice = basePrice - discount;
        var createdAt = DateTimeOffset.UtcNow;

        var booking = await _repository.InsertAsync(
            userId,
            hotelId,
            promoCode,
            discount,
            finalPrice,
            createdAt,
            ct
        );

        await _producer.PublishAsync(booking, ct);
        return booking;
    }

    public Task<IReadOnlyList<BookingRecord>> ListBookingsAsync(string? userId, CancellationToken ct) =>
        _repository.ListAsync(userId, ct);
}
