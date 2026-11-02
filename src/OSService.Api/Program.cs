
var builder = WebApplication.CreateBuilder(args);

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

app.UseHttpsRedirection();

app.UseAuthorization();

// Mapeia os controllers
app.MapControllers();

// Endpoint para verificar a saúde da aplicação
app.MapHealthChecks("/health");

app.Run();

public partial class Program { }
