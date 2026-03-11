using System.Globalization;
using System.Text.Json;
using CodexSpendMonitor.Models;

namespace CodexSpendMonitor.Services;

public sealed class SpendMonitorService : IDisposable
{
    private static readonly Uri OpenRouterModelsUri = new("https://openrouter.ai/api/v1/models");
    private static readonly TimeSpan AutomaticRefreshDebounce = TimeSpan.FromMilliseconds(900);
    private static readonly TimeSpan AutomaticRefreshCooldown = TimeSpan.FromSeconds(3);

    private readonly HttpClient _httpClient;
    private readonly IReadOnlyList<string> _sessionRoots;
    private readonly List<FileSystemWatcher> _watchers = new();
    private readonly Timer _debounceTimer;
    private readonly object _refreshLock = new();

    private Dictionary<string, ModelPricing> _pricingCatalog = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, CachedConversation> _conversationCache = new(StringComparer.OrdinalIgnoreCase);
    private DateTimeOffset? _lastPriceSyncAt;
    private DateTimeOffset _lastAutomaticRefreshAt = DateTimeOffset.MinValue;
    private string _statusText = "Loading sessions...";
    private bool _isDisposed;
    private bool _refreshInProgress;
    private bool _refreshPending;
    private bool _forceRefreshPending;

    public event EventHandler<DashboardSnapshot>? SnapshotUpdated;

    public SpendMonitorService()
    {
        _sessionRoots = DiscoverSessionRoots();
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("CodexSpendMonitor/1.0");
        _debounceTimer = new Timer(_ => _ = QueueSessionRefreshAsync(forceRefresh: false), null, Timeout.Infinite, Timeout.Infinite);
    }

    public async Task InitializeAsync()
    {
        await RefreshPricingAsync();
        SetupWatchers();
        LocalLog.Write("Monitoring session roots: " + string.Join(", ", _sessionRoots));
        _ = Task.Run(PricingRefreshLoopAsync);
    }

    public async Task RefreshPricingAsync()
    {
        try
        {
            using HttpResponseMessage response = await _httpClient.GetAsync(OpenRouterModelsUri);
            response.EnsureSuccessStatusCode();

            await using Stream contentStream = await response.Content.ReadAsStreamAsync();
            using JsonDocument document = await JsonDocument.ParseAsync(contentStream);

            Dictionary<string, ModelPricing> nextCatalog = new(StringComparer.OrdinalIgnoreCase);
            if (document.RootElement.TryGetProperty("data", out JsonElement dataElement))
            {
                foreach (JsonElement item in dataElement.EnumerateArray())
                {
                    string id = GetString(item, "id");
                    if (string.IsNullOrWhiteSpace(id))
                    {
                        continue;
                    }

                    JsonElement pricingElement = item.TryGetProperty("pricing", out JsonElement pricing) ? pricing : default;
                    var modelPricing = new ModelPricing
                    {
                        Id = id,
                        CanonicalSlug = GetString(item, "canonical_slug"),
                        Name = GetString(item, "name"),
                        Prompt = ParseDecimal(pricingElement, "prompt"),
                        Completion = ParseDecimal(pricingElement, "completion"),
                    };

                    nextCatalog[id] = modelPricing;
                    if (!string.IsNullOrWhiteSpace(modelPricing.CanonicalSlug))
                    {
                        nextCatalog[modelPricing.CanonicalSlug] = modelPricing;
                    }
                }
            }

            _pricingCatalog = nextCatalog;
            _lastPriceSyncAt = DateTimeOffset.Now;
            _statusText = $"Prices synced from OpenRouter at {_lastPriceSyncAt:HH:mm:ss}.";
        }
        catch (Exception ex)
        {
            _statusText = $"OpenRouter sync failed: {ex.Message}";
        }

        await QueueSessionRefreshAsync(forceRefresh: true);
    }

    public async Task RefreshSessionsAsync()
    {
        await QueueSessionRefreshAsync(forceRefresh: true);
    }

