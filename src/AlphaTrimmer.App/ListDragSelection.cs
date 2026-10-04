using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace AlphaTrimmer.App;

internal sealed class ListDragSelection
{
    private readonly DataGrid list;
    private readonly Canvas overlay;
    private readonly UIElement area;
    private readonly Rectangle rectangle;
    private readonly DispatcherTimer timer;
    private Point start;
    private Point pointer;
    private double startContentY;
    private object[] previousSelection = [];
    private bool pending;
    private bool dragging;

    internal ListDragSelection(DataGrid list, Canvas overlay)
    {
        this.list = list;
        this.overlay = overlay;
        area = (UIElement)overlay.Parent;
        rectangle = new Rectangle
        {
            Stroke = SystemColors.HighlightBrush,
            Fill = new SolidColorBrush(Color.FromArgb(40, SystemColors.HighlightColor.R, SystemColors.HighlightColor.G, SystemColors.HighlightColor.B)),
            StrokeThickness = 1,
            Visibility = Visibility.Collapsed
        };
        overlay.Children.Add(rectangle);
        timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
        timer.Tick += (_, _) => ScrollAtPointer();
        area.PreviewMouseLeftButtonDown += (_, e) => Begin(e.GetPosition(overlay), Keyboard.Modifiers);
        area.PreviewMouseMove += (_, e) =>
        {
            if (e.LeftButton != MouseButtonState.Pressed) { if (dragging) End(); return; }
            Move(e.GetPosition(overlay));
            if (dragging) e.Handled = true;
        };
        area.PreviewMouseLeftButtonUp += (_, e) =>
        {
            bool handled = dragging;
            End();
            if (handled) e.Handled = true;
        };
        area.LostMouseCapture += (_, _) => { if (!area.IsMouseCaptured) End(); };
        list.Unloaded += (_, _) => End();
    }

    internal bool Begin(Point position, ModifierKeys modifiers)
    {
        End();
        if (list.Items.Count == 0 || modifiers.HasFlag(ModifierKeys.Shift)) return false;
        var hit = list.InputHitTest(overlay.TranslatePoint(position, list)) as DependencyObject;
        for (var element = hit; element is not null; element = element is Visual ? VisualTreeHelper.GetParent(element) : (element as FrameworkContentElement)?.Parent)
            if (element is DataGridColumnHeader or ScrollBar or Thumb) return false;
        if (!TryContentGeometry(out double top, out _)) return false;
        start = pointer = position;
        startContentY = position.Y - top;
        previousSelection = modifiers.HasFlag(ModifierKeys.Control) ? list.SelectedItems.Cast<object>().ToArray() : [];
        pending = true;
        return true;
    }

    internal void Move(Point position)
    {
        if (!pending) return;
        pointer = position;
        if (!dragging)
        {
            if (Math.Abs(pointer.X - start.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(pointer.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            area.CaptureMouse();
            dragging = true;
            rectangle.Visibility = Visibility.Visible;
            timer.Start();
        }
        UpdateSelection();
    }

    internal void End()
    {
        pending = dragging = false;
        timer.Stop();
        rectangle.Visibility = Visibility.Collapsed;
        if (area.IsMouseCaptured) area.ReleaseMouseCapture();
        previousSelection = [];
    }

    internal void ScrollAtPointer()
    {
        if (!dragging) return;
        var viewer = Descendants<ScrollViewer>(list).FirstOrDefault();
        if (viewer is null) return;
        double top = viewer.TranslatePoint(new Point(0, 0), overlay).Y + list.ColumnHeaderHeight;
        if (double.IsNaN(top)) top = 40;
        double bottom = Math.Min(overlay.ActualHeight, viewer.TranslatePoint(new Point(0, viewer.ActualHeight), overlay).Y) - 18;
        double delta = pointer.Y < top + 16 ? -20 : pointer.Y > bottom - 16 ? 20 : 0;
        if (delta == 0) return;
        viewer.ScrollToVerticalOffset(viewer.VerticalOffset + delta);
        list.UpdateLayout();
        UpdateSelection();
    }

    private void UpdateSelection()
    {
        if (!TryContentGeometry(out double top, out double rowExtent)) return;
        double endContentY = pointer.Y - top;
        int first = Math.Max(0, (int)Math.Floor(Math.Min(startContentY, endContentY) / rowExtent));
        int last = Math.Min(list.Items.Count - 1, (int)Math.Ceiling(Math.Max(startContentY, endContentY) / rowExtent) - 1);
        var selected = previousSelection.ToHashSet();
        for (int index = first; index <= last; index++) selected.Add(list.Items[index]);
        var current = list.SelectedItems.Cast<object>().ToHashSet();
        foreach (var item in current.Except(selected)) list.SelectedItems.Remove(item);
        foreach (var item in selected.Except(current)) list.SelectedItems.Add(item);
        var bounds = new Rect(new Point(start.X, startContentY + top), pointer);
        Canvas.SetLeft(rectangle, bounds.Left);
        Canvas.SetTop(rectangle, bounds.Top);
        rectangle.Width = bounds.Width;
        rectangle.Height = bounds.Height;
    }

    private bool TryContentGeometry(out double top, out double rowExtent)
    {
        var rows = Descendants<DataGridRow>(list).Where(row => row.IsVisible && list.Items.IndexOf(row.Item) >= 0)
            .OrderBy(row => list.Items.IndexOf(row.Item)).Take(2).ToArray();
        top = 0;
        rowExtent = list.RowHeight;
        if (rows.Length == 0) return false;
        var first = rows[0];
        int firstIndex = list.Items.IndexOf(first.Item);
        double firstTop = first.TranslatePoint(new Point(0, 0), overlay).Y;
        rowExtent = first.ActualHeight + first.Margin.Top + first.Margin.Bottom;
        if (rows.Length > 1)
            rowExtent = (rows[1].TranslatePoint(new Point(0, 0), overlay).Y - firstTop) / (list.Items.IndexOf(rows[1].Item) - firstIndex);
        top = firstTop - firstIndex * rowExtent;
        return rowExtent > 0;
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject element) where T : DependencyObject
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(element); index++)
        {
            var child = VisualTreeHelper.GetChild(element, index);
            if (child is T match) yield return match;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }
}
