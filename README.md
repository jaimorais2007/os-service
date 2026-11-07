# OS Service (Ordem de Serviço)

[![Quality Gate](https://sonarcloud.io/api/project_badges/measure?project=jaimorais2007_os-service&metric=alert_status)](https://sonarcloud.io/summary/overall?id=jaimorais2007_os-service) [![Cobertura](https://sonarcloud.io/api/project_badges/measure?project=jaimorais2007_os-service&metric=coverage)](https://sonarcloud.io/component_measures?id=jaimorais2007_os-service&metric=coverage) [![CI/CD](https://github.com/jaimorais2007/os-service/actions/workflows/ci-cd.yml/badge.svg?branch=main)](https://github.com/jaimorais2007/os-service/actions/workflows/ci-cd.yml)

## Qualidade e cobertura de testes

A cada PR e a cada merge na `main`, a pipeline ([`ci-templates`](https://github.com/jaimorais2007/ci-templates)) roda os testes com cobertura e:

- **falha se a cobertura de linhas ficar abaixo de 80%** (passo "Checar cobertura mínima");
- envia a análise ao **SonarCloud** e **aguarda o quality gate** (`sonar.qualitygate.wait=true`): se o gate reprovar, a pipeline falha;
- publica o relatório detalhado no resumo da execução e como artefato `coverage-report-os-service` ([execuções da pipeline](https://github.com/jaimorais2007/os-service/actions/workflows/ci-cd.yml)).

A `main` é protegida: só aceita PR com a verificação `pipeline / Build, testes e qualidade` aprovada.

**Evidência:** cobertura de **98,6%** no SonarCloud em 07/11/2026, com quality gate aprovado. Os selos acima mostram o valor atual. Painel: [SonarCloud — os-service](https://sonarcloud.io/summary/overall?id=jaimorais2007_os-service).

## Observabilidade (OpenTelemetry + New Relic)

Traces, metricas e logs sao exportados via OTLP para o New Relic (`https://otlp.nr-data.net`).
Configuracao em `Configuration/OpenTelemetryConfiguration.cs`; variaveis `OTEL_*` definidas em `k8s/deployment.yaml`.
A license key vem do secret `os-service-secret`, chave `OTEL_EXPORTER_OTLP_HEADERS` (formato `api-key=<LICENSE_KEY>`).
O exporter so e ativado quando `OTEL_EXPORTER_OTLP_ENDPOINT` esta definido.
