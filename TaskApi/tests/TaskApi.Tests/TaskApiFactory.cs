using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using TaskApi.Web.Data;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Xunit;

namespace TaskApi.Tests;

public class TaskApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder()
        .WithImage("rabbitmq:4-management")
        .WithEnvironment("RABBITMQ_DEFAULT_USER", "admin")
        .WithEnvironment("RABBITMQ_DEFAULT_PASS", "admin")
        .Build();

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .Build();

    public int RabbitMqPort => _rabbitMq.GetMappedPublicPort(5672);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PostgreSQL"] = _postgres.GetConnectionString(),
                ["RabbitMQ:Host"]                = "localhost",
                ["RabbitMQ:Port"]                = _rabbitMq.GetMappedPublicPort(5672).ToString(),
                ["RabbitMQ:Username"]            = "admin",
                ["RabbitMQ:Password"]            = "admin",
            });
        });
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await _rabbitMq.StartAsync();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TaskDbContext>();
        await db.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await _rabbitMq.DisposeAsync();
    }
}
