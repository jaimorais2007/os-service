
# Etapa 1 - Build da aplicação
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

COPY . .

RUN dotnet restore "src/OSService.Api/OSService.Api.csproj"

RUN dotnet publish "src/OSService.Api/OSService.Api.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore

# Etapa 2 - Execução da API
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

WORKDIR /app

EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "OSService.Api.dll"]
