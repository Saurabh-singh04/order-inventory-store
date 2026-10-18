using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Store.Application.Orders;

namespace Store.Infrastructure.BackgroundJobs;

/// <summary>
/// Cancels orders that have been Pending for longer than Orders:PendingTimeoutMinutes
/// and puts their stock back.
/// </summary>
public sealed class PendingOrderCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OrderOptions _options;
    private readonly ILogger<PendingOrderCleanupService> _logger;

    public PendingOrderCleanupService(
        IServiceScopeFactory scopeFactory,
        IOptions<OrderOptions> options,
        ILogger<PendingOrderCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(5, _options.CleanupIntervalSeconds));
        _logger.LogInformation(
            "Pending order cleanup started (timeout {Timeout} min, every {Interval})",
            _options.PendingTimeoutMinutes, interval);

        using var timer = new PeriodicTimer(interval);
        do
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var orders = scope.ServiceProvider.GetRequiredService<IOrderService>();

                var cancelled = await orders.CancelExpiredPendingOrdersAsync(stoppingToken);
                if (cancelled > 0)
                    _logger.LogInformation("Auto-cancelled {Count} expired pending orders", cancelled);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Never let one bad run kill the hosted service.
                _logger.LogError(ex, "Pending order cleanup failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
