using Echo_Windows.Core;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.UI.Dispatching;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Graphics.DirectX;
using Windows.UI;
using Windows.UI.Text;

namespace Echo_Windows;

/// <summary>
/// A small native layered HWND for the desktop subtitle surface. WinUI remains the
/// application UI; this window is rendered into a premultiplied-alpha Direct2D
/// bitmap and submitted with UpdateLayeredWindow.
/// </summary>
public sealed class NativeDesktopSubtitleOverlayWindow
{
    private const int GwlExStyle = -20;
    private const long WsExTransparent = 0x00000020L;
    private const long WsExLayered = 0x00080000L;
    private const long WsExToolWindow = 0x00000080L;
    private const long WsExNoActivate = 0x08000000L;
    private const uint WsPopup = 0x80000000;
    private const uint UlwAlpha = 0x00000002;
    private const byte AcSrcOver = 0;
    private const byte AcSrcAlpha = 1;
    private const int SwHide = 0;
    private const int SwShowNoActivate = 4;
    private const int WmNcDestroy = 0x0082;
    private const int WmDisplayChange = 0x007E;
    private const int WmDpiChanged = 0x02E0;
    private const int WmSettingChange = 0x001A;
    private const nuint SpiSetWorkArea = 0x002F;
    private const int WmSize = 0x0005;
    private const int WmEraseBkgnd = 0x0014;
    private const int WmLButtonDown = 0x0201;
    private const int WmLButtonUp = 0x0202;
    private const int WmMouseMove = 0x0200;
    private const int WmPaint = 0x000F;
    private const int WmCancelMode = 0x001F;
    private const int WmCaptureChanged = 0x0215;
    private const int MonitorDefaultToNearest = 2;
    private const uint DibRgbColors = 0;
    private const uint BiRgb = 0;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpFrameChanged = 0x0020;
    private const uint SwpShowWindow = 0x0040;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint MkLButton = 0x0001;
    private const int ResizeGripDip = 22;
    private const int OverlayHeightDip = 150;
    private const int HorizontalPaddingDip = 18;

    private static readonly string WindowClassName = "Echo.Windows.NativeSubtitleOverlay.1";
    private static readonly ConcurrentDictionary<nint, NativeDesktopSubtitleOverlayWindow> Instances = new();
    private static readonly WindowProcedure Procedure = WindowProc;
    private static readonly nint ProcedurePointer = Marshal.GetFunctionPointerForDelegate(Procedure);
    private static bool classRegistered;
    private static readonly object ClassRegistrationLock = new();

    private readonly DesktopSubtitleOverlayFeed feed;
    private readonly Action<DesktopSubtitleOverlaySettings> settingsChanged;
    private readonly DispatcherQueue dispatcherQueue;
    private readonly nint anchorWindow;
    private readonly DispatcherQueueTimer expiryTimer;
    private readonly DispatcherQueueTimer publishTimer;
    private readonly CanvasDevice canvasDevice;
    private DesktopSubtitleOverlaySettings settings;
    private nint hwnd;
    private DesktopSubtitleOverlayState? visibleState;
    private DesktopSubtitleOverlayState? pendingState;
    private long lastPublishTimestamp;
    private bool adjusting;
    private bool dragging;
    private bool resizing;
    private bool changingPlacement;
    private bool isClosed;
    private PointInt32 dragPointerOrigin;
    private PointInt32 dragWindowOrigin;
    private SizeInt32 resizeStartSize;
    private double dpiScale = 1;
    private CanvasRenderTarget? renderTarget;
    private int renderTargetWidth;
    private int renderTargetHeight;
    private nint memoryDc;
    private nint dibBitmap;
    private nint previousBitmap;
    private nint dibBits;
    private int dibWidth;
    private int dibHeight;