    private async Task QueueSessionRefreshAsync(bool forceRefresh)
    {
        lock (_refreshLock)
        {
            _refreshPending = true;
            _forceRefreshPending |= forceRefresh;
            if (_refreshInProgress)
            {
                return;
            }

            _refreshInProgress = true;
        }

        try
        {
            while (true)
            {
                bool forceThisRun;
                lock (_refreshLock)
                {
                    if (!_refreshPending)
                    {
                        break;
                    }

                    _refreshPending = false;
                    forceThisRun = _forceRefreshPending;
                    _forceRefreshPending = false;
                }

                if (!forceThisRun)
                {
                    TimeSpan cooldownLeft = AutomaticRefreshCooldown - (DateTimeOffset.UtcNow - _lastAutomaticRefreshAt);
                    if (cooldownLeft > TimeSpan.Zero)
                    {
                        await Task.Delay(cooldownLeft);
                    }

                    _lastAutomaticRefreshAt = DateTimeOffset.UtcNow;
                }

                DashboardSnapshot snapshot = await Task.Run(() => BuildSnapshot(forceThisRun));
                PublishSnapshot(snapshot);
            }
        }
        catch (Exception ex)
        {
            PublishSnapshot(new DashboardSnapshot
            {
                Conversations = Array.Empty<ConversationSpendInfo>(),
                TotalCostUsd = 0m,
                ConversationCount = 0,
                ResolvedPriceCount = 0,
                LastPriceSyncAt = _lastPriceSyncAt,
                PriceSource = OpenRouterModelsUri.ToString(),
                StatusText = $"Session refresh failed: {ex.Message}",
            });
        }
        finally
        {
            bool shouldRestart;
            lock (_refreshLock)
            {
                shouldRestart = _refreshPending;
                _refreshInProgress = false;
            }

            if (shouldRestart && !_isDisposed)
            {
                _ = QueueSessionRefreshAsync(forceRefresh: false);
            }
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        foreach (FileSystemWatcher watcher in _watchers)
        {
            watcher.Dispose();
        }

        _debounceTimer.Dispose();
        _httpClient.Dispose();
    }

    private async Task PricingRefreshLoopAsync()
    {
        using PeriodicTimer timer = new(TimeSpan.FromHours(1));
        while (await timer.WaitForNextTickAsync())
        {
            if (_isDisposed)
            {
                return;
            }

            await RefreshPricingAsync();
        }
    }

    private void SetupWatchers()
    {
        foreach (string root in _sessionRoots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            var watcher = new FileSystemWatcher(root, "*.jsonl")
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true,
            };

            watcher.Changed += OnSessionFilesChanged;
            watcher.Created += OnSessionFilesChanged;
            watcher.Deleted += OnSessionFilesChanged;
            watcher.Renamed += OnSessionFilesChanged;
            _watchers.Add(watcher);
        }
    }

    private void OnSessionFilesChanged(object sender, FileSystemEventArgs e)
    {
        lock (_refreshLock)
        {
            _debounceTimer.Change(AutomaticRefreshDebounce, Timeout.InfiniteTimeSpan);
        }
    }

