
using Microsoft.EntityFrameworkCore;
using OSService.Application.Services;
using OSService.Domain.Interfaces;
using OSService.Infrastructure.Contexts;
using OSService.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("A connection string do PostgreSQL não foi configurada.");

builder.Services.AddDbContext<ServiceOrderDbContext>(options =>
{
    options.UseNpgsql(connectionString);
});

builder.Services.AddScoped<IServiceOrderRepository, ServiceOrderRepository>();
builder.Services.AddScoped<ServiceOrderService>();

// Registra os controllers da API
builder.Services.AddControllers();

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
