namespace CodexSpendMonitor.Models;

public sealed class ConversationSpendInfo
{
    public string ConversationId { get; init; } = string.Empty;
    public string Preview { get; init; } = "No user message yet";
    public string ModelName { get; init; } = "Unknown model";
    public string ModelProvider { get; init; } = "unknown";
    public string ResolvedModelId { get; init; } = string.Empty;
    public string SessionPath { get; init; } = string.Empty;
    public string WorkingDirectory { get; init; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; init; }
    public long InputTokens { get; init; }
    public long CachedInputTokens { get; init; }
    public long OutputTokens { get; init; }
    public long ReasoningTokens { get; init; }
    public decimal PromptPriceUsd { get; init; }
    public decimal CompletionPriceUsd { get; init; }
    public decimal TotalCostUsd { get; init; }
    public bool HasResolvedPricing { get; init; }
    public string PricingNote { get; init; } = string.Empty;
}