    private static IReadOnlyList<string> DiscoverSessionRoots()
    {
        var roots = new List<string>();
        var uniqueRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddDirectRoot(Environment.GetEnvironmentVariable("CODEX_SESSIONS_DIR"));
        AddDirectRoot(Environment.GetEnvironmentVariable("CODEX_ARCHIVED_SESSIONS_DIR"));
        AddCodexHome(Environment.GetEnvironmentVariable("CODEX_HOME"));
        AddCodexHome(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex"));

        return roots;

        void AddCodexHome(string? codexHome)
        {
            if (string.IsNullOrWhiteSpace(codexHome))
            {
                return;
            }

            AddDirectRoot(Path.Combine(codexHome, "sessions"));
            AddDirectRoot(Path.Combine(codexHome, "archived_sessions"));
        }

        void AddDirectRoot(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            string normalizedPath = NormalizePath(path);
            if (uniqueRoots.Add(normalizedPath))
            {
                roots.Add(normalizedPath);
            }
        }
    }

    private IEnumerable<string> EnumerateSessionFiles()
    {
        foreach (string root in _sessionRoots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (string file in Directory.EnumerateFiles(root, "*.jsonl", SearchOption.AllDirectories))
            {
                yield return file;
            }
        }
    }

    private DashboardSnapshot BuildSnapshot(bool forceRefresh)
    {
        List<ConversationSpendInfo> conversations = new();
        Dictionary<string, CachedConversation> nextCache = new(StringComparer.OrdinalIgnoreCase);

        foreach (string file in EnumerateSessionFiles())
        {
            try
            {
                CachedConversation cachedConversation = GetCachedConversation(file, forceRefresh);
                nextCache[file] = cachedConversation;
                if (cachedConversation.Conversation is not null)
                {
                    conversations.Add(cachedConversation.Conversation);
                }
            }
            catch (IOException)
            {
                // Ignore transient file access issues while Codex is still writing the session file.
            }
            catch (UnauthorizedAccessException)
            {
                // Skip inaccessible files rather than failing the full dashboard refresh.
            }
        }

        _conversationCache = nextCache;
        conversations.Sort((left, right) => right.UpdatedAt.CompareTo(left.UpdatedAt));

        return new DashboardSnapshot
        {
            Conversations = conversations,
            TotalCostUsd = conversations.Sum(item => item.TotalCostUsd),
            ConversationCount = conversations.Count,
            ResolvedPriceCount = conversations.Count(item => item.HasResolvedPricing),
            LastPriceSyncAt = _lastPriceSyncAt,
            PriceSource = OpenRouterModelsUri.ToString(),
            StatusText = _statusText,
        };
    }

    private CachedConversation GetCachedConversation(string filePath, bool forceRefresh)
    {
        var fileInfo = new FileInfo(filePath);
        long length = fileInfo.Exists ? fileInfo.Length : 0;
        DateTimeOffset lastWriteUtc = fileInfo.Exists
            ? fileInfo.LastWriteTimeUtc
            : DateTimeOffset.MinValue;

        if (!forceRefresh &&
            _conversationCache.TryGetValue(filePath, out CachedConversation? cachedConversation) &&
            cachedConversation.Length == length &&
            cachedConversation.LastWriteUtc == lastWriteUtc)
        {
            return cachedConversation;
        }

        return new CachedConversation(lastWriteUtc, length, TryParseConversation(filePath));
    }

    private ConversationSpendInfo? TryParseConversation(string filePath)
    {
        try
        {
            string conversationId = Path.GetFileNameWithoutExtension(filePath);
            string preview = string.Empty;
            string modelName = string.Empty;
            string modelProvider = string.Empty;
            string workingDirectory = string.Empty;
            DateTimeOffset updatedAt = File.GetLastWriteTimeUtc(filePath);
            long inputTokens = 0;
            long cachedInputTokens = 0;
            long outputTokens = 0;
            long reasoningTokens = 0;

            foreach (string line in File.ReadLines(filePath))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    using JsonDocument document = JsonDocument.Parse(line);
                    JsonElement root = document.RootElement;

                    if (root.TryGetProperty("timestamp", out JsonElement timestampElement) &&
                        timestampElement.ValueKind == JsonValueKind.String &&
                        DateTimeOffset.TryParse(timestampElement.GetString(), out DateTimeOffset parsedTimestamp))
                    {
                        updatedAt = parsedTimestamp;
                    }

                    string recordType = GetString(root, "type");
                    if (!root.TryGetProperty("payload", out JsonElement payload))
                    {
                        continue;
                    }

                    switch (recordType)
                    {
                        case "session_meta":
                            conversationId = GetString(payload, "id", conversationId);
                            modelProvider = GetString(payload, "model_provider", modelProvider);
                            workingDirectory = GetString(payload, "cwd", workingDirectory);
                            break;
                        case "turn_context":
                            modelName = GetString(payload, "model", modelName);
                            break;
                        case "event_msg":
                            ParseEventMessage(payload, ref preview, ref inputTokens, ref cachedInputTokens, ref outputTokens, ref reasoningTokens);
                            break;
                    }
                }
                catch (JsonException)
                {
                    // Ignore partial/in-flight lines while the session file is still being written.
                }
            }

            if (string.IsNullOrWhiteSpace(preview))
            {
                preview = conversationId;
            }

            ModelPricing? pricing = ResolvePricing(modelProvider, modelName);
            bool hasResolvedPricing = pricing is not null;
            decimal totalCostUsd = 0m;
            string pricingNote;

            if (pricing is null)
            {
                pricingNote = $"No OpenRouter price match for {ComposeModelKey(modelProvider, modelName)}";
            }
            else
            {
                totalCostUsd += inputTokens * pricing.Prompt;
                totalCostUsd += outputTokens * pricing.Completion;

                pricingNote = pricing.Id;
            }

            return new ConversationSpendInfo
            {
                ConversationId = conversationId,
                Preview = preview,
                ModelName = string.IsNullOrWhiteSpace(modelName) ? "Unknown model" : modelName,
                ModelProvider = string.IsNullOrWhiteSpace(modelProvider) ? "unknown" : modelProvider,
                ResolvedModelId = pricing?.Id ?? string.Empty,
                SessionPath = filePath,
                WorkingDirectory = workingDirectory,
                UpdatedAt = updatedAt.ToLocalTime(),
                InputTokens = inputTokens,
                CachedInputTokens = cachedInputTokens,
                OutputTokens = outputTokens,
                ReasoningTokens = reasoningTokens,
                PromptPriceUsd = pricing?.Prompt ?? 0m,
                CompletionPriceUsd = pricing?.Completion ?? 0m,
                TotalCostUsd = totalCostUsd,
                HasResolvedPricing = hasResolvedPricing,
                PricingNote = pricingNote,
            };
        }
        catch
        {
            return null;
        }
    }

    private static string NormalizePath(string path)
    {
        string fullPath = Path.GetFullPath(path);
        return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private void ParseEventMessage(
        JsonElement payload,
        ref string preview,
        ref long inputTokens,
        ref long cachedInputTokens,
        ref long outputTokens,
        ref long reasoningTokens)
    {
        string eventType = GetString(payload, "type");
        if (eventType == "user_message" && string.IsNullOrWhiteSpace(preview))
        {
            preview = Condense(GetString(payload, "message", "No user message yet"));
            return;
        }

        if (eventType != "token_count" ||
            !payload.TryGetProperty("info", out JsonElement infoElement) ||
            infoElement.ValueKind != JsonValueKind.Object ||
            !infoElement.TryGetProperty("total_token_usage", out JsonElement usageElement))
        {
            return;
        }

        inputTokens = GetInt64(usageElement, "input_tokens");
        cachedInputTokens = GetInt64(usageElement, "cached_input_tokens");
        outputTokens = GetInt64(usageElement, "output_tokens");
        reasoningTokens = GetInt64(usageElement, "reasoning_output_tokens");
    }

    private ModelPricing? ResolvePricing(string provider, string model)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            return null;
        }

        if (_pricingCatalog.TryGetValue(model, out ModelPricing? directMatch))
        {
            return directMatch;
        }

        string providerKey = ComposeModelKey(provider, model);
        if (_pricingCatalog.TryGetValue(providerKey, out ModelPricing? providerMatch))
        {
            return providerMatch;
        }

        return _pricingCatalog.Values.FirstOrDefault(item =>
            item.Id.EndsWith("/" + model, StringComparison.OrdinalIgnoreCase) ||
            item.CanonicalSlug.EndsWith("/" + model, StringComparison.OrdinalIgnoreCase));
    }

    private void PublishSnapshot(DashboardSnapshot snapshot)
    {
        SnapshotUpdated?.Invoke(this, snapshot);
    }

    private static string ComposeModelKey(string provider, string model)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            return "unknown";
        }

        if (model.Contains('/'))
        {
            return model;
        }

        return string.IsNullOrWhiteSpace(provider) ? model : $"{provider}/{model}";
    }

    private static string Condense(string text)
    {
        string[] parts = text.Split(
            new[] { '\r', '\n', '\t' },
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(" ", parts).Trim();
    }

    private static string GetString(JsonElement element, string propertyName, string fallback = "")
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out JsonElement value) &&
            value.ValueKind == JsonValueKind.String)
        {
            return value.GetString() ?? fallback;
        }

        return fallback;
    }

    private static long GetInt64(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out JsonElement value) &&
            value.ValueKind == JsonValueKind.Number &&
            value.TryGetInt64(out long parsed))
        {
            return parsed;
        }

        return 0;
    }

    private static decimal ParseDecimal(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out JsonElement value))
        {
            return 0m;
        }

        string raw = value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? "0",
            JsonValueKind.Number => value.GetDecimal().ToString(CultureInfo.InvariantCulture),
            _ => "0",
        };

        if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal parsed))
        {
            return parsed < 0m ? 0m : parsed;
        }

        return 0m;
    }

    private sealed class ModelPricing
    {
        public string Id { get; init; } = string.Empty;
        public string CanonicalSlug { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public decimal Prompt { get; init; }
        public decimal Completion { get; init; }
    }

    private sealed class CachedConversation
    {
        public CachedConversation(DateTimeOffset lastWriteUtc, long length, ConversationSpendInfo? conversation)
        {
            LastWriteUtc = lastWriteUtc;
            Length = length;
            Conversation = conversation;
        }

        public DateTimeOffset LastWriteUtc { get; }
        public long Length { get; }
        public ConversationSpendInfo? Conversation { get; }
    }
}
