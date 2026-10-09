using Echo_Windows.Core;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Foundation;
using Windows.Graphics;
using Windows.UI;

namespace Echo_Windows;

public sealed partial class DesktopSubtitleOverlayWindow : Window
{
    private const int GwlExStyle = -20;
    private const long WsExTransparent = 0x00000020L;
    private const long WsExLayered = 0x00080000L;
    private const long WsExToolWindow = 0x00000080L;
    private const long WsExNoActivate = 0x08000000L;
    private const uint LwaAlpha = 0x00000002;
    private readonly DesktopSubtitleOverlayFeed feed;
    private readonly Action<DesktopSubtitleOverlaySettings> settingsChanged;
    private DesktopSubtitleOverlaySettings settings;
    private readonly DispatcherQueueTimer expiryTimer;
    private readonly DispatcherQueueTimer publishTimer;
    private DesktopSubtitleOverlayState? visibleState;
    private DesktopSubtitleOverlayState? pendingState;
    private long lastPublishTimestamp;
    private bool adjusting;
    private bool resizing;
    private bool dragging;
    private bool reflowingForDisplayChange;
    private bool isClosed;
    private PointInt32 pointerOrigin;
    private PointInt32 startPosition;
    private SizeInt32 startSize;
    private double dpiScale = 1;
    private ulong activeDisplayId;
    private readonly DisplayAreaWatcher displayAreaWatcher;

    public DesktopSubtitleOverlayWindow(DesktopSubtitleOverlayFeed feed, DesktopSubtitleOverlaySettings settings, Action<DesktopSubtitleOverlaySettings> settingsChanged)
    {
        InitializeComponent();
        this.feed = feed;
        this.settingsChanged = settingsChanged;
        this.settings = settings.Validate();
        SystemBackdrop = null;
        ExtendsContentIntoTitleBar = true;
        AppWindow.SetPresenter(OverlappedPresenter.Create());
        var presenter = (OverlappedPresenter)AppWindow.Presenter;
        presenter.SetBorderAndTitleBar(false, false);
        presenter.IsAlwaysOnTop = true;
        presenter.IsMinimizable = false;
        presenter.IsMaximizable = false;
        presenter.IsResizable = false;
        AppWindow.IsShownInSwitchers = false;
        expiryTimer = DispatcherQueue.CreateTimer();
        expiryTimer.IsRepeating = false;
        expiryTimer.Tick += (_, _) => ExpireCurrentState();
        publishTimer = DispatcherQueue.CreateTimer();
        publishTimer.IsRepeating = false;
        publishTimer.Tick += (_, _) =>
        {
            var pending = pendingState;
            pendingState = null;
            if (pending is not null) Accept(pending);
        };
        feed.PropertyChanged += Feed_PropertyChanged;

        // Activate creates the HWND; hide it immediately, then show without activation after configuration.
        Activate();
        AppWindow.Hide();
        ConfigureNativeStyles();
        AppWindow.Changed += AppWindow_Changed;
        displayAreaWatcher = DisplayArea.CreateWatcher();
        displayAreaWatcher.Updated += DisplayAreaWatcher_Changed;
        displayAreaWatcher.Added += DisplayAreaWatcher_Changed;
        displayAreaWatcher.Removed += DisplayAreaWatcher_Changed;
        displayAreaWatcher.Start();
        Closed += (_, _) =>
        {
            isClosed = true;
            expiryTimer.Stop(); publishTimer.Stop();
            feed.PropertyChanged -= Feed_PropertyChanged;
            AppWindow.Changed -= AppWindow_Changed;
            displayAreaWatcher.Updated -= DisplayAreaWatcher_Changed;
            displayAreaWatcher.Added -= DisplayAreaWatcher_Changed;
            displayAreaWatcher.Removed -= DisplayAreaWatcher_Changed;
            displayAreaWatcher.Stop();
        };
        ApplySettings(this.settings, reposition: true);
    }

