using System.Diagnostics;
using System.Text.Json;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OSService.Application.Saga;
using OSService.Domain.Entities;
using OSService.Domain.Enum;
using OSService.Domain.Interface;
using OSService.Domain.Interfaces;
using OSService.Infrastructure.Messaging;
using Xunit;

namespace OSService.Tests.Messaging;

public class SagaMessagingTests
{
    [Fact]
    public async Task SnsSagaEventPublisher_WhenSnsConfigured_ShouldPublishToTopicArnWithAttributes()
    {
        var snsMock = new Mock<IAmazonSimpleNotificationService>();
        PublishRequest? capturedRequest = null;

        snsMock
            .Setup(x => x.PublishAsync(It.IsAny<PublishRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PublishRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new PublishResponse { MessageId = "sns-msg-1" });

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AWS:SnsTopicArnPrefix"] = "arn:aws:sns:us-east-1:123456789012:oficina"
            })
            .Build();

        var publisher = new SnsSagaEventPublisher(
            config,
            NullLogger<SnsSagaEventPublisher>.Instance,
            snsMock.Object);

        using var activity = new Activity("TestSagaPublish").Start();
        var orderId = Guid.NewGuid();

        await publisher.PublishAsync(
            SagaEventTypes.BudgetApproved,
            orderId,
            new BudgetApprovedPayload(orderId, 500m, [], []));

        Assert.NotNull(capturedRequest);
        Assert.Equal("arn:aws:sns:us-east-1:123456789012:oficina-BudgetApproved", capturedRequest.TopicArn);
        Assert.Equal("BudgetApproved", capturedRequest.MessageAttributes["eventType"].StringValue);
        Assert.Equal(activity.Id, capturedRequest.MessageAttributes["traceparent"].StringValue);

