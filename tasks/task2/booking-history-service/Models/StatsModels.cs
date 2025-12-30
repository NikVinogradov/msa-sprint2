namespace BookingHistoryServiceApp.Models;

public sealed record UserStats(string UserId, int TotalBookings, double TotalSpent);
public sealed record HotelStats(string HotelId, int TotalBookings, double TotalRevenue);
public sealed record DayStats(DateTime Day, int TotalBookings, double TotalRevenue);
