using Echo_Windows.Core;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using Windows.Graphics;
using Windows.UI;

namespace Echo_Windows;

/// <summary>
/// WinUI controls hosted in their own small HWNDs. The subtitle HWND can therefore
/// remain WS_EX_TRANSPARENT while the toolbar and settings panel stay interactive.
/// </summary>
internal sealed class OverlayInteractionWindows : IDisposable
{
    private const int SwHide = 0;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpNoZOrder = 0x0004;
    private static readonly nint HwndTopMost = new(-1);

    private readonly Action<bool> setAdjusting;
    private readonly Action<DesktopSubtitleOverlaySettings> saveSettings;
    private readonly Action closeOverlay;
    private readonly DispatcherQueueTimer persistTimer;
    private Window? toolbar;
    private Window? settingsWindow;
    private Button? moveButton;
    private Button? lockButton;
    private Button? clickThroughButton;
    private FontIcon? lockIcon;
    private FontIcon? clickThroughIcon;
    private ToggleSwitch? settingsLockToggle;
    private ToggleSwitch? settingsClickThroughToggle;
    private ScrollViewer? settingsScrollViewer;
    private Button? moreSettingsToggle;
    private StackPanel? advancedSettingsPanel;
    private bool moreSettingsExpanded;
    private readonly Dictionary<string, Control> settingsControls = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TextBlock> sliderValueLabels = new(StringComparer.Ordinal);
    private DesktopSubtitleOverlaySettings? currentSettings;
    private nint toolbarHwnd;
    private nint settingsHwnd;
    private bool settingsVisible;
    private bool disposed;
    private bool synchronizingSettingsUi;
    private uint toolbarDpi;
    private int toolbarWidth;
    private int toolbarHeight;
    private bool settingsPlacementValid;
    private uint lastSettingsDpi;
    private NativeRect lastSettingsAnchor;
    private NativeRect lastSettingsWorkArea;

    public OverlayInteractionWindows(
        DispatcherQueue dispatcherQueue,
        Action<bool> setAdjusting,
        Action<DesktopSubtitleOverlaySettings> saveSettings,
        Action closeOverlay)
    {
        this.setAdjusting = setAdjusting;
        this.saveSettings = saveSettings;
        this.closeOverlay = closeOverlay;
        persistTimer = dispatcherQueue.CreateTimer();
        persistTimer.Interval = TimeSpan.FromMilliseconds(350);
        persistTimer.IsRepeating = false;
        persistTimer.Tick += (_, _) =>
        {
            if (currentSettings is not null) saveSettings(currentSettings);
        };
    }

    public bool IsSettingsVisible => settingsVisible;

    public bool ContainsPoint(int x, int y) =>
        IsPointInside(toolbarHwnd, x, y) || (settingsVisible && IsPointInside(settingsHwnd, x, y));

    public void ShowToolbar(RectInt32 anchor, DesktopSubtitleOverlaySettings settings, bool adjusting)
    {
        if (disposed) return;
        currentSettings = settings;
        EnsureToolbar();
        UpdateToolbarLabels(settings, adjusting);
        nint anchorHwnd = GetAnchorWindow?.Invoke() ?? toolbarHwnd;
        uint dpi = Math.Max(96, GetDpiForWindow(anchorHwnd));
        int width = (int)Math.Round(184 * dpi / 96d);
        int height = (int)Math.Round(40 * dpi / 96d);
        var monitor = MonitorFromWindow(anchorHwnd, 2);
        var work = GetWorkArea(monitor, anchor);
        int x = Math.Clamp(anchor.X + (anchor.Width - width) / 2, work.Left, Math.Max(work.Left, work.Right - width));
        int y = anchor.Y - height;
        if (y < work.Top) y = Math.Min(anchor.Y + anchor.Height, work.Bottom - height);
        if (toolbarDpi != dpi || toolbarWidth != width || toolbarHeight != height)
        {
            toolbar!.AppWindow.Resize(new SizeInt32(width, height));
            toolbarDpi = dpi;
            toolbarWidth = width;
            toolbarHeight = height;
        }
        if (!GetWindowRect(toolbarHwnd, out NativeRect current)
            || current.Left != x || current.Top != y
            || current.Right - current.Left != width || current.Bottom - current.Top != height)
            SetWindowPos(toolbarHwnd, HwndTopMost, x, y, width, height, SwpNoActivate);
        if (!IsWindowVisible(toolbarHwnd)) toolbar!.AppWindow.Show(false);
    }

