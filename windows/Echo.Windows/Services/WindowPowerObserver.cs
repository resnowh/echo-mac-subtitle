using System.Runtime.InteropServices;

namespace Echo_Windows.Services;

/// <summary>Observes suspend/resume messages on the WinUI window without owning recording policy.</summary>
internal sealed class WindowPowerObserver : IDisposable
{
    private const uint WmPowerBroadcast = 0x0218;
    private const nuint PbtApmSuspend = 0x0004;
    private const nuint PbtApmResumeCritical = 0x0006;
    private const nuint PbtApmResumeSuspend = 0x0007;
    private const nuint PbtApmResumeAutomatic = 0x0012;
    private static readonly nuint SubclassId = (nuint)0x4543484F;
    private readonly nint window;
    private readonly Action suspending, resuming;
    private readonly SubclassProcedure procedure;
    private bool attached;

    private WindowPowerObserver(nint window, Action suspending, Action resuming)
    {
        this.window = window;
        this.suspending = suspending;
        this.resuming = resuming;
        procedure = WindowProcedure;
    }

    public static WindowPowerObserver? TryAttach(nint window, Action suspending, Action resuming)
    {
        var observer = new WindowPowerObserver(window, suspending, resuming);
        try
        {
            observer.attached = SetWindowSubclass(window, observer.procedure, SubclassId, 0);
            if (observer.attached) return observer;
        }
        catch { }
        return null;
    }

    private nint WindowProcedure(nint hwnd, uint message, nuint wParam, nint lParam, nuint subclassId, nuint referenceData)
    {
        if (message == WmPowerBroadcast)
        {
            try
            {
                if (wParam == PbtApmSuspend) { suspending(); return 1; }
                if (wParam is PbtApmResumeCritical or PbtApmResumeSuspend or PbtApmResumeAutomatic) { resuming(); return 1; }
            }
            catch { return 1; }
        }
        return DefSubclassProc(hwnd, message, wParam, lParam);
    }

    public void Dispose()
    {
        if (!attached) return;
        attached = false;
        RemoveWindowSubclass(window, procedure, SubclassId);
        GC.KeepAlive(procedure);
    }

    private delegate nint SubclassProcedure(nint hwnd, uint message, nuint wParam, nint lParam, nuint subclassId, nuint referenceData);

    [DllImport("comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowSubclass(nint hwnd, SubclassProcedure callback, nuint subclassId, nuint referenceData);

    [DllImport("comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveWindowSubclass(nint hwnd, SubclassProcedure callback, nuint subclassId);

    [DllImport("comctl32.dll")]
    private static extern nint DefSubclassProc(nint hwnd, uint message, nuint wParam, nint lParam);
}
