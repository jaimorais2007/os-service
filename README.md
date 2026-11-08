# OS Service (Oficina Mecânica - Tech Challenge Fase 4)

Microsserviço responsável pelo ciclo de vida completo das **Ordens de Serviço (OS)** da oficina mecânica, construído em **.NET 8** com Clean Architecture, **PostgreSQL (EF Core)**, mensageria assíncrona (**AWS SNS + SQS**) e observabilidade distribuída (**OpenTelemetry + New Relic**).

---

## Saga Pattern Coreografado (Aceite de Orçamento, Pagamento e Estoque)

### Justificativa Arquitetural: Por que Saga Coreografada?

Para coordenar as transações distribuídas entre `os-service` (PostgreSQL) e `billing-service` (DynamoDB + Mercado Pago) sem acoplamento de banco de dados nem bloqueio HTTP síncrono, adotamos o **Saga Pattern Coreografado** baseado em eventos SNS/SQS:

1. **Baixo Acoplamento Temporal e de Domínio**: O `os-service` não conhece detalhes de preferências ou webhooks do Mercado Pago, nem da tabela DynamoDB de estoque; cada serviço reage apenas a eventos de domínio publicados no SNS.
2. **Ausência de Ponto Único de Falha (SPOF)**: Diferente da Saga Orquestrada (que exige um orquestrador centralizado mantendo estado global), cada microsserviço gerencia sua própria máquina de estados local de maneira autônoma.
3. **Transactional Outbox + Inbox Idempotente**:
   - **Outbox Transacional (`OutboxMessage`)**: Todos os eventos publicados pela Saga (`BudgetApproved`, `ServiceOrderRejected` e `ServiceOrderApproved`) são gravados na tabela `OutboxMessage` do PostgreSQL na **mesma transação (`SaveChangesAsync()`)** da mudança de estado da `ServiceOrder` (e/ou `ProcessedSagaEvent`), eliminando o problema de *dual-write* entre banco de dados e broker SNS. O worker `OutboxPublisherBackgroundService` faz *polling* periódico em lotes com *exponential backoff* (`3s` até `60s`), publica no tópico SNS correspondente via `SnsSagaEventPublisher` e remove o registro após confirmação (ou incrementa `Attempts` e grava `LastError` em caso de falha transitória).
   - **Inbox Idempotente (`ProcessedSagaEvent`)**: Mensagens consumidas via SQS são deduplicadas na tabela `ProcessedSagaEvent` (`EventId` único), evitando efeitos colaterais duplicados em reentregas (*at-least-once delivery*).
4. **Rastreabilidade Ponta a Ponta**: O cabeçalho W3C `traceparent` é capturado no momento da criação do `OutboxMessage` e propagado nos atributos SNS/SQS entre `os-service`, `billing-service` e `execution-service`.

---

### Máquina de Estados e Transações Compensatórias

Apenas os status com efeitos colaterais cruzados entre microsserviços participam da Saga (`AwaitingApproval`, `WaitingPayment`, `InProgress` e `Rejected`):

```text
[Received (1)] -> [UnderAnalysis (2)] -> [AwaitingApproval (3)] <======================+
                                            |              |                           |
                                 Reject()   |              | Approve()                 |
                                            v              | Publica "BudgetApproved"  |
                                      [Rejected (7)]       v                           |
                                            ^      [WaitingPayment (8)]                |
                                            |         |    |                           |
                                 Reject()   +---------+    +-> "PaymentCreationFailed" + (Compensacao 1A)
                         Publica "ServiceOrderRejected"    |                           |
                                                           +-> "PaymentExpired" -------+ (Compensacao 1B)
                                                           |                           |
                                                           +-> "PaymentConfirmed"      |
                                                                 |                     |
                                           Sem pecas na OS ------+-> [InProgress (4)]  |
                                                                 |          |          |
                                           Com pecas: publica ---+          |          |
                                           "ServiceOrderApproved"           |          |
                                                 |                          |          |
                                                 +-> Estoque OK             |          |
                                                 |   ("PartStockEnsured") --+          |
                                                 |                          |          |
                                                 +-> Falta estoque:         |          |
                                                     Billing estorna MP     |          |
                                                     ("PaymentRefunded") --------------+ (Compensacao 2)
                                                                            |
                                                                            v
                                                                     [Completed (5)] -> [Delivered (6)]
```

