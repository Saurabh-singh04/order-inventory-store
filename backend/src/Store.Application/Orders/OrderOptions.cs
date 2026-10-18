namespace Store.Application.Orders;

public sealed class OrderOptions
{
    public const string SectionName = "Orders";

    /// <summary>Pending orders older than this are cancelled automatically.</summary>
    public int PendingTimeoutMinutes { get; set; } = 30;

    /// <summary>How often the cleanup job looks for expired orders.</summary>
    public int CleanupIntervalSeconds { get; set; } = 60;

    public int MaxIdempotencyKeyLength { get; set; } = 100;
}
