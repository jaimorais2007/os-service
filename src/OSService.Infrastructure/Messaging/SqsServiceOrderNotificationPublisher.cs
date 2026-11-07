using System.Diagnostics;
using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OSService.Domain.Interface;

namespace OSService.Infrastructure.Messaging;

public class SqsServiceOrderNotificationPublisher : IServiceOrderNotificationPublisher
{
    private readonly IAmazonSQS? _sqs;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SqsServiceOrderNotificationPublisher> _logger;

    public SqsServiceOrderNotificationPublisher(
        IConfiguration configuration,
        ILogger<SqsServiceOrderNotificationPublisher> logger,
        IAmazonSQS? sqs = null)
    {
        _configuration = configuration;
        _logger = logger;
        _sqs = sqs;
    }

    public async Task PublishNotificationAsync(Guid customerId, Guid serviceOrderId, string status, CancellationToken cancellationToken = default)
    {
        var queueUrl = _configuration["AWS:SqsQueueUrl"];

        var payload = JsonSerializer.Serialize(new
        {
            customerId,
            serviceOrderId,
            status,
            occurredAt = DateTime.UtcNow
        });

        if (_sqs is null || string.IsNullOrWhiteSpace(queueUrl))
        {
            _logger.LogInformation("SQS queue URL not configured or SQS client unavailable; notification logged locally: {Payload}", payload);
            return;
        }

        try
        {
            var request = new SendMessageRequest
            {
                QueueUrl = queueUrl,
                MessageBody = payload
            };

            var traceParent = Activity.Current?.Id;
            if (!string.IsNullOrWhiteSpace(traceParent))
            {
                request.MessageAttributes["traceparent"] = new MessageAttributeValue
                {
                    DataType = "String",
                    StringValue = traceParent
                };
            }

            var response = await _sqs.SendMessageAsync(request, cancellationToken);
            _logger.LogInformation("Notification published to SQS for ServiceOrder {ServiceOrderId}, Customer {CustomerId} (MessageId: {MessageId})",
                serviceOrderId, customerId, response.MessageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish notification to SQS for ServiceOrder {ServiceOrderId} to queue {QueueUrl}",
                serviceOrderId, queueUrl);
        }
    }
}