### Eventos da Saga no `os-service`

| Evento | Direção | Tópico SNS | Ação / Compensação no `os-service` |
|---|---|---|---|
| `BudgetApproved` | Publicado (Outbox) | `oficina-BudgetApproved` | Gravado atomicamente na tabela `OutboxMessage` em `POST /api/ServiceOrders/{id}/approve` ao mudar para `WaitingPayment`, e publicado no SNS pelo `OutboxPublisherBackgroundService`. |
| `PaymentCreated` | Consumido | `oficina-PaymentCreated` | Armazena `PaymentUrl` e `PaymentTransactionId` na OS. |
| `PaymentCreationFailed` | Consumido | `oficina-PaymentCreationFailed` | **Compensação 1A**: Reverte a OS de `WaitingPayment` para `AwaitingApproval`. |
| `PaymentExpired` | Consumido | `oficina-PaymentExpired` | **Compensação 1B**: Reverte a OS de `WaitingPayment` para `AwaitingApproval` após vencimento/recusa no gateway. |
| `ServiceOrderRejected` | Publicado (Outbox) | `oficina-ServiceOrderRejected` | Gravado no `OutboxMessage` quando o cliente recusa a OS durante `WaitingPayment`, cancelando o pagamento pendente no Billing. |
| `PaymentConfirmed` | Consumido | `oficina-PaymentConfirmed` | Marca `IsPaid = true`; se não houver peças avança para `InProgress`, caso contrário grava `ServiceOrderApproved` no `OutboxMessage` na mesma transação. |
| `ServiceOrderApproved` | Publicado (Outbox) | `oficina-ServiceOrderApproved` | Solicita reserva e baixa atômica das peças da OS paga no `billing-service`. |
| `PartStockEnsured` | Consumido | `oficina-PartStockEnsured` | Marca `StockQuantityWasEnsured = true` na peça; quando todas estiverem garantidas, avança a OS para `InProgress`. |
| `PaymentRefunded` | Consumido | `oficina-PaymentRefunded` | **Compensação 2**: Limpa `IsPaid = false`, reseta flags de estoque das peças e retroage a OS para `AwaitingApproval`. |

---

## Configuração AWS / Kubernetes

Variáveis definidas em `k8s/deployment.yaml` (secret `os-service-secret`):

- `ConnectionStrings__DefaultConnection` (`CONNECTION_STRING`): PostgreSQL do `os-service`.
- `AWS__Region`: `us-east-1`.
- `AWS__SqsQueueUrl` (`SQS_QUEUE_URL`): Fila de notificações de mudança de status consumida pelo `execution-service` (envio de e-mails).
- `AWS__SnsTopicArnPrefix`: `arn:aws:sns:us-east-1:168126498555:oficina`.
- `AWS__SagaSqsQueueUrl` (`SAGA_SQS_QUEUE_URL`): Fila SQS do `os-service` inscrita nos tópicos de retorno do `billing-service`.

---

## Observabilidade (OpenTelemetry + New Relic)

Traces, metricas e logs sao exportados via OTLP para o New Relic (`https://otlp.nr-data.net`).
Configuracao em `Configuration/OpenTelemetryConfiguration.cs`; variaveis `OTEL_*` definidas em `k8s/deployment.yaml`.
A license key vem do secret `os-service-secret`, chave `OTEL_EXPORTER_OTLP_HEADERS` (formato `api-key=<LICENSE_KEY>`).
O exporter so e ativado quando `OTEL_EXPORTER_OTLP_ENDPOINT` esta definido.
