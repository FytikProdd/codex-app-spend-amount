using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using CodexSpendMonitor.Models;

namespace CodexSpendMonitor.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private string _headerCost = "$0.0000";
    private string _headerSummary = "Scanning conversations...";
    private string _syncStatus = "Waiting for OpenRouter sync...";
    private string _footerStatus = "Starting up...";

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ConversationSpendRow> Conversations { get; } = new();

    public string HeaderCost
    {
        get => _headerCost;
        private set => SetField(ref _headerCost, value);
    }

    public string HeaderSummary
    {
        get => _headerSummary;
        private set => SetField(ref _headerSummary, value);
    }

    public string SyncStatus
    {
        get => _syncStatus;
        private set => SetField(ref _syncStatus, value);
    }

    public string FooterStatus
    {
        get => _footerStatus;
        private set => SetField(ref _footerStatus, value);
    }

    public void ApplySnapshot(DashboardSnapshot snapshot)
    {
        HeaderCost = FormatUsd(snapshot.TotalCostUsd);
        int unmatchedCount = snapshot.ConversationCount - snapshot.ResolvedPriceCount;
        HeaderSummary = $"{snapshot.ConversationCount} chats tracked, {snapshot.ResolvedPriceCount} matched, {unmatchedCount} unmatched.";
        SyncStatus = snapshot.LastPriceSyncAt is null
            ? "OpenRouter prices not synced yet."
            : $"OpenRouter sync: {snapshot.LastPriceSyncAt:dd MMM yyyy HH:mm:ss}";
        FooterStatus = snapshot.StatusText;

        Conversations.Clear();
        foreach (ConversationSpendInfo conversation in snapshot.Conversations)
        {
            Conversations.Add(new ConversationSpendRow
            {
                Preview = conversation.Preview,
                ModelLabel = $"{conversation.ModelProvider} / {conversation.ModelName}",
                TokensLabel = $"Input {conversation.InputTokens:N0} | Cached {conversation.CachedInputTokens:N0} | Output {conversation.OutputTokens:N0} | Reasoning {conversation.ReasoningTokens:N0}",
                StatusLabel = conversation.HasResolvedPricing
                    ? $"Matched via {conversation.PricingNote}"
                    : conversation.PricingNote,
                PricingLabel = conversation.HasResolvedPricing
                    ? $"Pricing: input {FormatUsdPerMillion(conversation.PromptPriceUsd)} / 1M | output {FormatUsdPerMillion(conversation.CompletionPriceUsd)} / 1M"
                    : "Pricing: unavailable",
                UpdatedLabel = conversation.UpdatedAt.ToString("HH:mm:ss"),
                CostLabel = FormatUsd(conversation.TotalCostUsd),
                PathLabel = string.IsNullOrWhiteSpace(conversation.WorkingDirectory)
                    ? conversation.SessionPath
                    : conversation.WorkingDirectory,
            });
        }
    }

    private static string FormatUsd(decimal value)
    {
        return value switch
        {
            >= 100m => value.ToString("$#,##0.00", CultureInfo.InvariantCulture),
            >= 1m => value.ToString("$0.0000", CultureInfo.InvariantCulture),
            >= 0.01m => value.ToString("$0.0000", CultureInfo.InvariantCulture),
            _ => value.ToString("$0.000000", CultureInfo.InvariantCulture),
        };
    }

    private static string FormatUsdPerMillion(decimal perTokenRate)
    {
        decimal perMillion = perTokenRate * 1_000_000m;
        return perMillion >= 1m
            ? perMillion.ToString("$#,##0.####", CultureInfo.InvariantCulture)
            : perMillion.ToString("$0.#######", CultureInfo.InvariantCulture);
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
