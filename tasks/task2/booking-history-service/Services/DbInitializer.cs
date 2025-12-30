using BookingHistoryServiceApp.Repositories;

namespace BookingHistoryServiceApp.Services;

public class DbInitializer : IHostedService
{
    private readonly HistoryRepository _repository;

    public DbInitializer(HistoryRepository repository)
    {
        _repository = repository;
    }

    public Task StartAsync(CancellationToken cancellationToken) => _repository.InitializeAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
