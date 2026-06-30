using TaskApi.Web.Models;

namespace TaskApi.Web.Services;

public interface IRabbitMqService : IAsyncDisposable
{
    Task PublishTaskCompletedAsync(TaskItem task);
}
