using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TaskApi.Web.Data;
using TaskApi.Web.Models;
using TaskApi.Web.Services;

namespace TaskApi.Web.Endpoints;

public static class TaskEndpoints
{
    public static void MapTaskEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/tasks");

        group.MapPost("/", CreateTask);
        group.MapGet("/", GetTasks);
        group.MapPut("/{id:guid}/complete", CompleteTask);
        group.MapDelete("/{id:guid}", DeleteTask);
    }

    static async Task<IResult> CreateTask(
        TaskItem request,
        TaskDbContext db,
        IValidator<TaskItem> validator)
    {
        var result = await validator.ValidateAsync(request);
        if (!result.IsValid)
            return Results.ValidationProblem(result.ToDictionary());

        var task = new TaskItem
        {
            Id          = Guid.NewGuid(),
            Title       = request.Title.Trim(),
            IsCompleted = false,
            CreatedAt   = DateTimeOffset.UtcNow,
            Priority    = request.Priority
        };

        db.TaskItems.Add(task);
        await db.SaveChangesAsync();

        return Results.Created($"/tasks/{task.Id}", task);
    }

    static async Task<IResult> GetTasks(TaskDbContext db)
    {
        var tasks = await db.TaskItems.AsNoTracking().ToListAsync();
        return Results.Ok(tasks);
    }

    static async Task<IResult> CompleteTask(
        Guid id,
        TaskDbContext db,
        IRabbitMqService rabbitMq)
    {
        var task = await db.TaskItems.AsTracking().FirstOrDefaultAsync(t => t.Id == id);
        if (task is null)
            return Results.NotFound();

        if (task.IsCompleted)
            return Results.Conflict(new { error = "Task is already completed" });

        task.IsCompleted = true;
        task.CompletedAt = DateTimeOffset.UtcNow;

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Conflict(new { error = "Task was modified by another request" });
        }

        await rabbitMq.PublishTaskCompletedAsync(task);

        return Results.Ok(task);
    }

    static async Task<IResult> DeleteTask(Guid id, TaskDbContext db)
    {
        var task = await db.TaskItems.FindAsync(id);
        if (task is null)
            return Results.NotFound();

        db.TaskItems.Remove(task);
        await db.SaveChangesAsync();

        return Results.Ok();
    }
}
