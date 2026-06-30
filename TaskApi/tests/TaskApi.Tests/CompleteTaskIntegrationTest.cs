using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using TaskApi.Web.Models;
using Xunit;

namespace TaskApi.Tests;

public class CompleteTaskIntegrationTest : IClassFixture<TaskApiFactory>
{
    private readonly TaskApiFactory _factory;
    private readonly HttpClient _client;

    public CompleteTaskIntegrationTest(TaskApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateAndCompleteTask_ShouldPublishRabbitMqEvent()
    {
        // Arrange
        var createPayload = new { title = "Buy milk", priority = Priority.High };
        var createResponse = await _client.PostAsync(
            "/tasks",
            new StringContent(JsonSerializer.Serialize(createPayload), Encoding.UTF8, "application/json"));

        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadAsStringAsync();
        var task = JsonSerializer.Deserialize<JsonElement>(created);
        var taskId = task.GetProperty("id").GetGuid();

        var rmqFactory = new ConnectionFactory
        {
            HostName = "localhost",
            Port = _factory.RabbitMqPort,
            UserName = "admin",
            Password = "admin",
        };
        await using var connection = await rmqFactory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        await channel.ExchangeDeclareAsync("task.events", ExchangeType.Topic, durable: true);
        var queue = await channel.QueueDeclareAsync("", durable: false, exclusive: true, autoDelete: true);
        await channel.QueueBindAsync(queue.QueueName, "task.events", "task.completed");

        var received = new TaskCompletionSource<string>();
        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            received.TrySetResult(Encoding.UTF8.GetString(ea.Body.ToArray()));
            await Task.CompletedTask;
        };

        await channel.BasicConsumeAsync(queue.QueueName, autoAck: true, consumer: consumer);

        // Act
        var completeResponse = await _client.PutAsync($"/tasks/{taskId}/complete", null);

        // Assert
        completeResponse.EnsureSuccessStatusCode();
        
        var getResponse = await _client.GetAsync("/tasks");
        getResponse.EnsureSuccessStatusCode();
        
        var tasksJson = await getResponse.Content.ReadAsStringAsync();
        var tasks = JsonSerializer.Deserialize<JsonElement>(tasksJson);
        var completedTask = tasks.EnumerateArray().First(t => t.GetProperty("id").GetGuid() == taskId);
        Assert.True(completedTask.GetProperty("isCompleted").GetBoolean());
        Assert.NotNull(completedTask.GetProperty("completedAt").GetString());

        var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(message);
        
        var eventData = JsonSerializer.Deserialize<JsonElement>(message);
        Assert.Equal(taskId, eventData.GetProperty("taskId").GetGuid());
        Assert.Equal("Buy milk", eventData.GetProperty("title").GetString());
        Assert.Equal((int)Priority.High, eventData.GetProperty("priority").GetInt32());
    }
}
