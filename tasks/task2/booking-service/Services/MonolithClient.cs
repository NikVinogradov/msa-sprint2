using System.Text.Json;

namespace BookingServiceApp.Services;

public class MonolithClient
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly HttpClient _http;

    public MonolithClient(HttpClient http)
    {
        _http = http;
    }

    public Task<bool> IsUserActiveAsync(string userId, CancellationToken ct) =>
        GetBoolAsync($"/api/users/{Uri.EscapeDataString(userId)}/active", ct);

    public Task<bool> IsUserBlacklistedAsync(string userId, CancellationToken ct) =>
        GetBoolAsync($"/api/users/{Uri.EscapeDataString(userId)}/blacklisted", ct);

    public Task<string?> GetUserStatusAsync(string userId, CancellationToken ct) =>
        GetStringAsync($"/api/users/{Uri.EscapeDataString(userId)}/status", ct);

    public Task<bool> IsHotelOperationalAsync(string hotelId, CancellationToken ct) =>
        GetBoolAsync($"/api/hotels/{Uri.EscapeDataString(hotelId)}/operational", ct);

    public Task<bool> IsHotelFullyBookedAsync(string hotelId, CancellationToken ct) =>
        GetBoolAsync($"/api/hotels/{Uri.EscapeDataString(hotelId)}/fully-booked", ct);

    public Task<bool> IsHotelTrustedAsync(string hotelId, CancellationToken ct) =>
        GetBoolAsync($"/api/reviews/hotel/{Uri.EscapeDataString(hotelId)}/trusted", ct);

    public async Task<double?> ValidatePromoAsync(string code, string userId, CancellationToken ct)
    {
        var url = $"/api/promos/validate?code={Uri.EscapeDataString(code)}&userId={Uri.EscapeDataString(userId)}";
        using var response = await _http.PostAsync(url, content: null, ct);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);

        if (doc.RootElement.TryGetProperty("discount", out var discountEl))
        {
            return discountEl.GetDouble();
        }

        if (doc.RootElement.TryGetProperty("discountPercent", out var discountPercentEl))
        {
            return discountPercentEl.GetDouble();
        }

        return null;
    }

    private async Task<bool> GetBoolAsync(string path, CancellationToken ct)
    {
        using var response = await _http.GetAsync(path, ct);
        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<bool>(json, JsonOptions);
    }

    private async Task<string?> GetStringAsync(string path, CancellationToken ct)
    {
        using var response = await _http.GetAsync(path, ct);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<string>(json, JsonOptions);
    }
}