    public void ShowOverlay()
    {
        if (feed.Current is { } current) Accept(current);
        AppWindow.Show(false);
    }

    public void CloseOverlay() => Close();
    public void HideOverlay() => AppWindow.Hide();
    public bool IsAdjusting => adjusting;

    public void SetAdjusting(bool value)
    {
        adjusting = value;
        ResizeHandle.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        AdjustBorder.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        UpdateExtendedStyle(WsExTransparent, !value && settings.ClickThrough);
        if (value && (feed.Current is not { } state || !state.RemainsVisible(DateTimeOffset.UtcNow, settings.RetentionSeconds)))
        {
            OriginalLine.Visibility = settings.ShowOriginal ? Visibility.Visible : Visibility.Collapsed;
            OriginalText.Text = OriginalShadow.Text = "Original subtitle text";
            TranslationLine.Visibility = settings.ShowTranslation ? Visibility.Visible : Visibility.Collapsed;
            TranslationText.Text = TranslationShadow.Text = "实时双语字幕预览";
        }
        else if (!value) Accept(feed.Current);
    }

    public void ApplySettings(DesktopSubtitleOverlaySettings value, bool reposition = false)
    {
        settings = value.Validate();
        OverlayRoot.Opacity = settings.Opacity;
        OriginalText.FontSize = OriginalShadow.FontSize = settings.OriginalFontSize;
        TranslationText.FontSize = TranslationShadow.FontSize = settings.TranslationFontSize;
        OriginalShadow.Opacity = TranslationShadow.Opacity = settings.ShadowStrength;
        UpdateExtendedStyle(WsExTransparent, !adjusting && settings.ClickThrough);
        if (reposition) PlaceOnCurrentDisplay(useSavedPosition: true);
        else ResizeToConfiguredWidth();
        Accept(feed.Current);
    }

    private void ConfigureNativeStyles()
    {
        nint hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        dpiScale = Math.Max(1, GetDpiForWindow(hwnd) / 96.0);
        long style = GetWindowLongPtr(hwnd, GwlExStyle).ToInt64();
        style |= WsExLayered | WsExToolWindow | WsExNoActivate;
        SetWindowLongPtr(hwnd, GwlExStyle, new nint(style));
        SetLayeredWindowAttributes(hwnd, 0, 255, LwaAlpha);
        var margins = new Margins { Left = -1, Right = -1, Top = -1, Bottom = -1 };
        _ = DwmExtendFrameIntoClientArea(hwnd, ref margins);
    }

    private void UpdateExtendedStyle(long flag, bool enabled)
    {
        nint hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        long style = GetWindowLongPtr(hwnd, GwlExStyle).ToInt64();
        style = enabled ? style | flag : style & ~flag;
        SetWindowLongPtr(hwnd, GwlExStyle, new nint(style));
    }

    private void ResizeToConfiguredWidth()
    {
        PlaceOnCurrentDisplay(useSavedPosition: true);
    }

    private void PlaceOnCurrentDisplay(bool useSavedPosition)
    {
        DisplayArea area = useSavedPosition ? SavedOrCurrentDisplayArea() : CurrentDisplayArea();
        OverlayPlacement placement = CalculatePlacement(area, useSavedPosition);
        activeDisplayId = area.DisplayId.Value;
        settings.DisplayId = activeDisplayId;
        reflowingForDisplayChange = true;
        try
        {
            AppWindow.MoveAndResize(new RectInt32(placement.X, placement.Y, placement.Width, placement.Height), area);
        }
        finally { reflowingForDisplayChange = false; }
    }

