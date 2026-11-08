using System.Diagnostics;
using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OSService.Application.Saga;

namespace OSService.Infrastructure.Messaging;

public class SagaSqsConsumerBackgroundService(
    IServiceScopeFactory scopeFactory,
    IServiceProvider serviceProvider,
    IConfiguration configuration,
    ILogger<SagaSqsConsumerBackgroundService> logger) : BackgroundService
{
    public const string ActivitySourceName = "os-service";
    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var queueUrl = configuration["AWS:SagaSqsQueueUrl"];
        var sqsClient = serviceProvider.GetService<IAmazonSQS>();

        if (string.IsNullOrWhiteSpace(queueUrl) || sqsClient == null)
        {
            logger.LogInformation(
                "Saga SQS queue URL not configured or SQS client unavailable. SagaSqsConsumer disabled.");
            return;
        }

        logger.LogInformation("SagaSqsConsumer listening on {QueueUrl}", queueUrl);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var response = await sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
                    {
                        QueueUrl = queueUrl,
                        MaxNumberOfMessages = 10,
                        WaitTimeSeconds = 20,
                        MessageAttributeNames = ["All"]
                    }, stoppingToken);

                    foreach (var message in response.Messages ?? [])
                    {
                        await ProcessMessageAsync(sqsClient, message, queueUrl, stoppingToken);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Error receiving Saga messages from SQS");
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("SagaSqsConsumer stopped");
        }
    }

    public async Task ProcessMessageAsync(
        IAmazonSQS sqsClient,
        Message message,
        string queueUrl,
        CancellationToken cancellationToken)
    {
        var (envelope, snsTraceParent) = TryParseEnvelope(message);

        string? parentId = null;
        if (message.MessageAttributes != null &&
            message.MessageAttributes.TryGetValue("traceparent", out var traceAttr))
        {
            parentId = traceAttr.StringValue;
        }
        else
        {
            parentId = snsTraceParent;
        }

        using var activity = ActivitySource.StartActivity("saga.sqs.process", ActivityKind.Consumer, parentId);

        try
        {
            if (envelope == null || string.IsNullOrWhiteSpace(envelope.EventType))
            {
                logger.LogWarning(
                    "Saga message {MessageId} skipped: invalid envelope or empty EventType",
                    message.MessageId);
                await DeleteMessageAsync(sqsClient, message.ReceiptHandle, queueUrl, cancellationToken);
                return;
            }

            activity?.SetTag("messaging.event_type", envelope.EventType);
            activity?.SetTag("messaging.correlation_id", envelope.CorrelationId);

            using var scope = scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<SagaEventProcessor>();

            await processor.ProcessAsync(envelope, cancellationToken);

            await DeleteMessageAsync(sqsClient, message.ReceiptHandle, queueUrl, cancellationToken);
            logger.LogInformation(
                "Saga message {MessageId} ({EventType}) processed and deleted",
                message.MessageId,
                envelope.EventType);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            logger.LogError(
                ex,
                "Error processing Saga message {MessageId}. It will be returned to queue / DLQ.",
                message.MessageId);
        }
    }

    private (EventEnvelope? Envelope, string? TraceParent) TryParseEnvelope(Message message)
    {
        if (string.IsNullOrWhiteSpace(message.Body))
            return (null, null);

        try
        {
            using var doc = JsonDocument.Parse(message.Body);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("Type", out var typeProp) &&
                typeProp.ValueKind == JsonValueKind.String &&
                string.Equals(typeProp.GetString(), "Notification", StringComparison.OrdinalIgnoreCase) &&
                root.TryGetProperty("Message", out var innerMessageProp) &&
                innerMessageProp.ValueKind == JsonValueKind.String)
            {
                string? snsTraceParent = null;
                if (root.TryGetProperty("MessageAttributes", out var attrsProp) &&
                    attrsProp.ValueKind == JsonValueKind.Object &&
                    attrsProp.TryGetProperty("traceparent", out var tpProp) &&
                    tpProp.ValueKind == JsonValueKind.Object &&
                    tpProp.TryGetProperty("Value", out var valProp) &&
                    valProp.ValueKind == JsonValueKind.String)
                {
                    snsTraceParent = valProp.GetString();
                }

                var innerJson = innerMessageProp.GetString();
                var unwrapped = string.IsNullOrWhiteSpace(innerJson)
                    ? null
                    : JsonSerializer.Deserialize<EventEnvelope>(innerJson, SagaEventJson.Options);

                return (unwrapped, snsTraceParent);
            }

            var envelope = JsonSerializer.Deserialize<EventEnvelope>(message.Body, SagaEventJson.Options);
            return (envelope, null);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Unable to deserialize Saga message {MessageId} as EventEnvelope", message.MessageId);
            return (null, null);
        }
    }

    private static async Task DeleteMessageAsync(
        IAmazonSQS sqsClient,
        string receiptHandle,
        string queueUrl,
        CancellationToken cancellationToken)
    {
        await sqsClient.DeleteMessageAsync(new DeleteMessageRequest
        {
            QueueUrl = queueUrl,
            ReceiptHandle = receiptHandle
        }, cancellationToken);
    }
}
