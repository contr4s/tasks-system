using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using TaskApi.Web.Models;

namespace TaskApi.Web.Services;

public class RabbitMqService : IRabbitMqService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<RabbitMqService> _logger;
    private IConnection? _connection;
    private IChannel? _channel;
    private bool _disposed;

    public RabbitMqService(IConfiguration configuration, ILogger<RabbitMqService> logger)
    {
        _configuration = configuration;
        _logger        = logger;
    }

    public async Task InitializeAsync()
    {
        if (_channel is not null) return;
        if (_disposed) return;

        var cfg = _configuration.GetSection("RabbitMQ");
        var factory = new ConnectionFactory
        {
            HostName                 = cfg["Host"] ?? "localhost",
            Port                     = int.Parse(cfg["Port"] ?? "5672"),
            UserName                 = cfg["Username"] ?? "guest",
            Password                 = cfg["Password"] ?? "guest",
            AutomaticRecoveryEnabled = true,
            RequestedHeartbeat       = TimeSpan.FromSeconds(10),
        };

        try
        {
            _connection = await factory.CreateConnectionAsync();
            _channel    = await _connection.CreateChannelAsync();

            await _channel.ExchangeDeclareAsync(exchange: "task.events", type: ExchangeType.Topic, durable: true,
                autoDelete: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to RabbitMQ");
        }
    }

    public async Task PublishTaskCompletedAsync(TaskItem task)
    {
        if (_channel is null)
        {
            _logger.LogError("RabbitMQ not available — skipping publish for task {TaskId}", task.Id.ToString());
            return;
        }

        try
        {
            var body = new
            {
                taskId      = task.Id,
                title       = task.Title,
                completedAt = task.CompletedAt,
                priority    = task.Priority
            };

            var json = JsonSerializer.Serialize(body);
            var bytes = Encoding.UTF8.GetBytes(json);

            await _channel.BasicPublishAsync(exchange: "task.events", routingKey: "task.completed", body: bytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish task {TaskId} completed event to RabbitMQ", task.Id.ToString());
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            if (_channel is not null)
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await _channel.CloseAsync(cancellationToken: cts.Token);
                await _channel.DisposeAsync();
            }
            if (_connection is not null)
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await _connection.CloseAsync(cancellationToken: cts.Token);
                await _connection.DisposeAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during RabbitMQ graceful shutdown");
        }
    }
}
