using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Store.Application.Abstractions;
using Store.Application.Orders;
using Store.Infrastructure.Persistence;

namespace Store.Infrastructure.BackgroundJobs;

/// <summary>
/// Publishes outbox messages that were written together with the business change.
/// Delivery is at-least-once: a message is marked processed only after it was handled.
/// </summary>
public sealed class OutboxProcessor : BackgroundService
{
    private const int BatchSize = 20;
    private const int MaxAttempts = 5;
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _clock;
    private readonly ILogger<OutboxProcessor> _logger;

    public OutboxProcessor(IServiceScopeFactory scopeFactory, TimeProvider clock, ILogger<OutboxProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _clock = clock;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox processing failed");
            }
        }
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notifier = scope.ServiceProvider.GetRequiredService<IOrderNotifier>();

        var messages = await db.OutboxMessages
            .Where(m => m.ProcessedAt == null && m.Attempts < MaxAttempts)
            .OrderBy(m => m.Id)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (messages.Count == 0)
            return;

        foreach (var message in messages)
        {
            try
            {
                switch (message.Type)
                {
                    case OrderService.OrderPlacedMessageType:
                        var placed = JsonSerializer.Deserialize<OrderPlacedEvent>(message.Payload)
                            ?? throw new InvalidOperationException("Empty OrderPlaced payload");
                        await notifier.OrderPlacedAsync(placed, cancellationToken);
                        _logger.LogInformation(
                            "Order placed notification sent for order {OrderId} (customer {CustomerId}, total {Total})",
                            placed.OrderId, placed.CustomerId, placed.Total);
                        break;

                    default:
                        _logger.LogWarning("Unknown outbox message type {Type} (id {Id})", message.Type, message.Id);
                        break;
                }

                message.ProcessedAt = _clock.GetUtcNow().UtcDateTime;
                message.LastError = null;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                message.Attempts++;
                message.LastError = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
                _logger.LogWarning(ex, "Outbox message {Id} failed (attempt {Attempt})", message.Id, message.Attempts);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
