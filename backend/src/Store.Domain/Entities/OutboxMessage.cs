namespace Store.Domain.Entities;

/// <summary>
/// Integration event saved in the same transaction as the business change
/// and published later by a background worker (transactional outbox).
/// </summary>
public class OutboxMessage
{
    public long Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
}
