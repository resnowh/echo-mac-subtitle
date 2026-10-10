using Echo_Windows.Core;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Runtime.InteropServices;
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
    private ToggleSwitch? settingsLockToggle;
    private ToggleSwitch? settingsClickThroughToggle;
    private DesktopSubtitleOverlaySettings? currentSettings;
    private nint toolbarHwnd;
    private nint settingsHwnd;
    private bool settingsVisible;
    private bool disposed;
    private bool synchronizingSettingsUi;
    private uint toolbarDpi;
    private int toolbarWidth;
    private int toolbarHeight;

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
        uint dpi = Math.Max(96, GetDpiForWindow(toolbarHwnd));
        int width = (int)Math.Round(218 * dpi / 96d);
        int height = (int)Math.Round(46 * dpi / 96d);
        var monitor = MonitorFromWindow(GetAnchorWindow?.Invoke() ?? toolbarHwnd, 2);
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
        toolbar = CreateBorderlessWindow("Echo Subtitle Controls", new SizeInt32(218, 46));
        toolbarHwnd = WinRT.Interop.WindowNative.GetWindowHandle(toolbar);

        var surface = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(238, 37, 39, 44)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(5)
        };
        var row = new Grid { ColumnSpacing = 3 };
        for (int i = 0; i < 5; i++) row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        moveButton = MakeToolButton("↕", "拖动字幕；先解锁位置", "OverlayMoveHandle");
        lockButton = MakeToolButton("锁", "锁定或解锁字幕位置", "OverlayPositionLock");
        clickThroughButton = MakeToolButton("透", "切换点击穿透；此工具栏仍可操作", "OverlayClickThrough");
        Button settingsButton = MakeToolButton("⚙", "打开悬浮字幕设置", "OverlayOpenSettings");
        Button closeButton = MakeToolButton("×", "关闭悬浮字幕", "OverlayClose");
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
        PlaceSettingsWindow();
        settingsWindow!.AppWindow.Show(true);
    }

    private void EnsureSettingsWindow()
    {
        if (settingsWindow is not null) return;
        settingsWindow = CreateBorderlessWindow("Echo Subtitle Settings", new SizeInt32(390, 620));
        settingsHwnd = WinRT.Interop.WindowNative.GetWindowHandle(settingsWindow);
        settingsWindow.AppWindow.Closing += (_, args) =>
        {
            if (disposed) return;
            args.Cancel = true;
            settingsVisible = false;
            ShowWindow(settingsHwnd, SwHide);
        };
    }

    private void BuildSettings(DesktopSubtitleOverlaySettings settings)
    {
        var root = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(246, 34, 36, 41)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(70, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(18)
        };
        var content = new StackPanel { Spacing = 10, MaxWidth = 370 };
        content.Children.Add(new TextBlock { Text = "悬浮字幕", FontSize = 18 });
        AddToggle(content, "显示原文", "OverlayShowOriginal", settings.ShowOriginal, value => settings.ShowOriginal = value);
        AddToggle(content, "显示译文", "OverlayShowTranslation", settings.ShowTranslation, value => settings.ShowTranslation = value);
        AddSlider(content, "原文字号", "OverlayOriginalFontSize", settings.OriginalFontSize, 16, 48, "pt", value => settings.OriginalFontSize = value);
        AddSlider(content, "译文字号", "OverlayTranslationFontSize", settings.TranslationFontSize, 14, 44, "pt", value => settings.TranslationFontSize = value);
        AddSlider(content, "字幕透明度", "OverlayOpacity", settings.Opacity * 100, 35, 100, "%", value => settings.Opacity = value / 100);
        AddSlider(content, "字幕最大宽度", "OverlayMaximumWidth", settings.WidthFraction * 100, 35, 95, "%", value => settings.WidthFraction = value / 100);
        AddSlider(content, "定稿保留时间", "OverlayRetention", settings.RetentionSeconds, 1, 15, "秒", value => settings.RetentionSeconds = value);
        AddSlider(content, "文字阴影", "OverlayShadow", settings.ShadowStrength * 100, 0, 100, "%", value => settings.ShadowStrength = value / 100);
        AddToggle(content, "锁定位置", "OverlayPositionLocked", settings.PositionLocked, value =>
        {
            settings.PositionLocked = value;
            setAdjusting(!value);
            UpdateToolbarLabels(settings, !value);
        });
        AddToggle(content, "点击穿透", "OverlayClickThrough", settings.ClickThrough, value => settings.ClickThrough = value);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        var resetPosition = MakeToolButton("重置字幕位置", "重置字幕位置", "OverlayResetPosition");
        resetPosition.Click += (_, _) =>
        {
            settings.DisplayId = null;
            settings.DisplayDeviceName = null;
            settings.NormalizedX = .5;
            settings.NormalizedBottom = .09;
            saveSettings(settings);
        };
        var defaults = MakeToolButton("恢复默认", "恢复悬浮字幕默认设置", "OverlayRestoreDefaults");
        defaults.Click += (_, _) =>
        {
            bool enabled = settings.Enabled;
            settings = currentSettings = new DesktopSubtitleOverlaySettings
            {
                Enabled = enabled,
                DisplayId = settings.DisplayId,
                DisplayDeviceName = settings.DisplayDeviceName,
                NormalizedX = settings.NormalizedX,
                NormalizedBottom = settings.NormalizedBottom
            };
            saveSettings(settings);
            BuildSettings(settings);
        };
        actions.Children.Add(resetPosition);
        actions.Children.Add(defaults);
        content.Children.Add(actions);
        var done = MakeToolButton("完成", "关闭悬浮字幕设置", "OverlaySettingsDone");
        done.HorizontalAlignment = HorizontalAlignment.Right;
        done.Click += (_, _) => { settingsVisible = false; ShowWindow(settingsHwnd, SwHide); };
        content.Children.Add(done);
        root.Child = new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        root.KeyDown += (_, args) =>
        {
            if (args.Key == Windows.System.VirtualKey.Escape)
            {
                args.Handled = true;
                settingsVisible = false;
                ShowWindow(settingsHwnd, SwHide);
            }
        };
        settingsWindow!.Content = root;
        UpdateSettingsPositionFromAnchor();
    }

    private void AddToggle(StackPanel content, string label, string automationId, bool initial, Action<bool> update)
    {
        var control = new ToggleSwitch { Header = label, IsOn = initial, HorizontalAlignment = HorizontalAlignment.Stretch };
        AutomationProperties.SetName(control, label);
        AutomationProperties.SetAutomationId(control, automationId);
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

    private void AddSlider(StackPanel content, string label, string automationId, double initial,
        double min, double max, string suffix, Action<double> update)
    {
        var row = new StackPanel { Spacing = 2 };
        var valueText = new TextBlock { Text = $"{label} · {initial:0}{suffix}", FontSize = 12, Opacity = .82 };
        var slider = new Slider { Minimum = min, Maximum = max, Value = initial, StepFrequency = 1 };
        AutomationProperties.SetName(slider, label);
        AutomationProperties.SetAutomationId(slider, automationId);
        slider.ValueChanged += (_, args) =>
        {
            valueText.Text = $"{label} · {args.NewValue:0}{suffix}";
            update(args.NewValue);
            SaveLiveChange();
        };
        row.Children.Add(valueText);
        row.Children.Add(slider);
        content.Children.Add(row);
    }

    private void SaveLiveChange()
    {
        if (currentSettings is null) return;
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
        var overlayMonitor = MonitorFromWindow(GetAnchorWindow?.Invoke() ?? settingsHwnd, 2);
        var work = GetWorkArea(overlayMonitor, anchorRect);
        SizeInt32 desired = ScaleSize(settingsHwnd, 390, 620);
        int width = Math.Min(desired.Width, work.Right - work.Left);
        int height = Math.Min(desired.Height, work.Bottom - work.Top);
        var appWindow = settingsWindow!.AppWindow;
        if (!GetWindowRect(settingsHwnd, out NativeRect currentSize)
            || currentSize.Right - currentSize.Left != width || currentSize.Bottom - currentSize.Top != height)
            appWindow.Resize(new SizeInt32(width, height));
        int x = Math.Clamp(anchorRect.X + anchorRect.Width - width, work.Left, Math.Max(work.Left, work.Right - width));
        int y = anchorRect.Y - height - toolbarHeight - 6;
        if (y < work.Top) y = Math.Min(anchorRect.Y + anchorRect.Height + toolbarHeight + 6, work.Bottom - height);
        if (!GetWindowRect(settingsHwnd, out NativeRect current)
            || current.Left != x || current.Top != y)
            SetWindowPos(settingsHwnd, HwndTopMost, x, y, width, height, SwpNoActivate);
    }

    public Func<nint>? GetAnchorWindow { get; set; }
    public Action<int, int>? MoveByKeyboard { get; set; }

    private void UpdateToolbarLabels(DesktopSubtitleOverlaySettings settings, bool adjusting)
    {
        if (moveButton is null) return;
        moveButton.IsEnabled = !settings.PositionLocked;
        AutomationProperties.SetName(moveButton, settings.PositionLocked ? "移动字幕；请先解锁位置" : "拖动字幕到新位置");
        lockButton!.Content = settings.PositionLocked ? "锁" : "开锁";
        AutomationProperties.SetName(lockButton, settings.PositionLocked ? "解锁位置" : "锁定位置");
        clickThroughButton!.Content = settings.ClickThrough ? "✓透" : "透";
        AutomationProperties.SetName(clickThroughButton, settings.ClickThrough ? "关闭点击穿透" : "开启点击穿透");
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
