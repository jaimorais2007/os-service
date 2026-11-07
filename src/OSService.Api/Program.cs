
using Amazon;
using Amazon.SQS;
using Microsoft.EntityFrameworkCore;
using OSService.Api.Configuration;
using OSService.Api.Filters;
using OSService.Application.Services;
using OSService.Domain.Interface;
using OSService.Domain.Interfaces;
using OSService.Infrastructure.Contexts;
using OSService.Infrastructure.Messaging;
using OSService.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("A connection string do PostgreSQL não foi configurada.");

builder.Services.AddDbContext<ServiceOrderDbContext>(options =>
{
    options.UseNpgsql(connectionString);
});

builder.Services.AddScoped<IServiceOrderRepository, ServiceOrderRepository>();

var awsRegion = builder.Configuration["AWS:Region"];
if (!string.IsNullOrWhiteSpace(awsRegion))
{
    var region = RegionEndpoint.GetBySystemName(awsRegion);
    builder.Services.AddSingleton<IAmazonSQS>(_ => new AmazonSQSClient(region));
}

builder.Services.AddScoped<IServiceOrderNotificationPublisher, SqsServiceOrderNotificationPublisher>();
builder.Services.AddScoped<ServiceOrderService>();

// Configura OpenTelemetry (traces, metricas e logs) com exportacao OTLP para o New Relic
builder.Services.AddOTelConfiguration(builder.Configuration);
builder.Logging.AddOTelLogging();

// Registra os controllers da API com filtro global de exceções
builder.Services.AddControllers(options =>
{
    options.Filters.Add<GlobalExceptionFilter>();
});

// Registra os Health Checks
builder.Services.AddHealthChecks();

// Configura o Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Aplica as migrations pendentes ao iniciar a aplicação
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider
        .GetRequiredService<ServiceOrderDbContext>();

    await context.Database.MigrateAsync();
}

// Habilita o Swagger em desenvolvimento
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthorization();

// Mapeia os controllers
app.MapControllers();

// Endpoint para verificar a saúde da aplicação
app.MapHealthChecks("/health");

await app.RunAsync();

public partial class Program { }
