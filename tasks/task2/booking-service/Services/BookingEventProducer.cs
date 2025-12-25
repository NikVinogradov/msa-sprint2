using System.Text.Json;
using BookingServiceApp.Models;
using Confluent.Kafka;

namespace BookingServiceApp.Services;

public class BookingEventProducer : IDisposable
{
    private readonly IProducer<Null, string> _producer;
    private readonly string _topic;
    private readonly ILogger<BookingEventProducer> _logger;

    public BookingEventProducer(IConfiguration config, ILogger<BookingEventProducer> logger)
    {
        _logger = logger;
        _topic = config["Kafka:Topic"] ?? "booking-created";
        var bootstrapServers = config["Kafka:BootstrapServers"] ?? "kafka:9092";

        var producerConfig = new ProducerConfig
        {
            BootstrapServers = bootstrapServers
        };

        _producer = new ProducerBuilder<Null, string>(producerConfig).Build();
    }

    public async Task PublishAsync(BookingRecord record, CancellationToken ct)
    {
        var evt = new BookingCreatedEvent(
            record.Id.ToString(),
            record.UserId,
            record.HotelId,
            record.PromoCode,
            record.DiscountPercent,
            record.Price,
            record.CreatedAt.UtcDateTime.ToString("O")
        );

        var payload = JsonSerializer.Serialize(evt);

        try
        {
            await _producer.ProduceAsync(_topic, new Message<Null, string> { Value = payload }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish booking event");
        }
    }

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(3));
        _producer.Dispose();
    }
}
