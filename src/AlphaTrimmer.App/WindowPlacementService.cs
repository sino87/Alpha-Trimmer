using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using AlphaTrimmer.Core;

namespace AlphaTrimmer.App;

internal static class WindowPlacementService
{
    internal static void Restore(Window window, WindowPlacement? placement)
    {
        if (placement is null) return;
        var dpi = VisualTreeHelper.GetDpi(window);
        var areas = new List<Rect>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr monitor, IntPtr context, ref NativeRect bounds, IntPtr data) =>
        {
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (GetMonitorInfo(monitor, ref info))
                areas.Add(new Rect(info.Work.Left / dpi.DpiScaleX, info.Work.Top / dpi.DpiScaleY,
                    (info.Work.Right - info.Work.Left) / dpi.DpiScaleX, (info.Work.Bottom - info.Work.Top) / dpi.DpiScaleY));
            return true;
        }, IntPtr.Zero);
        if (areas.Count == 0) areas.Add(SystemParameters.WorkArea);
        Rect restored = Fit(placement, areas, window.MinWidth, window.MinHeight);
        window.MinWidth = Math.Min(window.MinWidth, restored.Width);
        window.MinHeight = Math.Min(window.MinHeight, restored.Height);
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = restored.Left;
        window.Top = restored.Top;
        window.Width = restored.Width;
        window.Height = restored.Height;
        if (placement.Maximized) window.WindowState = WindowState.Maximized;
    }

    internal static Rect Fit(WindowPlacement placement, IReadOnlyList<Rect> areas, double minWidth, double minHeight)
    {
        var saved = new Rect(placement.Left, placement.Top, placement.Width, placement.Height);
        var area = areas.OrderByDescending(area =>
        {
            Rect intersection = Rect.Intersect(saved, area);
            return intersection.IsEmpty ? 0 : intersection.Width * intersection.Height;
        }).First();
        double width = Math.Clamp(saved.Width, Math.Min(minWidth, area.Width), area.Width);
        double height = Math.Clamp(saved.Height, Math.Min(minHeight, area.Height), area.Height);
        return new Rect(Math.Clamp(saved.Left, area.Left, area.Right - width), Math.Clamp(saved.Top, area.Top, area.Bottom - height), width, height);
    }

    internal static WindowPlacement Capture(Window window, bool maximized)
    {
        Rect bounds = window.WindowState == WindowState.Normal ? new Rect(window.Left, window.Top, window.Width, window.Height) : window.RestoreBounds;
        return new(bounds.Left, bounds.Top, bounds.Width, bounds.Height, maximized);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo { public int Size; public NativeRect Monitor, Work; public int Flags; }
    private delegate bool MonitorCallback(IntPtr monitor, IntPtr context, ref NativeRect bounds, IntPtr data);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayMonitors(IntPtr device, IntPtr clip, MonitorCallback callback, IntPtr data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
}
