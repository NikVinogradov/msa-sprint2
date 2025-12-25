using System.Text.Json;
using BookingHistoryServiceApp.Models;
using BookingHistoryServiceApp.Repositories;
using Confluent.Kafka;

namespace BookingHistoryServiceApp.Services;

public class BookingHistoryConsumer : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly HistoryRepository _repository;
    private readonly IConfiguration _config;
    private readonly ILogger<BookingHistoryConsumer> _logger;

    public BookingHistoryConsumer(HistoryRepository repository, IConfiguration config, ILogger<BookingHistoryConsumer> logger)
    {
        _repository = repository;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var bootstrapServers = _config["Kafka:BootstrapServers"] ?? "kafka:9092";
        var topic = _config["Kafka:Topic"] ?? "booking-created";
        var groupId = _config["Kafka:GroupId"] ?? "booking-history-service";

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = groupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        using var consumer = new ConsumerBuilder<Ignore, string>(consumerConfig).Build();
        consumer.Subscribe(topic);

        _logger.LogInformation("Booking history consumer started on topic {Topic}", topic);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                if (result?.Message?.Value is null)
                {
                    continue;
                }

                var evt = JsonSerializer.Deserialize<BookingCreatedEvent>(result.Message.Value, JsonOptions);
                if (evt is null)
                {
                    _logger.LogWarning("Failed to deserialize booking event");
                    consumer.Commit(result);
                    continue;
                }

                await _repository.ProcessEventAsync(evt, stoppingToken);
                consumer.Commit(result);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing booking event");
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
        }

        consumer.Close();
    }
}
