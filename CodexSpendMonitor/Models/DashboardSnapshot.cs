namespace CodexSpendMonitor.Models;

public sealed class DashboardSnapshot
{
    public IReadOnlyList<ConversationSpendInfo> Conversations { get; init; } = Array.Empty<ConversationSpendInfo>();
    public decimal TotalCostUsd { get; init; }
    public int ResolvedPriceCount { get; init; }
    public int ConversationCount { get; init; }
    public DateTimeOffset? LastPriceSyncAt { get; init; }
    public string PriceSource { get; init; } = "https://openrouter.ai/api/v1/models";
    public string StatusText { get; init; } = string.Empty;
}
