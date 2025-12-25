using BookingServiceApp.Models;
using Npgsql;

namespace BookingServiceApp.Repositories;

public class BookingRepository
{
    private readonly string _connectionString;

    public BookingRepository(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("BookingDb")
            ?? throw new InvalidOperationException("Connection string BookingDb is required");
    }

    public async Task InitializeAsync(CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var sql = @"
CREATE TABLE IF NOT EXISTS booking (
    id BIGSERIAL PRIMARY KEY,
    user_id TEXT NOT NULL,
    hotel_id TEXT NOT NULL,
    promo_code TEXT,
    discount_percent DOUBLE PRECISION NOT NULL,
    price DOUBLE PRECISION NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS booking_user_id_idx ON booking(user_id);
";

        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<BookingRecord> InsertAsync(
        string userId,
        string hotelId,
        string? promoCode,
        double discountPercent,
        double price,
        DateTimeOffset createdAt,
        CancellationToken ct)
    {
        const string sql = @"
INSERT INTO booking (user_id, hotel_id, promo_code, discount_percent, price, created_at)
VALUES (@user_id, @hotel_id, @promo_code, @discount_percent, @price, @created_at)
RETURNING id, user_id, hotel_id, promo_code, discount_percent, price, created_at;
";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("user_id", userId);
        cmd.Parameters.AddWithValue("hotel_id", hotelId);
        cmd.Parameters.AddWithValue("promo_code", (object?)promoCode ?? DBNull.Value);
        cmd.Parameters.AddWithValue("discount_percent", discountPercent);
        cmd.Parameters.AddWithValue("price", price);
        cmd.Parameters.AddWithValue("created_at", createdAt);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            throw new InvalidOperationException("Failed to insert booking");
        }

        return new BookingRecord(
            reader.GetInt64(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.GetDouble(4),
            reader.GetDouble(5),
            reader.GetFieldValue<DateTimeOffset>(6)
        );
    }

    public async Task<IReadOnlyList<BookingRecord>> ListAsync(string? userId, CancellationToken ct)
    {
        var sql = userId is null
            ? "SELECT id, user_id, hotel_id, promo_code, discount_percent, price, created_at FROM booking ORDER BY created_at DESC"
            : "SELECT id, user_id, hotel_id, promo_code, discount_percent, price, created_at FROM booking WHERE user_id = @user_id ORDER BY created_at DESC";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = new NpgsqlCommand(sql, conn);
        if (userId is not null)
        {
            cmd.Parameters.AddWithValue("user_id", userId);
        }

        var results = new List<BookingRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(new BookingRecord(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetDouble(4),
                reader.GetDouble(5),
                reader.GetFieldValue<DateTimeOffset>(6)
            ));
        }

        return results;
    }
}
