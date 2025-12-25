using BookingHistoryServiceApp.Models;
using Npgsql;

namespace BookingHistoryServiceApp.Repositories;

public class HistoryRepository
{
    private readonly string _connectionString;

    public HistoryRepository(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("HistoryDb")
            ?? throw new InvalidOperationException("Connection string HistoryDb is required");
    }

    public async Task InitializeAsync(CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var sql = @"
CREATE TABLE IF NOT EXISTS booking_history (
    booking_id TEXT PRIMARY KEY,
    user_id TEXT NOT NULL,
    hotel_id TEXT NOT NULL,
    promo_code TEXT,
    discount_percent DOUBLE PRECISION NOT NULL,
    price DOUBLE PRECISION NOT NULL,
    created_at TIMESTAMPTZ NOT NULL
);

CREATE TABLE IF NOT EXISTS booking_stats_user (
    user_id TEXT PRIMARY KEY,
    total_bookings INT NOT NULL,
    total_spent DOUBLE PRECISION NOT NULL
);

CREATE TABLE IF NOT EXISTS booking_stats_hotel (
    hotel_id TEXT PRIMARY KEY,
    total_bookings INT NOT NULL,
    total_revenue DOUBLE PRECISION NOT NULL
);

CREATE TABLE IF NOT EXISTS booking_stats_day (
    day DATE PRIMARY KEY,
    total_bookings INT NOT NULL,
    total_revenue DOUBLE PRECISION NOT NULL
);
";

        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task ProcessEventAsync(BookingCreatedEvent evt, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var createdAt = DateTimeOffset.Parse(evt.CreatedAt);
        var day = createdAt.UtcDateTime.Date;

        var insertSql = @"
INSERT INTO booking_history (booking_id, user_id, hotel_id, promo_code, discount_percent, price, created_at)
VALUES (@booking_id, @user_id, @hotel_id, @promo_code, @discount_percent, @price, @created_at)
ON CONFLICT (booking_id) DO NOTHING
RETURNING booking_id;
";

        await using var insertCmd = new NpgsqlCommand(insertSql, conn, tx);
        insertCmd.Parameters.AddWithValue("booking_id", evt.Id);
        insertCmd.Parameters.AddWithValue("user_id", evt.UserId);
        insertCmd.Parameters.AddWithValue("hotel_id", evt.HotelId);
        insertCmd.Parameters.AddWithValue("promo_code", (object?)evt.PromoCode ?? DBNull.Value);
        insertCmd.Parameters.AddWithValue("discount_percent", evt.DiscountPercent);
        insertCmd.Parameters.AddWithValue("price", evt.Price);
        insertCmd.Parameters.AddWithValue("created_at", createdAt);

        var inserted = await insertCmd.ExecuteScalarAsync(ct);
        if (inserted is null)
        {
            await tx.RollbackAsync(ct);
            return;
        }

        var userSql = @"
INSERT INTO booking_stats_user (user_id, total_bookings, total_spent)
VALUES (@user_id, 1, @price)
ON CONFLICT (user_id) DO UPDATE
SET total_bookings = booking_stats_user.total_bookings + 1,
    total_spent = booking_stats_user.total_spent + EXCLUDED.total_spent;
";

        await using (var cmd = new NpgsqlCommand(userSql, conn, tx))
        {
            cmd.Parameters.AddWithValue("user_id", evt.UserId);
            cmd.Parameters.AddWithValue("price", evt.Price);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        var hotelSql = @"
INSERT INTO booking_stats_hotel (hotel_id, total_bookings, total_revenue)
VALUES (@hotel_id, 1, @price)
ON CONFLICT (hotel_id) DO UPDATE
SET total_bookings = booking_stats_hotel.total_bookings + 1,
    total_revenue = booking_stats_hotel.total_revenue + EXCLUDED.total_revenue;
";

        await using (var cmd = new NpgsqlCommand(hotelSql, conn, tx))
        {
            cmd.Parameters.AddWithValue("hotel_id", evt.HotelId);
            cmd.Parameters.AddWithValue("price", evt.Price);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        var daySql = @"
INSERT INTO booking_stats_day (day, total_bookings, total_revenue)
VALUES (@day, 1, @price)
ON CONFLICT (day) DO UPDATE
SET total_bookings = booking_stats_day.total_bookings + 1,
    total_revenue = booking_stats_day.total_revenue + EXCLUDED.total_revenue;
";

        await using (var cmd = new NpgsqlCommand(daySql, conn, tx))
        {
            cmd.Parameters.AddWithValue("day", day);
            cmd.Parameters.AddWithValue("price", evt.Price);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
    }

    public async Task<UserStats?> GetUserStatsAsync(string userId, CancellationToken ct)
    {
        const string sql = "SELECT user_id, total_bookings, total_spent FROM booking_stats_user WHERE user_id = @user_id";
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("user_id", userId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        return new UserStats(reader.GetString(0), reader.GetInt32(1), reader.GetDouble(2));
    }

    public async Task<HotelStats?> GetHotelStatsAsync(string hotelId, CancellationToken ct)
    {
        const string sql = "SELECT hotel_id, total_bookings, total_revenue FROM booking_stats_hotel WHERE hotel_id = @hotel_id";
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("hotel_id", hotelId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        return new HotelStats(reader.GetString(0), reader.GetInt32(1), reader.GetDouble(2));
    }

    public async Task<DayStats?> GetDayStatsAsync(DateTime day, CancellationToken ct)
    {
        const string sql = "SELECT day, total_bookings, total_revenue FROM booking_stats_day WHERE day = @day";
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("day", day.Date);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        return new DayStats(reader.GetDateTime(0), reader.GetInt32(1), reader.GetDouble(2));
    }
}
