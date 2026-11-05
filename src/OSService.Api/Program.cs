using Microsoft.EntityFrameworkCore;
using OSService.Infrastructure.Contextos;
using OSService.Domain.Interfaces;
using OSService.Infrastructure.Repositorios;
using OSService.Application.Servicos;

var builder = WebApplication.CreateBuilder(args);


var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("A connection string do PostgreSQL não foi configurada.");

builder.Services.AddDbContext<OrdemServicoDbContext>(options =>
{
    options.UseNpgsql(connectionString);
});

builder.Services.AddScoped<IOrdemServicoRepositorio, OrdemServicoRepositorio>();
builder.Services.AddScoped<OrdemServicoServico>();

// Registra os controllers da API
builder.Services.AddControllers();

// Registra os Health Checks
builder.Services.AddHealthChecks();

// Configura o Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

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
