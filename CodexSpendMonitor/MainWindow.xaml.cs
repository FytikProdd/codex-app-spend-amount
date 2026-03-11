using System.Diagnostics;
using System.Globalization;
using CodexSpendMonitor.Models;
using CodexSpendMonitor.Services;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using WinRT.Interop;
using Windows.Graphics;
using Color = Windows.UI.Color;

namespace CodexSpendMonitor;

public sealed class MainWindow : Window
{
    private const double AnimatedWheelScrollStep = 144d;

    private readonly SpendMonitorService _monitorService = new();
    private AppWindow? _appWindow;
    private readonly Grid _rootLayout;
    private readonly TextBlock _headerCostText;
    private readonly TextBlock _headerSummaryText;
    private readonly TextBlock _syncStatusText;
    private readonly TextBlock _footerStatusText;
    private readonly ToggleSwitch _alwaysOnTopSwitch;
    private readonly ScrollViewer _conversationScrollViewer;
    private readonly StackPanel _conversationStack;
    private bool _initialized;

    public MainWindow()
    {
        Title = "Codex Spend Popout";
        _rootLayout = BuildLayout(
            out _headerCostText,
            out _headerSummaryText,
            out _syncStatusText,
            out _footerStatusText,
            out _alwaysOnTopSwitch,
            out _conversationScrollViewer,
            out _conversationStack);

        Content = _rootLayout;
        Activated += MainWindow_Activated;
        Closed += MainWindow_Closed;
        _monitorService.SnapshotUpdated += MonitorService_SnapshotUpdated;
    }

    private async void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;

