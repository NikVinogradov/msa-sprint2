using Booking;
using BookingServiceApp.Models;
using BookingServiceApp.Services;
using Grpc.Core;

namespace BookingServiceApp.Grpc;

public class GrpcBookingService : BookingService.BookingServiceBase
{
    private readonly BookingDomainService _domain;
    private readonly ILogger<GrpcBookingService> _logger;

    public GrpcBookingService(BookingDomainService domain, ILogger<GrpcBookingService> logger)
    {
        _domain = domain;
        _logger = logger;
    }

    public override async Task<BookingResponse> CreateBooking(BookingRequest request, ServerCallContext context)
    {
        try
        {
            var booking = await _domain.CreateBookingAsync(
                request.UserId,
                request.HotelId,
                string.IsNullOrWhiteSpace(request.PromoCode) ? null : request.PromoCode,
                context.CancellationToken
            );

            return booking.ToGrpc();
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning(ex, "Validation failed for booking");
            throw new RpcException(new Status(StatusCode.FailedPrecondition, ex.Message));
        }
    }

    public override async Task<BookingListResponse> ListBookings(BookingListRequest request, ServerCallContext context)
    {
        var bookings = await _domain.ListBookingsAsync(
            string.IsNullOrWhiteSpace(request.UserId) ? null : request.UserId,
            context.CancellationToken
        );

        var response = new BookingListResponse();
        response.Bookings.AddRange(bookings.Select(b => b.ToGrpc()));
        return response;
    }
}
