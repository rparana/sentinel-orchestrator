namespace Sentinel.Infrastructure.Outbox;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    public int PollingIntervalMs { get; set; } = 200;
    public int BatchSize { get; set; } = 10;
    public int MaxRetries { get; set; } = 5;
}