    public void HideToolbar()
    {
        if (toolbarHwnd != 0 && !settingsVisible) ShowWindow(toolbarHwnd, SwHide);
    }

    public void HideAll()
    {
        FlushPendingSettings();
        settingsVisible = false;
        if (settingsHwnd != 0) ShowWindow(settingsHwnd, SwHide);
        if (toolbarHwnd != 0) ShowWindow(toolbarHwnd, SwHide);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        FlushPendingSettings();
        settingsVisible = false;
        toolbar?.Close();
        settingsWindow?.Close();
        toolbar = settingsWindow = null;
        toolbarHwnd = settingsHwnd = 0;
    }

    private void EnsureToolbar()
    {
        if (toolbar is not null) return;
        toolbar = CreateBorderlessWindow("Echo Subtitle Controls", new SizeInt32(184, 40));
        toolbarHwnd = WinRT.Interop.WindowNative.GetWindowHandle(toolbar);

        var surface = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(238, 37, 39, 44)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(13),
            Padding = new Thickness(4)
        };
        var row = new Grid { ColumnSpacing = 2 };
        for (int i = 0; i < 5; i++) row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        moveButton = MakeIconButton("\uE76F", "拖动字幕；先解锁位置", "OverlayMoveHandle");
        lockButton = MakeIconButton("\uE72E", "锁定或解锁字幕位置", "OverlayPositionLock");
        lockIcon = (FontIcon)lockButton.Content;
        clickThroughButton = MakeIconButton("\uE962", "切换点击穿透；此工具栏仍可操作", "OverlayClickThrough");
        clickThroughIcon = (FontIcon)clickThroughButton.Content;
        Button settingsButton = MakeIconButton("\uE713", "打开悬浮字幕设置", "OverlayOpenSettings");
        Button closeButton = MakeIconButton("\uE8BB", "关闭悬浮字幕", "OverlayClose");
        Button[] buttons = [moveButton, lockButton, clickThroughButton, settingsButton, closeButton];
        for (int i = 0; i < buttons.Length; i++)
        {
            Grid.SetColumn(buttons[i], i);
            row.Children.Add(buttons[i]);
        }
        moveButton.PointerPressed += (_, args) =>
        {
            if (currentSettings?.PositionLocked == true) return;
            args.Handled = true;
            BeginMoveFromToolbar?.Invoke();
        };
        moveButton.KeyDown += (_, args) =>
        {
            if (currentSettings?.PositionLocked != false) return;
            int step = (int)Math.Round(12 * Math.Max(1, GetDpiForWindow(toolbarHwnd)) / 96d);
            (int dx, int dy) = args.Key switch
            {
                Windows.System.VirtualKey.Left => (-step, 0),
                Windows.System.VirtualKey.Right => (step, 0),
                Windows.System.VirtualKey.Up => (0, -step),
                Windows.System.VirtualKey.Down => (0, step),
                _ => (0, 0)
            };
            if (dx != 0 || dy != 0)
            {
                args.Handled = true;
                MoveByKeyboard?.Invoke(dx, dy);
            }
        };
        lockButton.Click += (_, _) =>
        {
            if (currentSettings is null) return;
            currentSettings.PositionLocked = !currentSettings.PositionLocked;
            setAdjusting(!currentSettings.PositionLocked);
            saveSettings(currentSettings);
            UpdateToolbarLabels(currentSettings, !currentSettings.PositionLocked);
        };
        clickThroughButton.Click += (_, _) =>
        {
            if (currentSettings is null) return;
            currentSettings.ClickThrough = !currentSettings.ClickThrough;
            saveSettings(currentSettings);
            UpdateToolbarLabels(currentSettings, !currentSettings.PositionLocked);
        };
        settingsButton.Click += (_, _) => OpenSettings();
        closeButton.Click += (_, _) => closeOverlay();
        surface.Child = row;
        toolbar.Content = surface;
    }

    // Installed by NativeDesktopSubtitleOverlayWindow so the drag handle can capture
    // the pointer on the caption HWND even when the caption surface is click-through.
    public Action? BeginMoveFromToolbar { get; set; }

    private void OpenSettings()
    {
        if (currentSettings is null) return;
        EnsureSettingsWindow();
        settingsVisible = true;
        BuildSettings(currentSettings);
        SynchronizeSettingsControls(currentSettings);
        SetMoreSettingsExpanded(false);
        settingsScrollViewer?.ChangeView(0, 0, null);
        PlaceSettingsWindow();
        settingsWindow!.AppWindow.Show(true);
    }

    private void EnsureSettingsWindow()
    {
        if (settingsWindow is not null) return;
        settingsWindow = CreateBorderlessWindow("Echo Subtitle Settings", new SizeInt32(304, 416));
        settingsHwnd = WinRT.Interop.WindowNative.GetWindowHandle(settingsWindow);
        settingsWindow.AppWindow.Closing += (_, args) =>
        {
            if (disposed) return;
            args.Cancel = true;
            FlushPendingSettings();
            settingsVisible = false;
            settingsPlacementValid = false;
            ShowWindow(settingsHwnd, SwHide);
        };
    }

    private void BuildSettings(DesktopSubtitleOverlaySettings settings)
    {
        if (settingsWindow?.Content is not null) return;
        var root = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(246, 34, 36, 41)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(70, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(12)
        };
        var layout = new Grid { RowSpacing = 8 };
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new Grid { ColumnSpacing = 8, VerticalAlignment = VerticalAlignment.Center };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(new TextBlock { Text = "悬浮字幕", FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        var closeSettings = MakeIconButton("\uE8BB", "关闭悬浮字幕设置", "OverlaySettingsClose");
        closeSettings.Click += (_, _) => CloseSettings();
        Grid.SetColumn(closeSettings, 1);
        header.Children.Add(closeSettings);
        layout.Children.Add(header);

        var content = new StackPanel { Spacing = 6 };
        var visibilityRow = new Grid { ColumnSpacing = 8 };
        visibilityRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        visibilityRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        AddToggle(visibilityRow, "显示原文", "OverlayShowOriginal", settings.ShowOriginal, value => { if (currentSettings is { } s) s.ShowOriginal = value; }, 0);
        AddToggle(visibilityRow, "显示译文", "OverlayShowTranslation", settings.ShowTranslation, value => { if (currentSettings is { } s) s.ShowTranslation = value; }, 1);
        content.Children.Add(visibilityRow);
        AddSlider(content, "原文字号", "OverlayOriginalFontSize", settings.OriginalFontSize, 16, 48, " pt", value => { if (currentSettings is { } s) s.OriginalFontSize = value; });
        AddSlider(content, "译文字号", "OverlayTranslationFontSize", settings.TranslationFontSize, 14, 44, " pt", value => { if (currentSettings is { } s) s.TranslationFontSize = value; });
        AddSlider(content, "字幕透明度", "OverlayOpacity", settings.Opacity * 100, 35, 100, "%", value => { if (currentSettings is { } s) s.Opacity = value / 100; });
        AddSlider(content, "字幕最大宽度", "OverlayMaximumWidth", settings.WidthFraction * 100, 35, 95, "%", value => { if (currentSettings is { } s) s.WidthFraction = value / 100; });

        var advanced = new StackPanel { Spacing = 6, Visibility = Visibility.Collapsed };
        advancedSettingsPanel = advanced;
        AddSlider(advanced, "定稿保留时间", "OverlayRetention", settings.RetentionSeconds, 1, 15, " 秒", value => { if (currentSettings is { } s) s.RetentionSeconds = value; });
        AddSlider(advanced, "文字阴影", "OverlayShadow", settings.ShadowStrength * 100, 0, 100, "%", value => { if (currentSettings is { } s) s.ShadowStrength = value / 100; });
        AddToggle(advanced, "锁定位置", "OverlayPositionLocked", settings.PositionLocked, value =>
        {
            if (currentSettings is not { } current) return;
            current.PositionLocked = value;
            setAdjusting(!value);
            UpdateToolbarLabels(current, !value);
        });
        AddToggle(advanced, "点击穿透", "OverlayClickThrough", settings.ClickThrough, value => { if (currentSettings is { } s) s.ClickThrough = value; });
        var resetPosition = MakeToolButton("重置位置", "重置字幕位置", "OverlayResetPosition");
        resetPosition.Click += (_, _) =>
        {
            if (currentSettings is not { } s) return;
            s.DisplayId = null;
            s.DisplayDeviceName = null;
            s.NormalizedX = .5;
            s.NormalizedBottom = .09;
            saveSettings(s);
        };
        var defaults = MakeToolButton("恢复默认", "恢复悬浮字幕默认设置", "OverlayRestoreDefaults");
        defaults.Click += (_, _) =>
        {
            if (currentSettings is not { } s) return;
            s.ShowOriginal = true;
            s.ShowTranslation = true;
            s.OriginalFontSize = 26;
            s.TranslationFontSize = 24;
            s.Opacity = 1;
            s.WidthFraction = .75;
            s.RetentionSeconds = 5;
            s.ShadowStrength = .35;
            s.ClickThrough = true;
            s.PositionLocked = true;
            setAdjusting(false);
            SynchronizeSettingsControls(s);
            UpdateToolbarLabels(s, false);
            SaveLiveChange();
        };
        var advancedActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        advancedActions.Children.Add(resetPosition);
        advancedActions.Children.Add(defaults);
        advanced.Children.Add(advancedActions);
        content.Children.Add(advanced);
        moreSettingsToggle = MakeToolButton("更多设置", "显示更多悬浮字幕设置", "OverlayMoreSettings");
        AutomationProperties.SetName(moreSettingsToggle, "显示更多悬浮字幕设置");
        moreSettingsToggle.Click += (_, _) =>
        {
            SetMoreSettingsExpanded(!moreSettingsExpanded);
        };
        var scroller = settingsScrollViewer = new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        AutomationProperties.SetAutomationId(scroller, "OverlaySettingsScrollViewer");
        Grid.SetRow(scroller, 1);
        layout.Children.Add(scroller);
        var footer = new Grid { ColumnSpacing = 8 };
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footer.Children.Add(moreSettingsToggle);
        var done = MakeToolButton("完成", "关闭悬浮字幕设置", "OverlaySettingsDone");
        done.Click += (_, _) => CloseSettings();
        Grid.SetColumn(done, 1);
        footer.Children.Add(done);
        Grid.SetRow(footer, 2);
        layout.Children.Add(footer);
        root.Child = layout;
        root.KeyDown += (_, args) =>
        {
            if (args.Key == Windows.System.VirtualKey.Escape)
            {
                args.Handled = true;
                CloseSettings();
            }
        };
        settingsWindow!.Content = root;
        UpdateSettingsPositionFromAnchor();
    }

    private void AddToggle(Panel content, string label, string automationId, bool initial, Action<bool> update, int? column = null)
    {
        var control = new ToggleSwitch { Header = label, IsOn = initial, HorizontalAlignment = HorizontalAlignment.Stretch, MinHeight = 32, OnContent = "开", OffContent = "关" };
        AutomationProperties.SetName(control, label);
        AutomationProperties.SetAutomationId(control, automationId);
        settingsControls[automationId] = control;
        if (column is { } col) Grid.SetColumn(control, col);
        if (automationId == "OverlayPositionLocked") settingsLockToggle = control;
        if (automationId == "OverlayClickThrough") settingsClickThroughToggle = control;
        control.Toggled += (_, _) =>
        {
            if (synchronizingSettingsUi) return;
            update(control.IsOn);
            SaveLiveChange();
        };
        content.Children.Add(control);
    }

    private void AddSlider(Panel content, string label, string automationId, double initial,
        double min, double max, string suffix, Action<double> update)
    {
        var row = new Grid { RowSpacing = 1 };
        row.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        row.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var valueText = new TextBlock { Text = $"{label} · {initial:0}{suffix}", FontSize = 12, Opacity = .86 };
        var slider = new Slider { Minimum = min, Maximum = max, Value = initial, StepFrequency = 1, MinHeight = 28, Margin = new Thickness(0) };
        AutomationProperties.SetName(slider, label);
        AutomationProperties.SetAutomationId(slider, automationId);
        settingsControls[automationId] = slider;
        sliderValueLabels[automationId] = valueText;
        slider.ValueChanged += (_, args) =>
        {
            if (synchronizingSettingsUi) return;
            valueText.Text = $"{label} · {args.NewValue:0}{suffix}";
            update(args.NewValue);
            SaveLiveChange();
        };
        row.Children.Add(valueText);
        Grid.SetRow(slider, 1);
        row.Children.Add(slider);
        content.Children.Add(row);
    }

    private void SynchronizeSettingsControls(DesktopSubtitleOverlaySettings settings)
    {
        synchronizingSettingsUi = true;
        SetSlider("OverlayOriginalFontSize", settings.OriginalFontSize);
        SetSlider("OverlayTranslationFontSize", settings.TranslationFontSize);
        SetSlider("OverlayOpacity", settings.Opacity * 100);
        SetSlider("OverlayMaximumWidth", settings.WidthFraction * 100);
        SetSlider("OverlayRetention", settings.RetentionSeconds);
        SetSlider("OverlayShadow", settings.ShadowStrength * 100);
        if (settingsControls.GetValueOrDefault("OverlayShowOriginal") is ToggleSwitch original) original.IsOn = settings.ShowOriginal;
        if (settingsControls.GetValueOrDefault("OverlayShowTranslation") is ToggleSwitch translation) translation.IsOn = settings.ShowTranslation;
        if (settingsLockToggle is not null) settingsLockToggle.IsOn = settings.PositionLocked;
        if (settingsClickThroughToggle is not null) settingsClickThroughToggle.IsOn = settings.ClickThrough;
        synchronizingSettingsUi = false;
    }

    private void SetSlider(string id, double value)
    {
        if (settingsControls.GetValueOrDefault(id) is Slider slider) slider.Value = value;
        if (sliderValueLabels.GetValueOrDefault(id) is TextBlock label)
        {
            string suffix = id switch { "OverlayOriginalFontSize" or "OverlayTranslationFontSize" => " pt", "OverlayRetention" => " 秒", "OverlayOpacity" or "OverlayMaximumWidth" or "OverlayShadow" => "%", _ => string.Empty };
            string title = id switch { "OverlayOriginalFontSize" => "原文字号", "OverlayTranslationFontSize" => "译文字号", "OverlayOpacity" => "字幕透明度", "OverlayMaximumWidth" => "字幕最大宽度", "OverlayRetention" => "定稿保留时间", _ => "文字阴影" };
            label.Text = $"{title} · {value:0}{suffix}";
        }
    }

    private void CloseSettings()
    {
        FlushPendingSettings();
        settingsVisible = false;
        settingsPlacementValid = false;
        ShowWindow(settingsHwnd, SwHide);
    }

    private void SetMoreSettingsExpanded(bool expanded)
    {
        moreSettingsExpanded = expanded;
        if (advancedSettingsPanel is not null)
            advancedSettingsPanel.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
        if (moreSettingsToggle is not null)
        {
            moreSettingsToggle.Content = expanded ? "收起设置" : "更多设置";
            AutomationProperties.SetName(moreSettingsToggle, expanded ? "收起更多悬浮字幕设置" : "显示更多悬浮字幕设置");
        }
    }

    private void SaveLiveChange()
    {
        if (currentSettings is null) return;
        UpdateToolbarLabels(currentSettings, !currentSettings.PositionLocked);
        saveSettingsPreview?.Invoke(currentSettings);
        persistTimer.Stop();
        persistTimer.Start();
    }

    private void FlushPendingSettings()
    {
        if (!persistTimer.IsRunning) return;
        persistTimer.Stop();
        if (currentSettings is not null) saveSettings(currentSettings);
    }

    public Action<DesktopSubtitleOverlaySettings>? saveSettingsPreview { get; set; }

    private void PlaceSettingsWindow()
    {
        if (settingsHwnd == 0) return;
        if (GetAnchorWindow is null || !GetWindowRect(GetAnchorWindow(), out NativeRect anchor)) return;
        UpdateSettingsPosition(new RectInt32(anchor.Left, anchor.Top,
            anchor.Right - anchor.Left, anchor.Bottom - anchor.Top));
    }

    private void UpdateSettingsPositionFromAnchor()
    {
        if (GetAnchorWindow is null || !GetWindowRect(GetAnchorWindow(), out NativeRect anchor)) return;
        UpdateSettingsPosition(new RectInt32(anchor.Left, anchor.Top,
            anchor.Right - anchor.Left, anchor.Bottom - anchor.Top));
    }

    public void UpdateSettingsPosition(RectInt32 anchorRect)
    {
        if (settingsHwnd == 0 || !settingsVisible) return;
        nint anchorHwnd = toolbarHwnd != 0 && IsWindowVisible(toolbarHwnd)
            ? toolbarHwnd
            : GetAnchorWindow?.Invoke() ?? settingsHwnd;
        var overlayMonitor = MonitorFromWindow(anchorHwnd, 2);
        if (toolbarHwnd != 0 && IsWindowVisible(toolbarHwnd) && GetWindowRect(toolbarHwnd, out NativeRect toolbarBounds))
            anchorRect = new RectInt32(toolbarBounds.Left, toolbarBounds.Top, toolbarBounds.Right - toolbarBounds.Left, toolbarBounds.Bottom - toolbarBounds.Top);
        var work = GetWorkArea(overlayMonitor, anchorRect);
        uint dpi = Math.Max(96, GetDpiForWindow(anchorHwnd));
        if (settingsPlacementValid && dpi == lastSettingsDpi
            && SameRect(lastSettingsAnchor, new NativeRect { Left = anchorRect.X, Top = anchorRect.Y, Right = anchorRect.X + anchorRect.Width, Bottom = anchorRect.Y + anchorRect.Height })
            && SameRect(lastSettingsWorkArea, work)) return;
        SizeInt32 desired = new((int)Math.Round(304 * dpi / 96d), (int)Math.Round(416 * dpi / 96d));
        OverlayPixelRect placement = OverlayPopoverLayout.Place(
            new OverlayPixelRect(anchorRect.X, anchorRect.Y, anchorRect.Width, anchorRect.Height),
            new OverlayPixelRect(work.Left, work.Top, work.Right - work.Left, work.Bottom - work.Top),
            desired.Width, desired.Height, (int)Math.Round(8 * dpi / 96d));
        var appWindow = settingsWindow!.AppWindow;
        if (!GetWindowRect(settingsHwnd, out NativeRect currentSize)
            || currentSize.Right - currentSize.Left != placement.Width || currentSize.Bottom - currentSize.Top != placement.Height)
            appWindow.Resize(new SizeInt32(placement.Width, placement.Height));
        if (!GetWindowRect(settingsHwnd, out NativeRect current)
            || current.Left != placement.X || current.Top != placement.Y)
            SetWindowPos(settingsHwnd, HwndTopMost, placement.X, placement.Y, placement.Width, placement.Height, SwpNoActivate);
        lastSettingsAnchor = new NativeRect { Left = anchorRect.X, Top = anchorRect.Y, Right = anchorRect.X + anchorRect.Width, Bottom = anchorRect.Y + anchorRect.Height };
        lastSettingsWorkArea = work;
        lastSettingsDpi = dpi;
        settingsPlacementValid = true;
    }

    public Func<nint>? GetAnchorWindow { get; set; }
    public Action<int, int>? MoveByKeyboard { get; set; }

    private void UpdateToolbarLabels(DesktopSubtitleOverlaySettings settings, bool adjusting)
    {
        if (moveButton is null) return;
        moveButton.IsEnabled = !settings.PositionLocked;
        AutomationProperties.SetName(moveButton, settings.PositionLocked ? "移动字幕；请先解锁位置" : "拖动字幕到新位置");
        lockIcon!.Glyph = settings.PositionLocked ? "\uE72E" : "\uE785";
        AutomationProperties.SetName(lockButton, settings.PositionLocked ? "解锁位置" : "锁定位置");
        ToolTipService.SetToolTip(lockButton, settings.PositionLocked ? "解锁字幕位置" : "锁定字幕位置");
        clickThroughIcon!.Glyph = settings.ClickThrough ? "\uE962" : "\uE8B0";
        AutomationProperties.SetName(clickThroughButton, settings.ClickThrough ? "关闭点击穿透" : "开启点击穿透");
        ToolTipService.SetToolTip(clickThroughButton, settings.ClickThrough ? "关闭点击穿透；底层应用将不再接收字幕区域点击" : "开启点击穿透；工具栏仍可操作");
        synchronizingSettingsUi = true;
        if (settingsLockToggle is not null) settingsLockToggle.IsOn = settings.PositionLocked;
        if (settingsClickThroughToggle is not null) settingsClickThroughToggle.IsOn = settings.ClickThrough;
        synchronizingSettingsUi = false;
    }

    private static Window CreateBorderlessWindow(string title, SizeInt32 initialSize)
    {
        var window = new Window { Title = title, SystemBackdrop = new DesktopAcrylicBackdrop() };
        nint hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        var appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(hwnd));
        appWindow.Resize(initialSize);
        appWindow.IsShownInSwitchers = false;
        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsAlwaysOnTop = true;
        }
        return window;
    }

    private static Button MakeIconButton(string glyph, string accessibleName, string automationId)
    {
        var button = new Button
        {
            Content = new FontIcon { Glyph = glyph, FontSize = 14, FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe MDL2 Assets") },
            Width = 30,
            Height = 30,
            MinWidth = 30,
            MinHeight = 30,
            Padding = new Thickness(0),
            Background = new SolidColorBrush(Color.FromArgb(12, 255, 255, 255)),
            Foreground = new SolidColorBrush(Colors.White),
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(8)
        };
        AutomationProperties.SetName(button, accessibleName);
        AutomationProperties.SetAutomationId(button, automationId);
        ToolTipService.SetToolTip(button, accessibleName);
        return button;
    }

    private static Button MakeToolButton(string text, string accessibleName, string automationId)
    {
        var button = new Button
        {
            Content = text,
            MinWidth = 0,
            MinHeight = 34,
            Padding = new Thickness(8, 3, 8, 3),
            Background = new SolidColorBrush(Color.FromArgb(24, 255, 255, 255)),
            Foreground = new SolidColorBrush(Colors.White),
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(9),
            FontSize = 12
        };
        AutomationProperties.SetName(button, accessibleName);
        AutomationProperties.SetAutomationId(button, automationId);
        return button;
    }

    private static bool SameRect(NativeRect left, NativeRect right) =>
        left.Left == right.Left && left.Top == right.Top && left.Right == right.Right && left.Bottom == right.Bottom;

    private static bool IsPointInside(nint hwnd, int x, int y) => hwnd != 0
        && IsWindowVisible(hwnd)
        && GetWindowRect(hwnd, out NativeRect rect)
        && x >= rect.Left && x < rect.Right && y >= rect.Top && y < rect.Bottom;

    private static NativeRect GetWorkArea(nint monitor, RectInt32 fallback)
    {
        var info = new MonitorInfo { Size = (uint)Marshal.SizeOf<MonitorInfo>() };
        return monitor != 0 && GetMonitorInfo(monitor, ref info)
            ? info.WorkArea
            : new NativeRect { Left = fallback.X, Top = fallback.Y, Right = fallback.X + fallback.Width, Bottom = fallback.Y + fallback.Height };
    }

    private static SizeInt32 ScaleSize(nint hwnd, double widthDip, double heightDip)
    {
        double scale = Math.Max(1, GetDpiForWindow(hwnd) / 96d);
        return new SizeInt32((int)Math.Round(widthDip * scale), (int)Math.Round(heightDip * scale));
    }

    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public uint Size; public NativeRect MonitorArea; public NativeRect WorkArea; public uint Flags; }

    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint hwnd, out NativeRect rect);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")] private static extern bool ShowWindow(nint hwnd, int command);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowPos(nint hwnd, nint insertAfter, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] private static extern nint MonitorFromWindow(nint hwnd, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(nint hwnd);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out Windows.Graphics.PointInt32 point);
}
