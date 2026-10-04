using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AlphaTrimmer.App;
using AlphaTrimmer.Core;

internal static class Program
{
    private static int passed;
    [STAThread]
    private static int Main(string[] args)
    {
        string artifacts = args.Length > 0 ? Path.GetFullPath(args[0]) : Path.Combine(Environment.CurrentDirectory, "artifacts", "gui-tests");
        Directory.CreateDirectory(artifacts);
        string root = Path.Combine(artifacts, "run-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        ThemeService.Initialize();
        Exception? failure = null;
        application.DispatcherUnhandledException += (_, e) =>
        {
            failure = e.Exception;
            Console.Error.WriteLine(e.Exception);
            e.Handled = true;
            application.Shutdown(1);
        };
        application.Dispatcher.InvokeAsync(async () =>
        {
            MainWindow? window = null;
            try
            {
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
                var preferences = new AppPreferences { Language = "en", Theme = "Light", IncludeSubfolders = true, SaveBesideSource = false, OutputDirectory = Path.Combine(root, "output") };
                string settings = Path.Combine(root, "preferences.ini");
                window = new MainWindow(preferences, settings) { WindowStartupLocation = WindowStartupLocation.Manual, Left = -20000, Top = -20000, ShowActivated = false };
                window.Show();
                await Task.Delay(150);
                Assert(FindTitleBar(window) is not null, "visible draggable title bar with window controls");
                var titleBar = FindTitleBar(window)!;
                var captionPoint = titleBar.PointToScreen(new Point(200, 16));
                var packedPoint = new IntPtr(((int)captionPoint.X & 0xffff) | (((int)captionPoint.Y & 0xffff) << 16));
                Assert(SendMessage(new System.Windows.Interop.WindowInteropHelper(window).Handle, 0x0084, IntPtr.Zero, packedPoint).ToInt64() == 2, "Windows recognizes the title bar as a draggable caption");
                InvokeCaption(titleBar, "Maximize");
                Assert(window.WindowState == WindowState.Maximized, "title bar maximize button maximizes the window");
                var maximizedPlacement = WindowPlacementService.Capture(window, true);
                Assert(maximizedPlacement.Maximized && maximizedPlacement.Width == window.RestoreBounds.Width && maximizedPlacement.Height == window.RestoreBounds.Height, "maximized capture stores normal restore bounds");
                InvokeCaption(titleBar, "Restore");
                Assert(window.WindowState == WindowState.Normal, "title bar restore button restores the window");
                InvokeCaption(titleBar, "Minimize");
                Assert(window.WindowState == WindowState.Minimized, "title bar minimize button minimizes the window");
                window.WindowState = WindowState.Normal;
                window.UpdatePreferences(preferences with { SaveBesideSource = true, IncludeSubfolders = false });
                await Task.Delay(100);
                Capture(window, Path.Combine(artifacts, "english-light-empty.png"));
                window.UpdatePreferences(preferences with { Language = "ja", Theme = "Dark", SaveBesideSource = true, IncludeSubfolders = false });
                await Task.Delay(100);
                Capture(window, Path.Combine(artifacts, "japanese-dark-empty.png"));
                window.UpdatePreferences(preferences);
                string filters = Path.Combine(root, "filters");
                Directory.CreateDirectory(Path.Combine(filters, "sub"));
                Copy("rgba.png", Path.Combine(filters, "original.png"));
                Copy("rgba.png", Path.Combine(filters, "original-Trimmed-1.png"));
                Copy("rgba.webp", Path.Combine(filters, "sub", "nested.webp"));
                window.UpdatePreferences(preferences with { IncludeSubfolders = false });
                await window.AddPathsAsync([filters]);
                Assert(window.Items.Count == 1, "initial folder filters exclude subfolders and trimmed filenames");
                window.RecursiveCheck.IsChecked = true;
                while (window.IsBusy) await Task.Delay(10);
                Assert(window.Items.Count == 2, "enabling subfolders updates an already added folder");
                window.ExcludeCheck.IsChecked = false;
                while (window.IsBusy) await Task.Delay(10);
                Assert(window.Items.Count == 3, "disabling filename exclusion updates an already added folder");
                window.ImageList.SelectedItem = window.Items.Single(item => item.Name == "original.png");
                window.RemoveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                window.ExcludeCheck.IsChecked = true;
                while (window.IsBusy) await Task.Delay(10);
                Assert(window.Items.Count == 1 && window.Items[0].Name == "nested.webp", "filter changes remove pending excluded rows without restoring manually removed rows");
                await window.AddPathsAsync([Path.Combine(filters, "original-Trimmed-1.png")]);
                window.RecursiveCheck.IsChecked = false;
                while (window.IsBusy) await Task.Delay(10);
                Assert(window.Items.Count == 1 && window.Items[0].Name == "original-Trimmed-1.png", "explicit files bypass folder filename exclusion after filters change");
                window.RecursiveCheck.IsChecked = true;
                while (window.IsBusy) await Task.Delay(10);
                await window.RunAsync();
                window.RecursiveCheck.IsChecked = false;
                while (window.IsBusy) await Task.Delay(10);
                Assert(window.Items.Count == 2 && window.Items.All(item => item.Result?.Status == TrimStatus.Saved), "changing filters preserves completed results");
                window.ClearButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                window.RecursiveCheck.IsChecked = true;
                Assert(window.Items.Count == 0 && !window.IsBusy, "clearing the list forgets imported folders");
                window.UpdatePreferences(preferences);
                string source = Path.Combine(root, "写真A");
                Directory.CreateDirectory(Path.Combine(source, "sub"));
                Copy("rgba.png", Path.Combine(source, "one.png"));
                Copy("rgba.webp", Path.Combine(source, "sub", "two.webp"));
                Copy("animated.webp", Path.Combine(source, "animated.webp"));
                Copy("transparent.png", Path.Combine(source, "transparent.png"));
                Copy("no-margin.png", Path.Combine(source, "no-margin.png"));
                Copy("broken.png", Path.Combine(source, "broken.png"));
                Copy("rgba.png", Path.Combine(source, "one-Trimmed-1.png"));
                File.WriteAllText(Path.Combine(source, "ignore.jpg"), "unsupported");
                await window.AddPathsAsync([source, Path.Combine(source, "one.png")]);
                Assert(window.Items.Count == 6, "recursive import, extension filtering, processed-name exclusion and duplicates");
                window.UpdateLayout();
                var firstRow = (DataGridRow)window.ImageList.ItemContainerGenerator.ContainerFromIndex(0);
                Point firstRowPoint = firstRow.TranslatePoint(new Point(20, 0), window.SelectionOverlay);
                var secondRow = (DataGridRow)window.ImageList.ItemContainerGenerator.ContainerFromIndex(1);
                double rowExtent = secondRow.TranslatePoint(new Point(0, 0), window.SelectionOverlay).Y - firstRowPoint.Y;
                Point blankStart = new Point(700, firstRowPoint.Y + window.Items.Count * rowExtent + 15);
                Assert(window.DragSelection.Begin(blankStart, System.Windows.Input.ModifierKeys.None), "rubber-band selection begins in list whitespace");
                window.DragSelection.Move(new Point(20, firstRowPoint.Y + 2 * rowExtent + 5));
                Assert(window.ImageList.SelectedItems.Count == 4 && window.ImageList.SelectedItems.Contains(window.Items[2]) && !window.ImageList.SelectedItems.Contains(window.Items[1]), "upward rubber-band selects intersecting rows");
                Capture(window, Path.Combine(artifacts, "english-light-drag-selection.png"));
                window.DragSelection.Move(new Point(20, firstRowPoint.Y + 4 * rowExtent + 5));
                Assert(window.ImageList.SelectedItems.Count == 2, "shrinking the drag rectangle removes rows outside it");
                window.DragSelection.End();
                Assert(!window.ListArea.IsMouseCaptured && window.SelectionOverlay.Children.OfType<System.Windows.Shapes.Rectangle>().Single().Visibility == Visibility.Collapsed, "ending drag releases capture and hides the rectangle");
                window.ImageList.SelectedItems.Clear();
                window.ImageList.SelectedItems.Add(window.Items[5]);
                window.DragSelection.Begin(new Point(20, firstRowPoint.Y + 5), System.Windows.Input.ModifierKeys.Control);
                window.DragSelection.Move(new Point(700, firstRowPoint.Y + rowExtent + 25));
                Assert(window.ImageList.SelectedItems.Count == 3 && window.ImageList.SelectedItems.Contains(window.Items[5]), "Ctrl drag preserves previous selection and adds intersecting rows");
                window.DragSelection.End();
                var header = Descendants<System.Windows.Controls.Primitives.DataGridColumnHeader>(window.ImageList).First(header => header.IsVisible && header.ActualWidth > 0);
                Assert(!window.DragSelection.Begin(header.TranslatePoint(new Point(10, 10), window.SelectionOverlay), System.Windows.Input.ModifierKeys.None), "column headers do not start rubber-band selection");
                window.ImageList.SelectedItems.Clear();
                window.ImageList.SelectedItem = window.Items.Single(item => item.Name == "one.png");
                window.RemoveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert(window.Items.Count == 5, "remove selected through actual UI button");
                await window.AddPathsAsync([Path.Combine(source, "one.png")]);
                window.StartButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert(window.IsBusy && !window.AddControls.IsEnabled && !window.DestinationControls.IsEnabled && !window.SettingsButton.IsEnabled, "UI editing disabled while processing");
                while (window.IsBusy) await Task.Delay(10);
                Assert(window.Items.Count(item => item.Result?.Status == TrimStatus.Saved) == 2, "GUI saves both PNG and WebP");
                Assert(window.Items.Count(item => item.Result?.Status == TrimStatus.Failed) == 1 && window.Items.Count(item => item.Result?.Status == TrimStatus.AnimationUnsupported) == 1, "failure and animation appear in results");
                Assert(File.Exists(Path.Combine(preferences.OutputDirectory, "写真A", "sub", "two-Trimmed-1.webp")), "output preserves root folder and hierarchy");
                Assert(File.Exists(Path.Combine(preferences.OutputDirectory, "one-Trimmed-1.png")), "individually re-added file saves at output root");
                int savedCount = Directory.GetFiles(preferences.OutputDirectory, "*", SearchOption.AllDirectories).Length;
                await window.RunAsync();
                Assert(Directory.GetFiles(preferences.OutputDirectory, "*", SearchOption.AllDirectories).Length == savedCount, "start does not reprocess saved or skipped items");
                Copy("rgba.png", Path.Combine(source, "broken.png"));
                await window.RunAsync(true);
                Assert(window.Items.All(item => item.Result?.Status != TrimStatus.Failed), "retry only failed items after source repair");
                window.ImageList.SelectedItem = window.Items.First(item => item.Result?.Status == TrimStatus.Saved);
                Assert(window.OpenImageButton.IsEnabled && window.OpenFolderButton.IsEnabled, "saved image enables output actions");
                window.UpdatePreferences(preferences with { Language = "ja", Theme = "Dark" });
                Assert(window.StartButton.Content?.ToString() == "トリミング開始" && AppPreferences.Load(settings).Language == "ja", "language switches immediately and persists");
                Assert(window.ReasonColumn.Header?.ToString() == "詳細" && window.RemoveButton.Content?.ToString() == "選択をリストから削除", "Japanese labels explain result details and list removal");
                Assert(HintContent(window.ExcludeCheck.ToolTip).Contains("-Trimmed-1.png") && HintContent(window.RemoveButton.ToolTip).Contains("削除されません"), "Japanese tooltips explain filename matching and preservation of files");
                await Task.Delay(120);
                Capture(window, Path.Combine(artifacts, "japanese-dark-results.png"));
                var notices = new ResultsWindow(window.Items.Where(item => item.Result?.Status != TrimStatus.Saved).Select(item => item.Result!).ToArray(), "Dark")
                { WindowStartupLocation = WindowStartupLocation.Manual, Left = -20000, Top = -20000, ShowActivated = false };
                notices.Show();
                await Task.Delay(80);
                var noticeText = ((DockPanel)((DockPanel)notices.Content).Children.OfType<DockPanel>().Single()).Children.OfType<TextBox>().Single();
                Assert(notices.Title.Contains("処理結果") && noticeText.Text.Contains("アニメーション画像には対応していません") && noticeText.IsReadOnly, "shell results window displays localized reasons");
                Assert(noticeText.VerticalScrollBarVisibility == ScrollBarVisibility.Auto && noticeText.HorizontalScrollBarVisibility == ScrollBarVisibility.Auto, "shell result details support scrolling");
                Capture(notices, Path.Combine(artifacts, "japanese-dark-shell-results.png"));
                Assert(FindTitleBar(notices) is { ShowMinimize: true, ShowMaximize: true, ShowClose: true }, "results window includes window controls");
                InvokeCaption(FindTitleBar(notices)!, "Close");
                Assert(!notices.IsVisible && window.IsVisible, "results title bar closes only the results window");
                var settingsDialog = new SettingsWindow(preferences with { Language = "ja", Theme = "Dark" }, window.UpdatePreferences) { Owner = window, WindowStartupLocation = WindowStartupLocation.Manual, Left = -20000, Top = -20000, ShowActivated = false };
                settingsDialog.Show();
                await Task.Delay(80);
                Assert(settingsDialog.WindowStyle == WindowStyle.SingleBorderWindow && settingsDialog.ResizeMode == ResizeMode.NoResize, "settings uses a native fixed-size title bar");
                settingsDialog.Left = 100;
                settingsDialog.Top = 100;
                await Task.Delay(100);
                Assert(CaptionHasText(CaptureNative(settingsDialog, Path.Combine(artifacts, "settings-native-title.png"))), "native settings title is actually painted after opening");
                var settingsBoxes = Descendants<ComboBox>(settingsDialog).ToArray();
                settingsBoxes[0].SelectedValue = "en";
                await Task.Delay(80);
                Assert(CaptionHasText(CaptureNative(settingsDialog, Path.Combine(artifacts, "settings-native-title-english.png"))), "native settings title remains painted after changing language");
                Assert(AppPreferences.Load(settings).Language == "en" && window.StartButton.Content?.ToString() == "Start trimming" && settingsDialog.Title == "Settings", "settings language saves and updates both windows immediately");
                settingsBoxes[1].SelectedValue = "Light";
                Assert(AppPreferences.Load(settings).Theme == "Light", "settings theme saves without a save button");
                settingsBoxes[0].SelectedValue = "ja";
                settingsBoxes[1].SelectedValue = "Dark";
                Assert(!Descendants<Button>(settingsDialog).Any() && settingsDialog.ResizeMode == ResizeMode.NoResize && settingsDialog.Title == "設定", "settings is fixed size with a localized native title and no action buttons");
                var edge = settingsDialog.PointToScreen(new Point(settingsDialog.ActualWidth - 2, settingsDialog.ActualHeight - 2));
                var edgePoint = new IntPtr(((int)edge.X & 0xffff) | (((int)edge.Y & 0xffff) << 16));
                long edgeHit = SendMessage(new System.Windows.Interop.WindowInteropHelper(settingsDialog).Handle, 0x0084, IntPtr.Zero, edgePoint).ToInt64();
                Assert(edgeHit is < 10 or > 17, "Windows does not treat the settings border as a resize handle");
                var hint = (StackPanel)window.ExcludeCheck.ToolTip;
                Assert(HintContent(hint).StartsWith("・") && hint.Children.OfType<TextBlock>().SelectMany(block => block.Inlines.OfType<System.Windows.Documents.Run>()).Count(run => run.FontFamily.Source == "Consolas") == 2 && !HintContent(hint).Contains('`'), "tooltip uses bullet dots and inline-code styling without markup characters");
                Assert(hint.Children.OfType<TextBlock>().Take(hint.Children.Count - 1).All(block => block.Margin.Bottom == 8), "tooltip paragraphs have space between them");
                await Task.Delay(100);
                Assert(CaptionHasText(CaptureNative(settingsDialog, Path.Combine(artifacts, "settings-native-title-japanese.png"))), "native settings title remains painted after changing theme");
                typeof(Wpf.Ui.Controls.WindowBackdrop).Assembly.GetType("Wpf.Ui.Interop.UnsafeNativeMethods")!.GetMethod("RemoveWindowCaption", [typeof(IntPtr)])!.Invoke(null, [new System.Windows.Interop.WindowInteropHelper(settingsDialog).Handle]);
                Assert(!CaptionHasText(CaptureNative(settingsDialog, Path.Combine(artifacts, "settings-caption-negative-control.png"))), "caption capture detects the original missing-text failure");
                Wpf.Ui.Appearance.ApplicationThemeManager.Apply(Wpf.Ui.Appearance.ApplicationTheme.Light, Wpf.Ui.Controls.WindowBackdropType.None, true);
                await Task.Delay(100);
                Assert(CaptionHasText(CaptureNative(settingsDialog, Path.Combine(artifacts, "settings-native-title-theme-event.png"))), "theme events restore the native caption after library suppression");
                ThemeService.Apply("Dark", settingsDialog);
                await Task.Delay(100);
                hint.Measure(new Size(460, double.PositiveInfinity));
                hint.Arrange(new Rect(0, 0, hint.DesiredSize.Width, hint.DesiredSize.Height));
                var hintBitmap = new RenderTargetBitmap((int)Math.Ceiling(hint.ActualWidth), (int)Math.Ceiling(hint.ActualHeight), 96, 96, PixelFormats.Pbgra32);
                var hintBackground = new DrawingVisual();
                using (var drawing = hintBackground.RenderOpen()) drawing.DrawRectangle(window.Background, null, new Rect(0, 0, hint.ActualWidth, hint.ActualHeight));
                hintBitmap.Render(hintBackground);
                hintBitmap.Render(hint);
                var hintEncoder = new PngBitmapEncoder();
                hintEncoder.Frames.Add(BitmapFrame.Create(hintBitmap));
                using (var hintFile = File.Create(Path.Combine(artifacts, "japanese-inline-code-tooltip.png"))) hintEncoder.Save(hintFile);
                Capture(settingsDialog, Path.Combine(artifacts, "japanese-dark-settings.png"));
                SystemCommands.CloseWindow(settingsDialog);
                await Task.Delay(30);
                Assert(!settingsDialog.IsVisible && window.IsVisible, "settings title bar closes only settings");
                window.UpdatePreferences(preferences);
                await Task.Delay(120);
                Assert(Descendants<TextBlock>(window.ImageList).Any(block => block.Text == "Skipped") && Descendants<TextBlock>(window.ImageList).Any(block => block.Text.StartsWith("Skipped. Animated images")), "visible row bindings switch to English with the window");
                Capture(window, Path.Combine(artifacts, "english-light-results.png"));
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("ja-JP");
                string languageProbe = Path.Combine(root, "language-probe.png");
                File.WriteAllBytes(languageProbe, new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 100, 73, 68, 65, 84 });
                await window.AddPathsAsync([languageProbe]);
                var languageItem = window.Items.Single(item => item.SourcePath == languageProbe);
                Assert(languageItem.Status == "Pending", "new rows use the chosen language independently of caller culture");
                await window.RunAsync();
                Assert(languageItem.Status == "Failed" && languageItem.Reason.Contains("The image file contains incomplete data.") && window.ActivityLabel.Text == "Processing complete", "processing errors and later UI updates retain the chosen English language");
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
                window.ClearButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert(window.Items.Count == 0 && !window.StartButton.IsEnabled, "clear list through actual UI button");
                string scrollSource = Path.Combine(root, "scroll-selection");
                Directory.CreateDirectory(scrollSource);
                for (int index = 0; index < 40; index++) Copy("rgba.png", Path.Combine(scrollSource, $"image-{index:00}.png"));
                await window.AddPathsAsync(Directory.GetFiles(scrollSource));
                window.UpdateLayout();
                var scrollRow = (DataGridRow)window.ImageList.ItemContainerGenerator.ContainerFromIndex(0);
                var scrollStart = scrollRow.TranslatePoint(new Point(20, 10), window.SelectionOverlay);
                window.DragSelection.Begin(scrollStart, System.Windows.Input.ModifierKeys.None);
                window.DragSelection.Move(new Point(700, window.SelectionOverlay.ActualHeight - 5));
                int initialSelected = window.ImageList.SelectedItems.Count;
                for (int index = 0; index < 20; index++) window.DragSelection.ScrollAtPointer();
                var scrollViewer = Descendants<ScrollViewer>(window.ImageList).First();
                Assert(scrollViewer.VerticalOffset > 0 && window.ImageList.SelectedItems.Count > initialSelected, "edge drag scrolls and selects rows beyond the initial viewport");
                Assert(window.ImageList.SelectedItems.Contains(window.Items[0]), "virtualized scrolling retains the drag origin selection");
                window.DragSelection.End();
                window.ClearButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                for (int index = 0; index < 3; index++) Copy("rgba.png", Path.Combine(source, $"stop-{index}.png"));
                await window.AddPathsAsync(Enumerable.Range(0, 3).Select(index => Path.Combine(source, $"stop-{index}.png")));
                Task processing = window.RunAsync();
                window.RequestStop();
                await processing;
                Assert(window.Items.Count(item => item.Result?.Status == TrimStatus.Saved) == 1 && window.Items.Count(item => item.Result is null) == 2, "stop finishes current image and leaves pending items");
                await window.RunAsync();
                Assert(window.Items.All(item => item.Result?.Status == TrimStatus.Saved), "resume processes only pending items");
                window.ClearButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await window.AddPathsAsync(Enumerable.Range(0, 3).Select(index => Path.Combine(source, $"stop-{index}.png")));
                window.Width = 1240;
                window.Height = 800;
                window.Left = -19800;
                window.Top = -19900;
                processing = window.RunAsync();
                InvokeCaption(FindTitleBar(window)!, "Close");
                Assert(window.IsVisible, "close waits for current image");
                await processing;
                Assert(!window.IsVisible && window.Items.Count(item => item.Result is null) == 2, "close stops remaining images and preserves completed output");
                var storedPlacement = AppPreferences.Load(settings).WindowPlacement;
                Assert(storedPlacement is { Left: -19800, Top: -19900, Width: 1240, Height: 800, Maximized: false }, "closing saves normal window position and size after processing stops");
                var restored = new MainWindow(AppPreferences.Load(settings), settings);
                var desktop = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop, SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
                Assert(!Rect.Intersect(new Rect(restored.Left, restored.Top, restored.Width, restored.Height), desktop).IsEmpty, "reopening brings an offscreen saved window onto the current desktop");
                restored.Close();
                var maximizedRestore = new MainWindow(preferences with { WindowPlacement = new WindowPlacement(80, 90, 1120, 760, true) }, Path.Combine(root, "maximized.ini"));
                Assert(maximizedRestore.WindowState == WindowState.Maximized, "saved maximized state is restored on startup");
                maximizedRestore.WindowState = WindowState.Normal;
                maximizedRestore.Close();
                var fit = WindowPlacementService.Fit(new WindowPlacement(-1500, 50, 1120, 760), [new Rect(0, 0, 1920, 1040), new Rect(-1920, 0, 1920, 1040)], 1000, 650);
                Assert(fit.Left == -1500 && fit.Width == 1120, "valid saved position on a secondary monitor is retained");
                var small = WindowPlacementService.Fit(new WindowPlacement(3000, 3000, 1600, 1000), [new Rect(0, 0, 800, 600)], 1000, 650);
                Assert(small == new Rect(0, 0, 800, 600), "removed monitors and smaller work areas produce a fully visible window");
                Console.WriteLine($"{{\"passed\":{passed},\"result\":\"PASS\"}}");
            }
            catch (Exception exception) { failure = exception; Console.Error.WriteLine(exception); }
            finally { window?.Close(); application.Shutdown(); }
        });
        application.Run();
        if (failure is null) Directory.Delete(root, true);
        return failure is null ? 0 : 1;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    private static string HintContent(object? hint) => hint is Panel panel ? string.Join("\n", panel.Children.OfType<TextBlock>().Select(block => string.Concat(block.Inlines.OfType<System.Windows.Documents.Run>().Select(run => run.Text)))) : "";

    private static void Copy(string fixture, string destination) => File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", fixture), destination, true);
    private static void InvokeCaption(Wpf.Ui.Controls.TitleBar titleBar, string action)
    {
        var button = Descendants<Button>(titleBar).Single(button => button.CommandParameter?.ToString() == action);
        Assert(button.IsVisible && button.IsEnabled && button.Command?.CanExecute(button.CommandParameter) == true, $"{action} caption button is actionable");
        button.Command!.Execute(button.CommandParameter);
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
    private static Wpf.Ui.Controls.TitleBar? FindTitleBar(DependencyObject element)
    {
        if (element is Wpf.Ui.Controls.TitleBar titleBar && titleBar.IsVisible) return titleBar;
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(element); index++)
            if (FindTitleBar(VisualTreeHelper.GetChild(element, index)) is { } result) return result;
        return null;
    }
    private static void Assert(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
        passed++;
        Console.WriteLine("PASS: " + label);
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr GetWindowDC(IntPtr window);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr window, IntPtr dc);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool PrintWindow(IntPtr window, IntPtr dc, uint flags);
    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr dc, int width, int height);
    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr dc, IntPtr value);
    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr value);
    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr dc);

    private static bool CaptionHasText(BitmapSource source)
    {
        var image = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        int stride = image.PixelWidth * 4;
        var pixels = new byte[stride * image.PixelHeight];
        image.CopyPixels(pixels, stride, 0);
        double scale = image.PixelWidth / 440.0;
        int baseline = (int)(16 * scale) * stride + (int)(150 * scale) * 4;
        int contrastPixels = 0;
        for (int y = (int)(8 * scale); y < (int)(25 * scale); y++)
            for (int x = (int)(32 * scale); x < (int)(130 * scale); x++)
            {
                int offset = y * stride + x * 4;
                if (Enumerable.Range(0, 3).Any(channel => Math.Abs(pixels[offset + channel] - pixels[baseline + channel]) > 12)) contrastPixels++;
            }
        return contrastPixels > 10;
    }

    private static BitmapSource CaptureNative(Window window, string destination)
    {
        var handle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
        var transform = PresentationSource.FromVisual(window)!.CompositionTarget.TransformToDevice;
        int width = (int)Math.Ceiling(window.ActualWidth * transform.M11);
        int height = (int)Math.Ceiling(window.ActualHeight * transform.M22);
        var dc = GetWindowDC(handle);
        var memory = CreateCompatibleDC(dc);
        var bitmap = CreateCompatibleBitmap(dc, width, height);
        var previous = SelectObject(memory, bitmap);
        try
        {
            if (!PrintWindow(handle, memory, 2)) throw new InvalidOperationException("Window capture failed");
            var source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(bitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            using var output = File.Create(destination);
            encoder.Save(output);
            return source;
        }
        finally
        {
            SelectObject(memory, previous);
            DeleteObject(bitmap);
            DeleteDC(memory);
            ReleaseDC(handle, dc);
        }
    }

    private static void Capture(Window window, string destination)
    {
        window.UpdateLayout();
        var visual = (FrameworkElement)window.Content;
        double width = visual.ActualWidth + visual.Margin.Left + visual.Margin.Right;
        double height = visual.ActualHeight + visual.Margin.Top + visual.Margin.Bottom;
        var bitmap = new RenderTargetBitmap((int)width, (int)height, 96, 96, PixelFormats.Pbgra32);
        var background = new DrawingVisual();
        using (var drawing = background.RenderOpen()) drawing.DrawRectangle(window.Background, null, new Rect(0, 0, width, height));
        bitmap.Render(background);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = File.Create(destination);
        encoder.Save(output);
    }
}