        try
        {
            LocalLog.Write("MainWindow_Activated entered");
            ConfigureWindow();
            LocalLog.Write("ConfigureWindow completed");
            await _monitorService.InitializeAsync();
            LocalLog.Write("Monitor service initialized");
        }
        catch (Exception ex)
        {
            LocalLog.Write(ex, "MainWindow_Activated failed");
            throw;
        }
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        _monitorService.Dispose();
    }

    private void MonitorService_SnapshotUpdated(object? sender, DashboardSnapshot snapshot)
    {
        DispatcherQueue.TryEnqueue(() => ApplySnapshot(snapshot));
    }

    private async void RefreshChats_Click(object sender, RoutedEventArgs e)
    {
        await _monitorService.RefreshSessionsAsync();
    }

    private async void RefreshPrices_Click(object sender, RoutedEventArgs e)
    {
        await _monitorService.RefreshPricingAsync();
    }

    private void AlwaysOnTopSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_appWindow?.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = _alwaysOnTopSwitch.IsOn;
        }
    }

    private void OpenPricingSource_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://openrouter.ai/api/v1/models",
            UseShellExecute = true,
        });
    }

    private void ConfigureWindow()
    {
        _appWindow = GetAppWindow();
        if (_appWindow is null)
        {
            return;
        }

        _appWindow.Resize(new SizeInt32(500, 860));
        _appWindow.Title = "Codex Spend Popout";

        if (_appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = true;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = true;
        }
    }

    private AppWindow? GetAppWindow()
    {
        IntPtr hwnd = WindowNative.GetWindowHandle(this);
        WindowId windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        return AppWindow.GetFromWindowId(windowId);
    }

    private void ApplySnapshot(DashboardSnapshot snapshot)
    {
        _headerCostText.Text = FormatUsd(snapshot.TotalCostUsd);
        int unmatchedCount = snapshot.ConversationCount - snapshot.ResolvedPriceCount;
        _headerSummaryText.Text = $"{snapshot.ConversationCount} chats tracked, {snapshot.ResolvedPriceCount} matched, {unmatchedCount} unmatched.";
        _syncStatusText.Text = snapshot.LastPriceSyncAt is null
            ? "OpenRouter prices not synced yet."
            : $"OpenRouter sync: {snapshot.LastPriceSyncAt:dd MMM yyyy HH:mm:ss}";
        _footerStatusText.Text = snapshot.StatusText;

        _conversationStack.Children.Clear();
        if (snapshot.Conversations.Count == 0)
        {
            _conversationStack.Children.Add(BuildEmptyStateCard(
                "No conversations yet",
                "Start a Codex session and this panel will populate automatically."));
            return;
        }

        foreach (ConversationSpendInfo conversation in snapshot.Conversations)
        {
            _conversationStack.Children.Add(BuildConversationCard(conversation));
        }
    }

    private Grid BuildLayout(
        out TextBlock headerCostText,
        out TextBlock headerSummaryText,
        out TextBlock syncStatusText,
        out TextBlock footerStatusText,
        out ToggleSwitch alwaysOnTopSwitch,
        out ScrollViewer conversationScrollViewer,
        out StackPanel conversationStack)
    {
        var root = new Grid
        {
            Background = new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(1, 1),
                GradientStops =
                {
                    new GradientStop { Color = Color.FromArgb(255, 247, 241, 231), Offset = 0 },
                    new GradientStop { Color = Color.FromArgb(255, 242, 233, 221), Offset = 1 },
                },
            },
        };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var headerCard = new Border
        {
            Margin = new Thickness(18, 18, 18, 12),
            Padding = new Thickness(18),
            CornerRadius = new CornerRadius(24),
            BorderBrush = Brush("ShellBorderBrush"),
            BorderThickness = new Thickness(1),
            Background = new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(1, 1),
                GradientStops =
                {
                    new GradientStop { Color = Color.FromArgb(255, 255, 244, 231), Offset = 0 },
                    new GradientStop { Color = Color.FromArgb(255, 242, 223, 208), Offset = 1 },
                },
            },
        };

        var header = new Grid
        {
            RowSpacing = 12,
        };
        header.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        header.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        header.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        header.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var headerTitleStack = new StackPanel { Spacing = 4 };
        headerTitleStack.Children.Add(new TextBlock
        {
            Text = "Codex Spend Popout",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("ShellMutedTextBrush"),
        });

        headerCostText = new TextBlock
        {
            Text = "$0.0000",
            FontSize = 34,
            FontWeight = FontWeights.Bold,
            Foreground = Brush("ShellTextBrush"),
        };
        headerTitleStack.Children.Add(headerCostText);
        header.Children.Add(headerTitleStack);

        headerSummaryText = new TextBlock
        {
            Text = "Scanning conversations...",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brush("ShellMutedTextBrush"),
        };
        Grid.SetRow(headerSummaryText, 1);
        header.Children.Add(headerSummaryText);

        var actionsGrid = new Grid
        {
            ColumnSpacing = 10,
        };
        actionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        actionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        actionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(actionsGrid, 2);

        var refreshChatsButton = CreateActionButton("Refresh chats", RefreshChats_Click);
        Grid.SetColumn(refreshChatsButton, 0);
        actionsGrid.Children.Add(refreshChatsButton);

        var refreshPricesButton = CreateActionButton("Refresh prices", RefreshPrices_Click);
        Grid.SetColumn(refreshPricesButton, 1);
        actionsGrid.Children.Add(refreshPricesButton);

        var openPricingButton = CreateActionButton("OpenRouter prices", OpenPricingSource_Click);
        Grid.SetColumn(openPricingButton, 2);
        actionsGrid.Children.Add(openPricingButton);
        header.Children.Add(actionsGrid);

        alwaysOnTopSwitch = new ToggleSwitch
        {
            Header = "Always on top",
            IsOn = true,
            Foreground = Brush("ShellTextBrush"),
        };
        alwaysOnTopSwitch.Toggled += AlwaysOnTopSwitch_Toggled;

        var statusGrid = new Grid
        {
            ColumnSpacing = 12,
            VerticalAlignment = VerticalAlignment.Center,
        };
        statusGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        statusGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(statusGrid, 3);
        statusGrid.Children.Add(alwaysOnTopSwitch);

        syncStatusText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brush("ShellMutedTextBrush"),
            Margin = new Thickness(0, 22, 0, 0),
        };
        Grid.SetColumn(syncStatusText, 1);
        statusGrid.Children.Add(syncStatusText);
        header.Children.Add(statusGrid);

        headerCard.Child = header;
        root.Children.Add(headerCard);

        conversationStack = new StackPanel();
        conversationStack.Children.Add(BuildEmptyStateCard(
            "Waiting for data",
            "The app is starting and preparing the first pricing/session scan."));

        conversationScrollViewer = new ScrollViewer
        {
            Content = conversationStack,
            HorizontalScrollMode = ScrollMode.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollMode = ScrollMode.Enabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            ZoomMode = ZoomMode.Disabled,
        };
        conversationScrollViewer.PointerWheelChanged += ConversationScrollViewer_PointerWheelChanged;

        var conversationShell = new Border
        {
            Margin = new Thickness(18, 0, 18, 12),
            Padding = new Thickness(10),
            Background = Brush("ShellPanelBrush"),
            BorderBrush = Brush("ShellBorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(24),
            Child = conversationScrollViewer,
        };
        Grid.SetRow(conversationShell, 1);
        root.Children.Add(conversationShell);

        var footer = new Border
        {
            Margin = new Thickness(18, 0, 18, 18),
            Padding = new Thickness(14, 10, 14, 10),
            Background = new SolidColorBrush(Color.FromArgb(128, 255, 255, 255)),
            CornerRadius = new CornerRadius(16),
            BorderBrush = Brush("ShellBorderBrush"),
            BorderThickness = new Thickness(1),
        };

        footerStatusText = new TextBlock
        {
            Text = "Starting up...",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brush("ShellMutedTextBrush"),
        };
        footer.Child = footerStatusText;
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);

        return root;
    }

    private void ConversationScrollViewer_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer)
        {
            return;
        }

        int delta = e.GetCurrentPoint(scrollViewer).Properties.MouseWheelDelta;
        if (delta == 0 || scrollViewer.ScrollableHeight <= 0)
        {
            return;
        }

        double targetOffset = Math.Clamp(
            scrollViewer.VerticalOffset - (delta / 120d * AnimatedWheelScrollStep),
            0,
            scrollViewer.ScrollableHeight);

        scrollViewer.ChangeView(null, targetOffset, null, disableAnimation: false);
        e.Handled = true;
    }

    private UIElement BuildConversationCard(ConversationSpendInfo conversation)
    {
        var titleText = new TextBlock
        {
            Text = conversation.Preview,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            MaxLines = 2,
            Foreground = Brush("ShellTextBrush"),
        };

        var costBadge = new Border
        {
            Background = Brush("ShellAccentSoftBrush"),
            CornerRadius = new CornerRadius(999),
            Padding = new Thickness(10, 4, 10, 4),
            Child = new TextBlock
            {
                Text = FormatUsd(conversation.TotalCostUsd),
                Foreground = Brush("ShellAccentBrush"),
                FontWeight = FontWeights.SemiBold,
            },
        };

        var titleGrid = new Grid { ColumnSpacing = 8 };
        titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        titleGrid.Children.Add(titleText);
        Grid.SetColumn(costBadge, 1);
        titleGrid.Children.Add(costBadge);

        var modelText = new TextBlock
        {
            Text = $"{conversation.ModelProvider} / {conversation.ModelName}",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brush("ShellMutedTextBrush"),
        };

        var tokenText = new TextBlock
        {
            Text = $"Input {conversation.InputTokens:N0} | Cached {conversation.CachedInputTokens:N0} | Output {conversation.OutputTokens:N0} | Reasoning {conversation.ReasoningTokens:N0}",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brush("ShellMutedTextBrush"),
        };

        var pricingText = new TextBlock
        {
            Text = BuildPricingLabel(conversation),
            TextWrapping = TextWrapping.Wrap,
            Foreground = conversation.HasResolvedPricing
                ? Brush("ShellAccentBrush")
                : Brush("ShellMutedTextBrush"),
            FontWeight = FontWeights.SemiBold,
        };

        var statusText = new TextBlock
        {
            Text = conversation.HasResolvedPricing
                ? $"Matched via {conversation.PricingNote}"
                : conversation.PricingNote,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brush("ShellMutedTextBrush"),
        };

        var pathText = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(conversation.WorkingDirectory) ? conversation.SessionPath : conversation.WorkingDirectory,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brush("ShellMutedTextBrush"),
            FontSize = 11,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

        var updatedText = new TextBlock
        {
            Text = conversation.UpdatedAt.ToString("HH:mm:ss"),
            Foreground = Brush("ShellMutedTextBrush"),
            VerticalAlignment = VerticalAlignment.Center,
        };

        var footerGrid = new Grid { ColumnSpacing = 12 };
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var footerStack = new StackPanel { Spacing = 2 };
        footerStack.Children.Add(statusText);
        footerStack.Children.Add(pathText);
        footerGrid.Children.Add(footerStack);
        Grid.SetColumn(updatedText, 1);
        footerGrid.Children.Add(updatedText);

        var cardGrid = new Grid { RowSpacing = 8 };
        cardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        cardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        cardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        cardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        cardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        cardGrid.Children.Add(titleGrid);
        Grid.SetRow(modelText, 1);
        cardGrid.Children.Add(modelText);
        Grid.SetRow(tokenText, 2);
        cardGrid.Children.Add(tokenText);
        Grid.SetRow(pricingText, 3);
        cardGrid.Children.Add(pricingText);
        Grid.SetRow(footerGrid, 4);
        cardGrid.Children.Add(footerGrid);

        return new Border
        {
            Margin = new Thickness(0, 0, 0, 10),
            Padding = new Thickness(14),
            BorderThickness = new Thickness(1),
            BorderBrush = Brush("ShellBorderBrush"),
            CornerRadius = new CornerRadius(18),
            Background = new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(1, 1),
                GradientStops =
                {
                    new GradientStop { Color = Color.FromArgb(255, 255, 255, 255), Offset = 0 },
                    new GradientStop { Color = Color.FromArgb(255, 249, 239, 231), Offset = 1 },
                },
            },
            Child = cardGrid,
            Transitions = new TransitionCollection
            {
                new EntranceThemeTransition
                {
                    FromVerticalOffset = 18,
                },
            },
        };
    }

    private UIElement BuildEmptyStateCard(string title, string message)
    {
        var stack = new StackPanel { Spacing = 6 };
        stack.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("ShellTextBrush"),
        });
        stack.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brush("ShellMutedTextBrush"),
        });

        return new Border
        {
            Margin = new Thickness(0, 0, 0, 10),
            Padding = new Thickness(18),
            BorderThickness = new Thickness(1),
            BorderBrush = Brush("ShellBorderBrush"),
            CornerRadius = new CornerRadius(18),
            Background = Brush("ShellPanelBrush"),
            Child = stack,
        };
    }

    private Button CreateActionButton(string label, RoutedEventHandler clickHandler)
    {
        var button = new Button
        {
            Content = label,
            Style = Application.Current.Resources["ShellButtonStyle"] as Style,
        };
        button.Click += clickHandler;
        return button;
    }

    private static string BuildPricingLabel(ConversationSpendInfo conversation)
    {
        if (!conversation.HasResolvedPricing)
        {
            return "Pricing: no OpenRouter match";
        }

        return $"Pricing: {conversation.ResolvedModelId} | input {FormatUsdPerMillion(conversation.PromptPriceUsd)} / 1M | output {FormatUsdPerMillion(conversation.CompletionPriceUsd)} / 1M";
    }

    private SolidColorBrush Brush(string resourceKey)
    {
        return resourceKey switch
        {
            "ShellSurfaceBrush" => new SolidColorBrush(Color.FromArgb(255, 247, 241, 231)),
            "ShellPanelBrush" => new SolidColorBrush(Color.FromArgb(255, 253, 248, 241)),
            "ShellAccentBrush" => new SolidColorBrush(Color.FromArgb(255, 184, 92, 56)),
            "ShellAccentSoftBrush" => new SolidColorBrush(Color.FromArgb(255, 242, 214, 199)),
            "ShellTextBrush" => new SolidColorBrush(Color.FromArgb(255, 44, 34, 28)),
            "ShellMutedTextBrush" => new SolidColorBrush(Color.FromArgb(255, 110, 90, 74)),
            "ShellBorderBrush" => new SolidColorBrush(Color.FromArgb(40, 110, 74, 44)),
            _ => new SolidColorBrush(Color.FromArgb(255, 240, 240, 240)),
        };
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
}
