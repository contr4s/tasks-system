namespace TaskApi.Web.Services;

public sealed class RabbitMqHostedService : IHostedService
{
    private readonly RabbitMqService _rabbitMq;

    public RabbitMqHostedService(RabbitMqService rabbitMq)
    {
        _rabbitMq = rabbitMq;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _rabbitMq.InitializeAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _rabbitMq.DisposeAsync();
    }
}
