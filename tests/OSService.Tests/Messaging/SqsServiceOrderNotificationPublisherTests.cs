using System.Diagnostics;
using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using OSService.Infrastructure.Messaging;
using Xunit;

namespace OSService.Tests.Messaging;

public class SqsServiceOrderNotificationPublisherTests
{
    private readonly Mock<IAmazonSQS> _sqsMock = new();
    private readonly Mock<ILogger<SqsServiceOrderNotificationPublisher>> _loggerMock = new();

    [Fact]
    public async Task PublishNotificationAsync_QuandoSqsForNulo_DeveLogarLocalmenteENaoLancarExcecao()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AWS:SqsQueueUrl"] = "https://sqs.us-east-1.amazonaws.com/123/queue"
            })
            .Build();

        var publisher = new SqsServiceOrderNotificationPublisher(config, _loggerMock.Object, sqs: null);

        var exception = await Record.ExceptionAsync(() =>
            publisher.PublishNotificationAsync(Guid.NewGuid(), Guid.NewGuid(), "Received"));

        Assert.Null(exception);
        _sqsMock.Verify(x => x.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task PublishNotificationAsync_QuandoQueueUrlNaoConfigurada_DeveLogarLocalmenteENaoChamarSqs(string? queueUrl)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AWS:SqsQueueUrl"] = queueUrl
            })
            .Build();

        var publisher = new SqsServiceOrderNotificationPublisher(config, _loggerMock.Object, _sqsMock.Object);

        var exception = await Record.ExceptionAsync(() =>
            publisher.PublishNotificationAsync(Guid.NewGuid(), Guid.NewGuid(), "UnderAnalysis"));

        Assert.Null(exception);
        _sqsMock.Verify(x => x.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PublishNotificationAsync_ComConfiguracaoValidaSemActivity_DevePublicarMensagemSemTraceParent()
    {
        const string queueUrl = "https://sqs.us-east-1.amazonaws.com/123/service-orders";
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AWS:SqsQueueUrl"] = queueUrl
            })
            .Build();

        SendMessageRequest? capturedRequest = null;
        _sqsMock.Setup(x => x.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .Callback<SendMessageRequest, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(new SendMessageResponse { MessageId = "msg-123" });

        var publisher = new SqsServiceOrderNotificationPublisher(config, _loggerMock.Object, _sqsMock.Object);

        var customerId = Guid.NewGuid();
        var serviceOrderId = Guid.NewGuid();

        // Garante que Activity.Current esteja nulo
        Activity.Current = null;

        await publisher.PublishNotificationAsync(customerId, serviceOrderId, "InProgress");

        Assert.NotNull(capturedRequest);
        Assert.Equal(queueUrl, capturedRequest.QueueUrl);
        Assert.False(capturedRequest.MessageAttributes.ContainsKey("traceparent"));

        using var doc = JsonDocument.Parse(capturedRequest.MessageBody);
        var root = doc.RootElement;
        Assert.Equal(customerId, root.GetProperty("customerId").GetGuid());
        Assert.Equal(serviceOrderId, root.GetProperty("serviceOrderId").GetGuid());
        Assert.Equal("InProgress", root.GetProperty("status").GetString());
        Assert.True(root.TryGetProperty("occurredAt", out _));
    }

    [Fact]
    public async Task PublishNotificationAsync_ComActivityAtiva_DeveIncluirTraceParentNosAtributos()
    {
        const string queueUrl = "https://sqs.us-east-1.amazonaws.com/123/service-orders";
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AWS:SqsQueueUrl"] = queueUrl
            })
            .Build();

        SendMessageRequest? capturedRequest = null;
        _sqsMock.Setup(x => x.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .Callback<SendMessageRequest, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(new SendMessageResponse { MessageId = "msg-456" });

        var publisher = new SqsServiceOrderNotificationPublisher(config, _loggerMock.Object, _sqsMock.Object);

        using var activity = new Activity("TestActivity").Start();

        await publisher.PublishNotificationAsync(Guid.NewGuid(), Guid.NewGuid(), "Completed");

        Assert.NotNull(capturedRequest);
        Assert.True(capturedRequest.MessageAttributes.ContainsKey("traceparent"));
        Assert.Equal(activity.Id, capturedRequest.MessageAttributes["traceparent"].StringValue);
    }

    [Fact]
    public async Task PublishNotificationAsync_QuandoSqsLancarExcecao_DeveCapturarELogarSemPropagarErro()
    {
        const string queueUrl = "https://sqs.us-east-1.amazonaws.com/123/service-orders";
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AWS:SqsQueueUrl"] = queueUrl
            })
            .Build();

        _sqsMock.Setup(x => x.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonSQSException("Falha de rede com o serviço SQS"));

        var publisher = new SqsServiceOrderNotificationPublisher(config, _loggerMock.Object, _sqsMock.Object);

        var exception = await Record.ExceptionAsync(() =>
            publisher.PublishNotificationAsync(Guid.NewGuid(), Guid.NewGuid(), "Delivered"));

        Assert.Null(exception);
    }
}