    public NativeDesktopSubtitleOverlayWindow(
        DesktopSubtitleOverlayFeed feed,
        DesktopSubtitleOverlaySettings settings,
        Action<DesktopSubtitleOverlaySettings> settingsChanged,
        DispatcherQueue dispatcherQueue,
        nint anchorWindow)
    {
        this.feed = feed;
        this.settingsChanged = settingsChanged;
        this.dispatcherQueue = dispatcherQueue;
        this.anchorWindow = anchorWindow;
        this.settings = settings.Validate();
        canvasDevice = CanvasDevice.GetSharedDevice();

        RegisterWindowClass();
        hwnd = CreateWindowEx(
            unchecked((uint)(WsExLayered | WsExToolWindow | WsExNoActivate | (this.settings.ClickThrough ? WsExTransparent : 0))),
            WindowClassName,
            "Echo 悬浮字幕",
            WsPopup,
            0, 0, 1, 1,
            0, 0, GetModuleHandle(null), 0);
        if (hwnd == 0)
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "无法创建透明悬浮字幕窗口。");

        Instances[hwnd] = this;
        dpiScale = Math.Max(1, GetDpiForWindow(hwnd) / 96.0);
        expiryTimer = dispatcherQueue.CreateTimer();
        expiryTimer.IsRepeating = false;
        expiryTimer.Tick += (_, _) => ExpireCurrentState();
        publishTimer = dispatcherQueue.CreateTimer();
        publishTimer.IsRepeating = false;
        publishTimer.Tick += (_, _) =>
        {
            DesktopSubtitleOverlayState? pending = pendingState;
            pendingState = null;
            if (pending is not null) Accept(pending);
        };
        feed.PropertyChanged += Feed_PropertyChanged;
        EnsureDib(1, 1);
        ApplySettings(this.settings, reposition: true);
    }

    public bool IsAdjusting => adjusting;

    public void ShowOverlay()
    {
        if (feed.Current is { } current) Accept(current);
        ShowWindow(hwnd, SwShowNoActivate);
        SetWindowPos(hwnd, HwndTopMost, 0, 0, 0, 0,
            SwpNoActivate | SwpShowWindow | SwpNoSize | SwpNoMove);
        RenderAndPresent();
    }

    public void HideOverlay()
    {
        if (hwnd != 0) ShowWindow(hwnd, SwHide);
    }

    public void CloseOverlay()
    {
        if (isClosed) return;
        isClosed = true;
        expiryTimer.Stop();
        publishTimer.Stop();
        feed.PropertyChanged -= Feed_PropertyChanged;
        if (hwnd != 0)
        {
            Instances.TryRemove(hwnd, out _);
            DestroyWindow(hwnd);
            hwnd = 0;
        }
        ReleaseDrawingResources();
    }

    public void SetAdjusting(bool value)
    {
        adjusting = value;
        dragging = false;
        resizing = false;
        UpdateInteractionStyle();
        if (value && (feed.Current is not { } state || !state.RemainsVisible(DateTimeOffset.UtcNow, settings.RetentionSeconds)))
        {
            visibleState = null;
            UpdateAccessibleName(null);
        }
        else if (!value)
        {
            Accept(feed.Current);
        }
        RenderAndPresent();
    }

    public void ApplySettings(DesktopSubtitleOverlaySettings value, bool reposition = false)
    {
        settings = value.Validate();
        UpdateInteractionStyle();
        if (reposition) PlaceOnSelectedDisplay(useSavedPosition: true);
        else ResizeToConfiguredWidth();
        Accept(feed.Current);
    }

    private void UpdateInteractionStyle()
    {
        if (hwnd == 0) return;
        long style = GetWindowLongPointer(hwnd, GwlExStyle).ToInt64();
        bool transparent = !adjusting && settings.ClickThrough;
        style = transparent ? style | WsExTransparent : style & ~WsExTransparent;
        style |= WsExLayered | WsExToolWindow | WsExNoActivate;
        SetWindowLongPointer(hwnd, GwlExStyle, new nint(style));
        SetWindowPos(hwnd, 0, 0, 0, 0, 0,
            SwpNoActivate | SwpNoZOrder | SwpNoSize | SwpNoMove | SwpFrameChanged);
    }

    private void PlaceOnSelectedDisplay(bool useSavedPosition)
    {
        nint monitor = useSavedPosition ? FindSavedMonitor() : 0;
        if (monitor == 0)
            monitor = MonitorFromWindow(anchorWindow != 0 ? anchorWindow : hwnd, MonitorDefaultToNearest);
        if (monitor == 0 || !TryGetMonitorInfo(monitor, out MonitorInfo monitorInfo)) return;

        settings.DisplayDeviceName = monitorInfo.DeviceName;
        UpdateDpi();
        int workWidth = monitorInfo.WorkArea.Right - monitorInfo.WorkArea.Left;
        int workHeight = monitorInfo.WorkArea.Bottom - monitorInfo.WorkArea.Top;
        OverlayPlacement placement = DesktopSubtitleOverlayPlacement.Calculate(
            monitorInfo.WorkArea.Left, monitorInfo.WorkArea.Top, workWidth, workHeight, dpiScale,
            settings.WidthFraction, useSavedPosition ? settings.NormalizedX : .5,
            useSavedPosition ? settings.NormalizedBottom : .09);
        double initialDpiScale = dpiScale;
        changingPlacement = true;
        SetWindowPos(hwnd, HwndTopMost, placement.X, placement.Y, placement.Width, placement.Height,
            SwpNoActivate);
        UpdateDpi();
        if (Math.Abs(dpiScale - initialDpiScale) > .01)
        {
            placement = DesktopSubtitleOverlayPlacement.Calculate(
                monitorInfo.WorkArea.Left, monitorInfo.WorkArea.Top, workWidth, workHeight, dpiScale,
                settings.WidthFraction, useSavedPosition ? settings.NormalizedX : .5,
                useSavedPosition ? settings.NormalizedBottom : .09);
            SetWindowPos(hwnd, HwndTopMost, placement.X, placement.Y, placement.Width, placement.Height,
                SwpNoActivate);
            UpdateDpi();
        }
        changingPlacement = false;
        EnsureDib(placement.Width, placement.Height);
        UpdateAccessibleName(visibleState);
        RenderAndPresent();
    }

    private void ResizeToConfiguredWidth() => PlaceOnSelectedDisplay(useSavedPosition: true);

    private nint FindSavedMonitor()
    {
        if (string.IsNullOrWhiteSpace(settings.DisplayDeviceName)) return 0;
        nint match = 0;
        MonitorEnumerator callback = (monitor, _, _, _) =>
        {
            if (TryGetMonitorInfo(monitor, out MonitorInfo info)
                && string.Equals(info.DeviceName, settings.DisplayDeviceName, StringComparison.OrdinalIgnoreCase))
            {
                match = monitor;
                return false;
            }
            return true;
        };
        EnumDisplayMonitors(0, 0, callback, 0);
        GC.KeepAlive(callback);
        return match;
    }

    private void UpdateDpi()
    {
        if (hwnd == 0) return;
        dpiScale = Math.Max(1, GetDpiForWindow(hwnd) / 96.0);
    }

    private void PersistPlacement(bool save)
    {
        if (hwnd == 0) return;
        nint monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        if (monitor == 0 || !TryGetMonitorInfo(monitor, out MonitorInfo info)) return;
        if (!GetWindowRect(hwnd, out NativeRect window)) return;
        int workWidth = info.WorkArea.Right - info.WorkArea.Left;
        int workHeight = info.WorkArea.Bottom - info.WorkArea.Top;
        if (workWidth <= 0 || workHeight <= 0) return;

        settings.DisplayDeviceName = info.DeviceName;
        settings.NormalizedX = Math.Clamp(((window.Left + window.Right) / 2d - info.WorkArea.Left) / workWidth, 0, 1);
        settings.NormalizedBottom = Math.Clamp((info.WorkArea.Bottom - window.Bottom) / (double)workHeight, 0, 1);
        settings.WidthFraction = Math.Clamp((window.Right - window.Left) / (double)workWidth, .35, .95);
        if (save) settingsChanged(settings);
    }

    private void Feed_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(DesktopSubtitleOverlayFeed.Current)) return;
        DesktopSubtitleOverlayState? state = feed.Current;
        if (state is null || visibleState is null || state.EntryId != visibleState.EntryId || state.IsFinal != visibleState.IsFinal)
        {
            publishTimer.Stop();
            pendingState = null;
            Accept(state);
            return;
        }

        double elapsedMs = (Stopwatch.GetTimestamp() - lastPublishTimestamp) * 1000d / Stopwatch.Frequency;
        if (elapsedMs >= 50)
        {
            publishTimer.Stop();
            pendingState = null;
            Accept(state);
            return;
        }

        pendingState = state;
        publishTimer.Interval = TimeSpan.FromMilliseconds(50 - elapsedMs);
        if (!publishTimer.IsRunning) publishTimer.Start();
    }

    private void Accept(DesktopSubtitleOverlayState? state)
    {
        if (isClosed) return;
        lastPublishTimestamp = Stopwatch.GetTimestamp();
        expiryTimer.Stop();
        visibleState = state;
        bool show = state is { IsVisible: true } && state.RemainsVisible(DateTimeOffset.UtcNow, settings.RetentionSeconds);
        UpdateAccessibleName(show ? state : null);
        RenderAndPresent();

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
        if (visibleState is { IsFinal: true } state
            && !state.RemainsVisible(DateTimeOffset.UtcNow, settings.RetentionSeconds))
        {
            UpdateAccessibleName(null);
            RenderAndPresent();
        }
    }

    private void UpdateAccessibleName(DesktopSubtitleOverlayState? state)
    {
        if (hwnd == 0) return;
        string name = state is null
            ? "Echo 悬浮字幕"
            : string.Join("\n", new[] { state.Original, state.Translation }
                .Where((value, index) => !string.IsNullOrWhiteSpace(value)
                    && (index == 0 ? DesktopSubtitleOverlayPresentation.ShouldShowOriginal(state, settings)
                        : DesktopSubtitleOverlayPresentation.ShouldShowTranslation(state, settings))));
        SetWindowText(hwnd, name);
    }

    private void RenderAndPresent()
    {
        if (hwnd == 0 || isClosed || !GetWindowRect(hwnd, out NativeRect bounds)) return;
        int width = Math.Max(1, bounds.Right - bounds.Left);
        int height = Math.Max(1, bounds.Bottom - bounds.Top);
        EnsureDib(width, height);
        if (renderTarget is null || renderTargetWidth != width || renderTargetHeight != height)
        {
            renderTarget?.Dispose();
            renderTarget = new CanvasRenderTarget(canvasDevice, width, height, 96,
                DirectXPixelFormat.B8G8R8A8UIntNormalized, CanvasAlphaMode.Premultiplied);
            renderTargetWidth = width;
            renderTargetHeight = height;
        }

        using (CanvasDrawingSession drawing = renderTarget.CreateDrawingSession())
        {
            drawing.Clear(Color.FromArgb(0, 0, 0, 0));
            if (adjusting)
            {
                // Nonzero, premultiplied alpha keeps the transparent surface
                // available for drag hit-testing while remaining visually clear.
                using var hitTestBrush = new CanvasSolidColorBrush(canvasDevice, Color.FromArgb(1, 0, 0, 0));
                drawing.FillRectangle(new Rect(0, 0, width, height), hitTestBrush);
                using var frameBrush = new CanvasSolidColorBrush(canvasDevice, Color.FromArgb(210, 112, 231, 199));
                drawing.DrawRectangle(1, 1, Math.Max(0, width - 2), Math.Max(0, height - 2), frameBrush, 1.5f * (float)dpiScale);
                DrawResizeGrip(drawing, width, height);
            }
            DrawSubtitleText(drawing, width, height);
        }

        byte[] pixels = renderTarget.GetPixelBytes();
        Marshal.Copy(pixels, 0, dibBits, Math.Min(pixels.Length, dibWidth * dibHeight * 4));
        var destination = new NativePoint(bounds.Left, bounds.Top);
        var size = new NativeSize(width, height);
        var source = new NativePoint(0, 0);
        var blend = new BlendFunction { BlendOp = AcSrcOver, BlendFlags = 0, SourceConstantAlpha = 255, AlphaFormat = AcSrcAlpha };
        if (!UpdateLayeredWindow(hwnd, 0, ref destination, ref size, memoryDc, ref source, 0, ref blend, UlwAlpha))
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "UpdateLayeredWindow failed to present transparent subtitle pixels.");
    }

    private void DrawSubtitleText(CanvasDrawingSession drawing, int width, int height)
    {
        DesktopSubtitleOverlayState? state = visibleState;
        bool show = state is { IsVisible: true } && state.RemainsVisible(DateTimeOffset.UtcNow, settings.RetentionSeconds);
        bool preview = adjusting && !show;
        if (!show && !preview) return;

        bool showOriginal = show
            ? DesktopSubtitleOverlayPresentation.ShouldShowOriginal(state!, settings)
            : settings.ShowOriginal || !settings.ShowTranslation;
        bool showTranslation = show
            ? DesktopSubtitleOverlayPresentation.ShouldShowTranslation(state!, settings)
            : settings.ShowTranslation;
        string original = show ? state!.Original : "Original subtitle text";
        string translation = show ? state!.Translation : "实时双语字幕预览";
        float scale = (float)dpiScale;
        float horizontal = HorizontalPaddingDip * scale;
        float contentWidth = Math.Max(1, width - 2 * horizontal);
        float originalHeight = (float)(settings.OriginalFontSize * 2.35 * scale);
        float translationHeight = (float)(settings.TranslationFontSize * 2.35 * scale);
        float gap = 5 * scale;
        float visibleHeight = (showOriginal ? originalHeight : 0) + (showTranslation ? translationHeight : 0)
            + (showOriginal && showTranslation ? gap : 0);
        float y = Math.Max(0, (height - visibleHeight) / 2f);

        if (showOriginal && !string.IsNullOrWhiteSpace(original))
        {
            DrawTextLine(drawing, original, contentWidth, originalHeight, y,
                (float)settings.OriginalFontSize * scale, 500, true);
            y += originalHeight + (showTranslation ? gap : 0);
        }
        if (showTranslation && !string.IsNullOrWhiteSpace(translation))
        {
            DrawTextLine(drawing, translation, contentWidth, translationHeight, y,
                (float)settings.TranslationFontSize * scale, 400, true);
        }
    }

    private void DrawTextLine(CanvasDrawingSession drawing, string text, float width, float height,
        float y, float fontSize, ushort weight, bool shadow)
    {
        using var format = new CanvasTextFormat
        {
            FontFamily = "Segoe UI",
            FontSize = fontSize,
            FontWeight = new FontWeight { Weight = weight },
            HorizontalAlignment = CanvasHorizontalAlignment.Center,
            VerticalAlignment = CanvasVerticalAlignment.Center,
            WordWrapping = CanvasWordWrapping.Wrap,
            TrimmingGranularity = CanvasTextTrimmingGranularity.Character,
            TrimmingSign = CanvasTrimmingSign.Ellipsis
        };
        var rect = new Rect(HorizontalPaddingDip * dpiScale, y, width, height);
        byte opacity = (byte)Math.Clamp((int)Math.Round(settings.Opacity * 255), 0, 255);
        if (shadow && settings.ShadowStrength > 0)
        {
            byte shadowAlpha = (byte)Math.Clamp((int)Math.Round(opacity * settings.ShadowStrength), 0, 255);
            using var shadowBrush = new CanvasSolidColorBrush(canvasDevice, Color.FromArgb(shadowAlpha, 0, 0, 0));
            double dx = 0.8 * dpiScale;
            double dy = 1.5 * dpiScale;
            var shadowRect = new Rect(rect.X, rect.Y + dy, rect.Width, rect.Height);
            drawing.DrawText(text, shadowRect, shadowBrush, format);
            shadowRect = new Rect(rect.X - dx, rect.Y + dy, rect.Width, rect.Height);
            drawing.DrawText(text, shadowRect, shadowBrush, format);
            shadowRect = new Rect(rect.X + dx, rect.Y + dy, rect.Width, rect.Height);
            drawing.DrawText(text, shadowRect, shadowBrush, format);
        }
        using var textBrush = new CanvasSolidColorBrush(canvasDevice, Color.FromArgb(opacity, 255, 255, 255));
        drawing.DrawText(text, rect, textBrush, format);
    }

    private void DrawResizeGrip(CanvasDrawingSession drawing, int width, int height)
    {
        float scale = (float)dpiScale;
        using var gripBrush = new CanvasSolidColorBrush(canvasDevice, Color.FromArgb(230, 112, 231, 199));
        float right = width - 5 * scale;
        float bottom = height - 5 * scale;
        for (int inset = 0; inset < 3; inset++)
        {
            float delta = inset * 4 * scale;
            drawing.DrawLine(right - 12 * scale + delta, bottom, right, bottom - 12 * scale + delta,
                gripBrush, 1.4f * scale);
        }
    }

    private void EnsureDib(int width, int height)
    {
        if (width <= 0 || height <= 0 || (memoryDc != 0 && dibWidth == width && dibHeight == height)) return;
        ReleaseDib();
        memoryDc = CreateCompatibleDC(0);
        if (memoryDc == 0) throw new Win32Exception(Marshal.GetLastPInvokeError(), "CreateCompatibleDC failed.");
        var info = new BitmapInfo
        {
            Header = new BitmapInfoHeader
            {
                Size = (uint)Marshal.SizeOf<BitmapInfoHeader>(),
                Width = width,
                Height = -height,
                Planes = 1,
                BitCount = 32,
                Compression = BiRgb,
                SizeImage = (uint)(width * height * 4)
            }
        };
        dibBitmap = CreateDIBSection(memoryDc, ref info, DibRgbColors, out dibBits, 0, 0);
        if (dibBitmap == 0 || dibBits == 0)
        {
            ReleaseDib();
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "CreateDIBSection failed.");
        }
        previousBitmap = SelectObject(memoryDc, dibBitmap);
        dibWidth = width;
        dibHeight = height;
    }

    private void ReleaseDrawingResources()
    {
        renderTarget?.Dispose();
        renderTarget = null;
        ReleaseDib();
    }

    private void ReleaseDib()
    {
        if (memoryDc != 0 && previousBitmap != 0) SelectObject(memoryDc, previousBitmap);
        if (dibBitmap != 0) DeleteObject(dibBitmap);
        if (memoryDc != 0) DeleteDC(memoryDc);
        memoryDc = dibBitmap = previousBitmap = dibBits = 0;
        dibWidth = dibHeight = 0;
    }

    private void OnSizeChanged()
    {
        if (changingPlacement || !GetWindowRect(hwnd, out NativeRect rect)) return;
        EnsureDib(Math.Max(1, rect.Right - rect.Left), Math.Max(1, rect.Bottom - rect.Top));
        RenderAndPresent();
    }

    private void OnDisplayConfigurationChanged()
    {
        if (isClosed || changingPlacement) return;
        UpdateDpi();
        PlaceOnSelectedDisplay(useSavedPosition: true);
        PersistPlacement(save: true);
    }

    private void OnDpiChanged(nint suggestedRectPointer)
    {
        if (suggestedRectPointer != 0)
        {
            NativeRect suggested = Marshal.PtrToStructure<NativeRect>(suggestedRectPointer);
            changingPlacement = true;
            SetWindowPos(hwnd, HwndTopMost, suggested.Left, suggested.Top,
                suggested.Right - suggested.Left, suggested.Bottom - suggested.Top, SwpNoActivate);
            changingPlacement = false;
        }
        // The moved window may now belong to a different monitor. Save that
        // monitor and its normalized position before recomputing physical size.
        PersistPlacement(save: false);
        UpdateDpi();
        OnDisplayConfigurationChanged();
    }

    private nint HandleMessage(uint message, nuint wParam, nint lParam)
    {
        switch (message)
        {
            case WmEraseBkgnd:
            case WmPaint:
                return 1;
            case WmSize:
                OnSizeChanged();
                return 0;
            case WmDisplayChange:
                OnDisplayConfigurationChanged();
                return 0;
            case WmSettingChange when wParam == SpiSetWorkArea:
                OnDisplayConfigurationChanged();
                return 0;
            case WmDpiChanged:
                if (changingPlacement)
                {
                    UpdateDpi();
                    return 0;
                }
                OnDpiChanged(lParam);
                return 0;
            case WmLButtonDown when adjusting:
                BeginPointerOperation(lParam);
                return 0;
            case WmMouseMove when adjusting && (wParam & MkLButton) != 0:
                ContinuePointerOperation();
                return 0;
            case WmLButtonUp when adjusting:
                EndPointerOperation();
                return 0;
            case WmCancelMode:
            case WmCaptureChanged:
                dragging = false;
                resizing = false;
                return 0;
            case WmNcDestroy:
                if (hwnd != 0) Instances.TryRemove(hwnd, out _);
                return DefWindowProc(hwnd, message, wParam, lParam);
            default:
                return DefWindowProc(hwnd, message, wParam, lParam);
        }
    }

    private void BeginPointerOperation(nint lParam)
    {
        if (!GetCursorPos(out dragPointerOrigin) || !GetWindowRect(hwnd, out NativeRect rect)) return;
        int mouseX = unchecked((short)((long)lParam & 0xFFFF));
        int mouseY = unchecked((short)(((long)lParam >> 16) & 0xFFFF));
        int grip = (int)Math.Ceiling(ResizeGripDip * dpiScale);
        resizing = mouseX >= rect.Right - rect.Left - grip && mouseY >= rect.Bottom - rect.Top - grip;
        dragging = !resizing;
        dragWindowOrigin = new PointInt32(rect.Left, rect.Top);
        resizeStartSize = new SizeInt32(rect.Right - rect.Left, rect.Bottom - rect.Top);
        SetCapture(hwnd);
    }

    private void ContinuePointerOperation()
    {
        if (!GetCursorPos(out PointInt32 pointer)) return;
        nint monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        if (monitor == 0 || !TryGetMonitorInfo(monitor, out MonitorInfo info)) return;
        if (dragging)
        {
            SetWindowPos(hwnd, HwndTopMost,
                dragWindowOrigin.X + pointer.X - dragPointerOrigin.X,
                dragWindowOrigin.Y + pointer.Y - dragPointerOrigin.Y,
                0, 0, SwpNoActivate | SwpNoZOrder | SwpShowWindow | SwpNoSize);
            PersistPlacement(save: false);
            return;
        }
        if (!resizing) return;
        int workWidth = info.WorkArea.Right - info.WorkArea.Left;
        int maxWidth = Math.Max(1, (int)(workWidth * .95));
        int minWidth = Math.Min(maxWidth, (int)Math.Ceiling(280 * dpiScale));
        int width = Math.Clamp(resizeStartSize.Width + pointer.X - dragPointerOrigin.X, minWidth, maxWidth);
        changingPlacement = true;
        SetWindowPos(hwnd, HwndTopMost, 0, 0, width, resizeStartSize.Height,
            SwpNoActivate | SwpNoZOrder | SwpNoMove);
        changingPlacement = false;
        settings.WidthFraction = Math.Clamp((double)width / workWidth, .35, .95);
        EnsureDib(width, resizeStartSize.Height);
        PersistPlacement(save: false);
        RenderAndPresent();
    }

    private void EndPointerOperation()
    {
        if (!dragging && !resizing) return;
        dragging = false;
        resizing = false;
        ReleaseCapture();
        PersistPlacement(save: true);
        RenderAndPresent();
    }

    private static void RegisterWindowClass()
    {
        lock (ClassRegistrationLock)
        {
            if (classRegistered) return;
            var windowClass = new WindowClassEx
            {
                Size = (uint)Marshal.SizeOf<WindowClassEx>(),
                WindowProcedure = ProcedurePointer,
                Instance = GetModuleHandle(null),
                ClassName = WindowClassName
            };
            ushort atom = RegisterClassEx(ref windowClass);
            if (atom == 0 && Marshal.GetLastPInvokeError() != 1410)
                throw new Win32Exception(Marshal.GetLastPInvokeError(), "无法注册原生悬浮字幕窗口类。");
            classRegistered = true;
        }
    }

    private static nint WindowProc(nint window, uint message, nuint wParam, nint lParam)
    {
        if (Instances.TryGetValue(window, out NativeDesktopSubtitleOverlayWindow? instance))
        {
            try { return instance.HandleMessage(message, wParam, lParam); }
            catch (Exception exception)
            {
                Debug.WriteLine($"Native subtitle overlay message {message:X} failed: {exception}");
                return DefWindowProc(window, message, wParam, lParam);
            }
        }
        return DefWindowProc(window, message, wParam, lParam);
    }

    private static bool TryGetMonitorInfo(nint monitor, out MonitorInfo info)
    {
        info = new MonitorInfo { Size = (uint)Marshal.SizeOf<MonitorInfo>(), DeviceName = string.Empty };
        return GetMonitorInfo(monitor, ref info);
    }

    private static nint HwndTopMost => new(-1);

    private static nint GetWindowLongPointer(nint window, int index) => IntPtr.Size == 8
        ? GetWindowLongPtr64(window, index)
        : new nint(GetWindowLong32(window, index));

    private static nint SetWindowLongPointer(nint window, int index, nint value) => IntPtr.Size == 8
        ? SetWindowLongPtr64(window, index, value)
        : new nint(SetWindowLong32(window, index, value.ToInt32()));

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nint WindowProcedure(nint hwnd, uint message, nuint wParam, nint lParam);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private delegate bool MonitorEnumerator(nint monitor, nint hdc, nint rect, nint data);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WindowClassEx
    {
        public uint Size;
        public uint Style;
        public nint WindowProcedure;
        public int ClassExtra;
        public int WindowExtra;
        public nint Instance;
        public nint Icon;
        public nint Cursor;
        public nint Background;
        public string? MenuName;
        public string ClassName;
        public nint SmallIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint { public int X, Y; public NativePoint(int x, int y) => (X, Y) = (x, y); }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeSize { public int Width, Height; public NativeSize(int width, int height) => (Width, Height) = (width, height); }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    private struct BlendFunction { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }
    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public uint Size;
        public int Width;
        public int Height;
        public ushort Planes;
        public ushort BitCount;
        public uint Compression;
        public uint SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public uint ClrUsed;
        public uint ClrImportant;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo { public BitmapInfoHeader Header; public uint Colors; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfo
    {
        public uint Size;
        public NativeRect MonitorArea;
        public NativeRect WorkArea;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClassEx(ref WindowClassEx windowClass);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateWindowEx(uint exStyle, string className, string windowName, uint style,
        int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool DestroyWindow(nint hwnd);
    [DllImport("user32.dll", EntryPoint = "DefWindowProcW", CharSet = CharSet.Unicode)] private static extern nint DefWindowProc(nint hwnd, uint message, nuint wParam, nint lParam);
    [DllImport("user32.dll")] private static extern bool ShowWindow(nint hwnd, int command);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowPos(nint hwnd, nint insertAfter,
        int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)] private static extern nint GetWindowLongPtr64(nint hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)] private static extern int GetWindowLong32(nint hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)] private static extern nint SetWindowLongPtr64(nint hwnd, int index, nint value);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)] private static extern int SetWindowLong32(nint hwnd, int index, int value);
    [DllImport("user32.dll", EntryPoint = "SetWindowTextW", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool SetWindowText(nint hwnd, string text);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool GetWindowRect(nint hwnd, out NativeRect rect);
    [DllImport("user32.dll", SetLastError = true)] private static extern nint SetCapture(nint hwnd);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool ReleaseCapture();
    [DllImport("user32.dll", SetLastError = true)] private static extern bool GetCursorPos(out PointInt32 point);
    [DllImport("user32.dll", SetLastError = true)] private static extern nint MonitorFromWindow(nint hwnd, int flags);
    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool EnumDisplayMonitors(nint hdc, nint clip, MonitorEnumerator callback, nint data);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(nint hwnd);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern nint GetModuleHandle(string? moduleName);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern nint CreateCompatibleDC(nint hdc);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern bool DeleteDC(nint hdc);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern nint CreateDIBSection(nint hdc, ref BitmapInfo info,
        uint usage, out nint bits, nint section, uint offset);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern nint SelectObject(nint hdc, nint obj);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern bool DeleteObject(nint obj);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool UpdateLayeredWindow(nint hwnd, nint destinationDc,
        ref NativePoint destination, ref NativeSize size, nint sourceDc, ref NativePoint source, uint colorKey,
        ref BlendFunction blend, uint flags);
}
