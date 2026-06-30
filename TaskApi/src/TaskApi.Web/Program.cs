using System.Reflection;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using TaskApi.Web.Data;
using TaskApi.Web.Endpoints;
using TaskApi.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

builder.Services.AddDbContext<TaskDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("PostgreSQL")));

builder.Services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

builder.Services.AddSingleton<RabbitMqService>()
    .AddSingleton<IRabbitMqService>(p => p.GetRequiredService<RabbitMqService>())
    .AddHostedService<RabbitMqHostedService>();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TaskDbContext>();
    await db.Database.MigrateAsync();
}

app.MapTaskEndpoints();

app.Run();

public partial class Program { }
