

## Observabilidade (OpenTelemetry + New Relic)

Traces, metricas e logs sao exportados via OTLP para o New Relic (`https://otlp.nr-data.net`).
Configuracao em `Configuration/OpenTelemetryConfiguration.cs`; variaveis `OTEL_*` definidas em `k8s/deployment.yaml`.
A license key vem do secret `os-service-secret`, chave `OTEL_EXPORTER_OTLP_HEADERS` (formato `api-key=<LICENSE_KEY>`).
O exporter so e ativado quando `OTEL_EXPORTER_OTLP_ENDPOINT` esta definido.


## Autenticação (JWT)

Todas as rotas exigem o token JWT emitido pela Lambda de autenticação (`POST /authenticate` no API Gateway),
mesmo quando o serviço é chamado direto, sem passar pelo Gateway. Ficam liberados sem token apenas `/health` e o Swagger.

O serviço valida assinatura (HS256), emissor (`oficina-api`), público (`oficina-clientes`) e validade.
A chave vem da variável `Jwt__Secret`, lida do secret `os-service-secret` (chave `Jwt__Secret`) no Kubernetes. Sem ela a aplicação não inicia.
Configuração em `Configuration/JwtAuthenticationConfiguration.cs`.
