using Microsoft.UI.Xaml;
using Echo_Windows.Services;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Echo_Windows;

/// <summary>
/// The application window. This hosts a Frame that displays pages. Add your
/// UI and logic to MainPage.xaml / MainPage.xaml.cs instead of here so you
/// can use Page features such as navigation events and the Loaded lifecycle.
/// </summary>
public sealed partial class MainWindow : Window
{
    private bool closingAfterSave;
    private WindowPowerObserver? powerObserver;
    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        AppWindow.SetIcon("Assets/AppIcon.ico");

        // Navigate the root frame to the main page on startup.
        RootFrame.Navigate(typeof(MainPage));
        if (RootFrame.Content is MainPage page)
        {
            powerObserver = WindowPowerObserver.TryAttach(WinRT.Interop.WindowNative.GetWindowHandle(this),
                () => _ = page.ViewModel.HandleSystemSuspendingAsync(),
                page.ViewModel.HandleSystemResuming);
            if (powerObserver is null) page.ViewModel.Status = "睡眠唤醒监听不可用；系统唤醒后请手动重新开始录音。";
        }
        Closed += (_, _) =>
        {
            powerObserver?.Dispose();
            if (RootFrame.Content is MainPage page) page.CloseSubtitleOverlay();
        };
        var scale = GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)) / 96.0;
        var work = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(AppWindow.Id, Microsoft.UI.Windowing.DisplayAreaFallback.Primary).WorkArea;
        AppWindow.Resize(new Windows.Graphics.SizeInt32(Math.Min((int)(920 * scale), work.Width), Math.Min((int)(720 * scale), work.Height)));
        AppWindow.Closing += async (_, e) =>
        {
            if (closingAfterSave) return;
            if (RootFrame.Content is MainPage page)
            {
                if (page.ViewModel.IsBusy) { e.Cancel = true; return; }
                e.Cancel = true;
                page.ViewModel.CancelPendingWakeRecovery();
                if (page.ViewModel.IsRecording) await page.ViewModel.StopAsync();
                if (page.ViewModel.IsBusy) return;
                if (!await page.ViewModel.FlushPendingSavesAsync()) return;
                closingAfterSave = true;
                Close();
            }
        };
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint window);
}