        var envelope = JsonSerializer.Deserialize<EventEnvelope>(capturedRequest.Message, SagaEventJson.Options);
        Assert.NotNull(envelope);
        Assert.Equal(SagaEventTypes.BudgetApproved, envelope.EventType);
        Assert.Equal(orderId, envelope.CorrelationId);
    }

    [Fact]
    public async Task SnsSagaEventPublisher_WhenSnsUnavailable_ShouldLogLocallyWithoutThrowing()
    {
        var config = new ConfigurationBuilder().Build();
        var publisher = new SnsSagaEventPublisher(
            config,
            NullLogger<SnsSagaEventPublisher>.Instance,
            sns: null);

        var exception = await Record.ExceptionAsync(() =>
            publisher.PublishAsync(SagaEventTypes.ServiceOrderRejected, Guid.NewGuid(), new ServiceOrderRejectedPayload(Guid.NewGuid())));

        Assert.Null(exception);
    }

    [Fact]
    public async Task SagaSqsConsumer_ProcessMessageAsync_ShouldProcessBothRawAndSnsWrappedEnvelopes()
    {
        var order = new ServiceOrder(Guid.NewGuid(), Guid.NewGuid());
        order.StartDiagnostics();
        order.FinishAnalysis(200m);
        order.Approve();

        var repoMock = new Mock<IServiceOrderRepository>();
        repoMock.Setup(x => x.GetByIdAsync(order.Id)).ReturnsAsync(order);

        var services = new ServiceCollection();
        services.AddScoped(_ => new SagaEventProcessor(repoMock.Object));
        var provider = services.BuildServiceProvider();

        var config = new ConfigurationBuilder().Build();
        var consumer = new SagaSqsConsumerBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider,
            config,
            NullLogger<SagaSqsConsumerBackgroundService>.Instance);

        var sqsMock = new Mock<IAmazonSQS>();
        sqsMock
            .Setup(x => x.DeleteMessageAsync(It.IsAny<DeleteMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeleteMessageResponse());

        var envelope = SagaEventJson.CreateEnvelope(
            SagaEventTypes.PaymentConfirmed,
            order.Id,
            new PaymentConfirmedPayload(order.Id, "PREF-SNS-WRAP"));

        var snsWrappedBody = JsonSerializer.Serialize(new
        {
            Type = "Notification",
            MessageId = "sns-1",
            Message = JsonSerializer.Serialize(envelope, SagaEventJson.Options),
            MessageAttributes = new
            {
                traceparent = new { Type = "String", Value = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01" }
            }
        });

        var sqsMessage = new Message
        {
            MessageId = "sqs-1",
            ReceiptHandle = "rh-1",
            Body = snsWrappedBody
        };

        await consumer.ProcessMessageAsync(sqsMock.Object, sqsMessage, "https://sqs/saga", CancellationToken.None);

        Assert.Equal(OrderStatus.InProgress, order.GetCurrentStatus());
        Assert.True(order.IsPaid);
        sqsMock.Verify(
            x => x.DeleteMessageAsync(
                It.Is<DeleteMessageRequest>(r => r.ReceiptHandle == "rh-1" && r.QueueUrl == "https://sqs/saga"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SagaSqsConsumer_ProcessMessageAsync_WhenInvalidJson_ShouldDiscardAndDelete()
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => new SagaEventProcessor(new Mock<IServiceOrderRepository>().Object));
        var provider = services.BuildServiceProvider();

        var consumer = new SagaSqsConsumerBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider,
            new ConfigurationBuilder().Build(),
            NullLogger<SagaSqsConsumerBackgroundService>.Instance);

        var sqsMock = new Mock<IAmazonSQS>();

        await consumer.ProcessMessageAsync(
            sqsMock.Object,
            new Message { MessageId = "bad-1", ReceiptHandle = "rh-bad", Body = "{not-valid-json" },
            "https://sqs/saga",
            CancellationToken.None);

        sqsMock.Verify(x => x.DeleteMessageAsync(It.IsAny<DeleteMessageRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SnsSagaEventPublisher_PublishRawAsync_WithExplicitTraceParent_ShouldForwardTraceParentAttribute()
    {
        var snsMock = new Mock<IAmazonSimpleNotificationService>();
        PublishRequest? capturedRequest = null;

        snsMock
            .Setup(x => x.PublishAsync(It.IsAny<PublishRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PublishRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new PublishResponse { MessageId = "sns-raw-1" });

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AWS:SnsTopicArnPrefix"] = "arn:aws:sns:us-east-1:123456789012:oficina"
            })
            .Build();

        var publisher = new SnsSagaEventPublisher(
            config,
            NullLogger<SnsSagaEventPublisher>.Instance,
            snsMock.Object);

        const string traceParent = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";
        await publisher.PublishRawAsync("BudgetApproved", "{\"eventId\":\"1\"}", traceParent);

        Assert.NotNull(capturedRequest);
        Assert.Equal("arn:aws:sns:us-east-1:123456789012:oficina-BudgetApproved", capturedRequest.TopicArn);
        Assert.Equal("BudgetApproved", capturedRequest.MessageAttributes["eventType"].StringValue);
        Assert.Equal(traceParent, capturedRequest.MessageAttributes["traceparent"].StringValue);
    }

    [Fact]
    public async Task OutboxPublisherBackgroundService_PublishPendingAsync_ShouldPublishAndMarkPublished()
    {
        var outboxMessage = new OutboxMessage(
            Guid.NewGuid(),
            SagaEventTypes.BudgetApproved,
            Guid.NewGuid(),
            "{\"eventType\":\"BudgetApproved\"}",
            "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01");

        var outboxRepoMock = new Mock<IOutboxRepository>();
        outboxRepoMock
            .Setup(x => x.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([outboxMessage]);

        var sagaPublisherMock = new Mock<ISagaEventPublisher>();

        var services = new ServiceCollection();
        services.AddScoped(_ => outboxRepoMock.Object);
        services.AddScoped(_ => sagaPublisherMock.Object);
        using var provider = services.BuildServiceProvider();

        var worker = new OutboxPublisherBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<OutboxPublisherBackgroundService>.Instance);

        var allPublished = await worker.PublishPendingAsync(CancellationToken.None);

        Assert.True(allPublished);
        sagaPublisherMock.Verify(
            x => x.PublishRawAsync(
                outboxMessage.EventType,
                outboxMessage.Content,
                outboxMessage.TraceParent,
                It.IsAny<CancellationToken>()),
            Times.Once);
        outboxRepoMock.Verify(
            x => x.MarkPublishedAsync(outboxMessage.Id, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OutboxPublisherBackgroundService_WhenPublisherFails_ShouldMarkFailedAndReturnFalse()
    {
        var outboxMessage = new OutboxMessage(
            Guid.NewGuid(),
            SagaEventTypes.ServiceOrderApproved,
            Guid.NewGuid(),
            "{\"eventType\":\"ServiceOrderApproved\"}");

        var outboxRepoMock = new Mock<IOutboxRepository>();
        outboxRepoMock
            .Setup(x => x.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([outboxMessage]);

        var sagaPublisherMock = new Mock<ISagaEventPublisher>();
        sagaPublisherMock
            .Setup(x => x.PublishRawAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SNS unreachable"));

        var services = new ServiceCollection();
        services.AddScoped(_ => outboxRepoMock.Object);
        services.AddScoped(_ => sagaPublisherMock.Object);
        using var provider = services.BuildServiceProvider();

        var worker = new OutboxPublisherBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<OutboxPublisherBackgroundService>.Instance);

        var allPublished = await worker.PublishPendingAsync(CancellationToken.None);

        Assert.False(allPublished);
        outboxRepoMock.Verify(
            x => x.MarkFailedAsync(outboxMessage.Id, "SNS unreachable", It.IsAny<CancellationToken>()),
            Times.Once);
        outboxRepoMock.Verify(
            x => x.MarkPublishedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task OutboxPublisherBackgroundService_StartAndStopAsync_ShouldRunCleanly()
    {
        var outboxRepoMock = new Mock<IOutboxRepository>();
        outboxRepoMock
            .Setup(x => x.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var services = new ServiceCollection();
        services.AddScoped(_ => outboxRepoMock.Object);
        services.AddScoped(_ => new Mock<ISagaEventPublisher>().Object);
        using var provider = services.BuildServiceProvider();

        var worker = new OutboxPublisherBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<OutboxPublisherBackgroundService>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(50);
        await worker.StopAsync(CancellationToken.None);

        outboxRepoMock.Verify(x => x.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }
}
