namespace Tralalero.Core.Interop;

/// <summary>Brings an already-running Tralalero window to the foreground.</summary>
public static class SingleInstance
{
    public static bool ActivateExistingWindow(string title)
    {
        var handle = NativeMethods.FindWindow(null, title);
        if (handle == IntPtr.Zero)
        {
            return false;
        }

        if (NativeMethods.IsIconic(handle))
        {
            NativeMethods.ShowWindow(handle, NativeMethods.SwRestore);
        }

        NativeMethods.SetForegroundWindow(handle);
        NativeMethods.BringWindowToTop(handle);
        return true;
    }
}
