using BookingHistoryServiceApp.Repositories;
using BookingHistoryServiceApp.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<HistoryRepository>();
builder.Services.AddHostedService<DbInitializer>();
builder.Services.AddHostedService<BookingHistoryConsumer>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok("ok"));

app.MapGet("/stats/user/{userId}", async (HistoryRepository repo, string userId, CancellationToken ct) =>
{
    var stats = await repo.GetUserStatsAsync(userId, ct);
    return stats is null ? Results.NotFound() : Results.Ok(stats);
});

app.MapGet("/stats/hotel/{hotelId}", async (HistoryRepository repo, string hotelId, CancellationToken ct) =>
{
    var stats = await repo.GetHotelStatsAsync(hotelId, ct);
    return stats is null ? Results.NotFound() : Results.Ok(stats);
});

app.MapGet("/stats/day/{day}", async (HistoryRepository repo, string day, CancellationToken ct) =>
{
    if (!DateTime.TryParse(day, out var parsed))
    {
        return Results.BadRequest("Invalid date format");
    }

    var stats = await repo.GetDayStatsAsync(parsed.Date, ct);
    return stats is null ? Results.NotFound() : Results.Ok(stats);
});

app.Run();
