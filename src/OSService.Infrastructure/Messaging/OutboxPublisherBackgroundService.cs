using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OSService.Domain.Interface;

namespace OSService.Infrastructure.Messaging;

public class OutboxPublisherBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxPublisherBackgroundService> logger) : BackgroundService
{
    private const int BatchSize = 25;
    private static readonly TimeSpan BaseDelay = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan MaxDelay = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("OutboxPublisher background service started");

        var delay = BaseDelay;
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var allPublished = await PublishPendingAsync(stoppingToken);
                    delay = allPublished ? BaseDelay : Increase(delay);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Unexpected error in OutboxPublisher");
                    delay = Increase(delay);
                }

                await Task.Delay(delay, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("OutboxPublisher background service stopped");
        }
    }

    public async Task<bool> PublishPendingAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
        var publisher = scope.ServiceProvider.GetRequiredService<ISagaEventPublisher>();

        var pendingMessages = await outboxRepository.GetPendingAsync(BatchSize, cancellationToken);
        var allPublished = true;

        foreach (var message in pendingMessages)
        {
            try
            {
                await publisher.PublishRawAsync(
                    message.EventType,
                    message.Content,
                    message.TraceParent,
                    cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(
                    ex,
                    "Failed to publish outbox message {MessageId} of type {EventType}",
                    message.Id,
                    message.EventType);
                await outboxRepository.MarkFailedAsync(message.Id, ex.Message, cancellationToken);
                allPublished = false;
                continue;
            }

            await outboxRepository.MarkPublishedAsync(message.Id, cancellationToken);
        }

        return allPublished;
    }

    private static TimeSpan Increase(TimeSpan delay)
    {
        var next = delay * 2;
        return next > MaxDelay ? MaxDelay : next;
    }
}
