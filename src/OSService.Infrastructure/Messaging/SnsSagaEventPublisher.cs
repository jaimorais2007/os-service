using System.Diagnostics;
using System.Text.Json;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OSService.Application.Saga;
using OSService.Domain.Interface;

namespace OSService.Infrastructure.Messaging;

public class SnsSagaEventPublisher : ISagaEventPublisher
{
    private readonly IAmazonSimpleNotificationService? _sns;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SnsSagaEventPublisher> _logger;

    public SnsSagaEventPublisher(
        IConfiguration configuration,
        ILogger<SnsSagaEventPublisher> logger,
        IAmazonSimpleNotificationService? sns = null)
    {
        _configuration = configuration;
        _logger = logger;
        _sns = sns;
    }

    public Task PublishAsync<TPayload>(
        string eventType,
        Guid correlationId,
        TPayload payload,
        CancellationToken cancellationToken = default)
    {
        var envelope = SagaEventJson.CreateEnvelope(eventType, correlationId, payload);
        var content = JsonSerializer.Serialize(envelope, SagaEventJson.Options);
        return PublishRawAsync(eventType, content, Activity.Current?.Id, cancellationToken);
    }

    public async Task PublishRawAsync(
        string eventType,
        string content,
        string? traceParent = null,
        CancellationToken cancellationToken = default)
    {
        var topicArnPrefix = _configuration["AWS:SnsTopicArnPrefix"];

        if (_sns is null || string.IsNullOrWhiteSpace(topicArnPrefix))
        {
            _logger.LogInformation(
                "[Simulated SNS] Saga event {EventType}: {Content}",
                eventType,
                content);
            return;
        }

        var request = new PublishRequest
        {
            TopicArn = $"{topicArnPrefix}-{eventType}",
            Message = content
        };

        request.MessageAttributes["eventType"] = new MessageAttributeValue
        {
            DataType = "String",
            StringValue = eventType
        };

        var effectiveTraceParent = !string.IsNullOrWhiteSpace(traceParent)
            ? traceParent
            : Activity.Current?.Id;

        if (!string.IsNullOrWhiteSpace(effectiveTraceParent))
        {
            request.MessageAttributes["traceparent"] = new MessageAttributeValue
            {
                DataType = "String",
                StringValue = effectiveTraceParent
            };
        }

        var response = await _sns.PublishAsync(request, cancellationToken);
        _logger.LogInformation(
            "Saga event {EventType} published to SNS topic {TopicArn} (MessageId: {MessageId})",
            eventType,
            request.TopicArn,
            response.MessageId);
    }
}