    private DisplayArea CurrentDisplayArea() => DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest);

    private DisplayArea SavedOrCurrentDisplayArea()
    {
        if (settings.DisplayId is ulong savedDisplayId)
        {
            foreach (DisplayArea area in DisplayArea.FindAll())
                if (area.DisplayId.Value == savedDisplayId) return area;
        }
        return DisplayArea.Primary;
    }

    private OverlayPlacement CalculatePlacement(DisplayArea area, bool useSavedPosition)
    {
        RectInt32 work = area.WorkArea;
        return DesktopSubtitleOverlayPlacement.Calculate(work.X, work.Y, work.Width, work.Height, dpiScale,
            settings.WidthFraction, useSavedPosition ? settings.NormalizedX : .5, settings.NormalizedBottom);
    }

    private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (isClosed || reflowingForDisplayChange || (!args.DidPositionChange && !args.DidSizeChange)) return;

        DisplayArea area = CurrentDisplayArea();
        ulong nextDisplayId = area.DisplayId.Value;
        double nextDpiScale = Math.Max(1, GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)) / 96.0);
        if (nextDisplayId == activeDisplayId && Math.Abs(nextDpiScale - dpiScale) < .01) return;

        // Preserve the normalized position on the display the user moved to, then
        // recompute physical dimensions for that display's DPI and work area.
        PersistPlacement();
        settings.DisplayId = nextDisplayId;
        dpiScale = nextDpiScale;
        PlaceOnCurrentDisplay(useSavedPosition: true);
        ResetPointerDragBaseline();
    }

    private void DisplayAreaWatcher_Changed(DisplayAreaWatcher sender, DisplayArea displayArea)
    {
        _ = DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            if (isClosed || reflowingForDisplayChange) return;
            DisplayArea current = CurrentDisplayArea();
            ulong nextDisplayId = current.DisplayId.Value;
            if (nextDisplayId == activeDisplayId)
            {
                double nextDpiScale = Math.Max(1, GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)) / 96.0);
                if (Math.Abs(nextDpiScale - dpiScale) < .01 && displayArea.DisplayId.Value != activeDisplayId) return;
                dpiScale = nextDpiScale;
            }
            else
            {
                settings.DisplayId = nextDisplayId;
                dpiScale = Math.Max(1, GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)) / 96.0);
            }

            PlaceOnCurrentDisplay(useSavedPosition: true);
            ResetPointerDragBaseline();
            settingsChanged(settings);
        });
    }

    private void ResetPointerDragBaseline()
    {
        if (!dragging && !resizing) return;
        if (!GetCursorPos(out pointerOrigin)) return;
        if (dragging) startPosition = AppWindow.Position;
        if (resizing) startSize = AppWindow.Size;
    }

    private void Feed_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(DesktopSubtitleOverlayFeed.Current)) return;
        var state = feed.Current;
        if (state is null || visibleState is null || state.EntryId != visibleState.EntryId || state.IsFinal != visibleState.IsFinal)
        {
            publishTimer.Stop(); pendingState = null; Accept(state); return;
        }
        double elapsedMs = (Stopwatch.GetTimestamp() - lastPublishTimestamp) * 1000d / Stopwatch.Frequency;
        if (elapsedMs >= 50)
        {
            publishTimer.Stop(); pendingState = null; Accept(state); return;
        }
        pendingState = state;
        publishTimer.Interval = TimeSpan.FromMilliseconds(50 - elapsedMs);
        if (!publishTimer.IsRunning) publishTimer.Start();
    }

    private void Accept(DesktopSubtitleOverlayState? state)
    {
        lastPublishTimestamp = Stopwatch.GetTimestamp();
        expiryTimer.Stop();
        visibleState = state;
        bool show = state is { IsVisible: true } && state.RemainsVisible(DateTimeOffset.UtcNow, settings.RetentionSeconds);
        bool showOriginal = show && settings.ShowOriginal && !string.IsNullOrWhiteSpace(state!.Original);
        bool showTranslation = show && settings.ShowTranslation && state!.TranslationEnabled && !string.IsNullOrWhiteSpace(state.Translation);
        OriginalLine.Visibility = showOriginal || (adjusting && settings.ShowOriginal) ? Visibility.Visible : Visibility.Collapsed;
        TranslationLine.Visibility = showTranslation || (adjusting && settings.ShowTranslation) ? Visibility.Visible : Visibility.Collapsed;
        if (show)
        {
            OriginalText.Text = OriginalShadow.Text = state!.Original;
            TranslationText.Text = TranslationShadow.Text = state.Translation;
        }
        if (state is { IsFinal: true, FinalizedAt: { } finalizedAt })
        {
            double remaining = settings.RetentionSeconds - (DateTimeOffset.UtcNow - finalizedAt).TotalSeconds;
            if (remaining > 0)
            {
                expiryTimer.Interval = TimeSpan.FromSeconds(remaining);
                expiryTimer.Start();
            }
        }
    }

    private void ExpireCurrentState()
    {
        if (visibleState is { IsFinal: true } state && !state.RemainsVisible(DateTimeOffset.UtcNow, settings.RetentionSeconds))
        {
            OriginalLine.Visibility = TranslationLine.Visibility = Visibility.Collapsed;
        }
    }

    private void OverlayRoot_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!adjusting || resizing) return;
        if (!GetCursorPos(out pointerOrigin)) return;
        startPosition = AppWindow.Position;
        dragging = true;
        OverlayRoot.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OverlayRoot_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!adjusting || resizing || !dragging) return;
        if (!GetCursorPos(out PointInt32 pointer)) return;
        AppWindow.Move(new PointInt32(startPosition.X + pointer.X - pointerOrigin.X,
            startPosition.Y + pointer.Y - pointerOrigin.Y));
        PersistPlacement();
    }

    private void OverlayRoot_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        OverlayRoot.ReleasePointerCapture(e.Pointer);
        dragging = false;
        PersistPlacement(save: true);
    }

    private void ResizeHandle_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!adjusting) return;
        resizing = true;
        if (!GetCursorPos(out pointerOrigin)) return;
        startSize = AppWindow.Size;
        ResizeHandle.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void ResizeHandle_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!resizing) return;
        if (!GetCursorPos(out PointInt32 pointer)) return;
        DisplayArea area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest);
        int maxWidth = Math.Max(1, (int)(area.WorkArea.Width * .95));
        int minWidth = Math.Min(maxWidth, (int)Math.Ceiling(280 * dpiScale));
        int width = Math.Clamp(startSize.Width + pointer.X - pointerOrigin.X, minWidth, maxWidth);
        AppWindow.Resize(new SizeInt32(width, startSize.Height));
        settings.WidthFraction = Math.Clamp((double)width / area.WorkArea.Width, .35, .95);
        PersistPlacement();
    }

    private void ResizeHandle_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        resizing = false;
        ResizeHandle.ReleasePointerCapture(e.Pointer);
        PersistPlacement(save: true);
    }

    private void PersistPlacement(bool save = false)
    {
        DisplayArea area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest);
        RectInt32 work = area.WorkArea;
        settings.NormalizedX = Math.Clamp((double)(AppWindow.Position.X - work.X + AppWindow.Size.Width / 2) / work.Width, 0, 1);
        settings.NormalizedBottom = Math.Clamp((double)(work.Y + work.Height - (AppWindow.Position.Y + AppWindow.Size.Height)) / work.Height, 0, 1);
        settings.DisplayId = area.DisplayId.Value;
        if (save) settingsChanged(settings);
    }

    [StructLayout(LayoutKind.Sequential)] private struct Margins { public int Left, Right, Top, Bottom; }
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)] private static extern nint GetWindowLongPtr(nint hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)] private static extern nint SetWindowLongPtr(nint hwnd, int index, nint value);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetLayeredWindowAttributes(nint hwnd, uint colorKey, byte alpha, uint flags);
    [DllImport("dwmapi.dll", SetLastError = true)] private static extern int DwmExtendFrameIntoClientArea(nint hwnd, ref Margins margins);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(nint hwnd);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool GetCursorPos(out PointInt32 point);
}
