using BookingServiceApp.Repositories;

namespace BookingServiceApp.Services;

public class DbInitializer : IHostedService
{
    private readonly BookingRepository _repository;

    public DbInitializer(BookingRepository repository)
    {
        _repository = repository;
    }

    public Task StartAsync(CancellationToken cancellationToken) => _repository.InitializeAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
