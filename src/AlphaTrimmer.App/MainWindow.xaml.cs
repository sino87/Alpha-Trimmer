using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using AlphaTrimmer.Core;
using Microsoft.Win32;
using Wpf.Ui.Controls;

namespace AlphaTrimmer.App;

public partial class MainWindow : FluentWindow
{
    private readonly List<string> importedPaths = [];
    private readonly HashSet<string> removedPaths = new(StringComparer.OrdinalIgnoreCase);
    private AppPreferences preferences;
    private readonly string? settingsPath;
    private readonly CultureInfo systemCulture;
    private WindowState lastWindowState = WindowState.Normal;
    private bool initialized;
    private bool busy;
    private bool stopRequested;
    private bool closingRequested;
    private int completed;
    private int total;
    private string activity = "Gui.Ready";
    private CancellationTokenSource? discoveryCancellation;
    internal ObservableCollection<QueueItem> Items { get; } = [];
    internal bool IsBusy => busy;
    internal ListDragSelection DragSelection { get; }
    private CultureInfo DisplayCulture => preferences.ResolveCulture(systemCulture);
    private LocalizedText Text;

    public MainWindow(AppPreferences preferences) : this(preferences, null) { }

    internal MainWindow(AppPreferences preferences, string? settingsPath)
    {
        this.preferences = preferences;
        this.settingsPath = settingsPath;
        systemCulture = Program.SystemCulture;
        Text = new LocalizedText(DisplayCulture);
        InitializeComponent();
        ImageList.ItemsSource = Items;
        DragSelection = new ListDragSelection(ImageList, SelectionOverlay);
        RecursiveCheck.IsChecked = preferences.IncludeSubfolders;
        ExcludeCheck.IsChecked = preferences.ExcludeProcessedNames;
        BesideCheck.IsChecked = preferences.SaveBesideSource;
        initialized = true;
        Loaded += (_, _) => ThemeService.Apply(this.preferences.Theme, this);
        Closing += OnClosing;
        StateChanged += (_, _) => { if (WindowState != WindowState.Minimized) lastWindowState = WindowState; };
        WindowPlacementService.Restore(this, preferences.WindowPlacement);
        Relabel();
    }

    private void Relabel()
    {
        AddFilesButton.Content = Text["Gui.AddFiles"];
        AddFolderButton.Content = Text["Gui.AddFolder"];
        RecursiveCheck.Content = Text["Gui.Recursive"];
        ExcludeCheck.Content = Text["Gui.Exclude"];
        RecursiveCheck.ToolTip = HintText.Create(Text["Gui.RecursiveHint"]);
        ExcludeCheck.ToolTip = HintText.Create(Text["Gui.ExcludeHint"]);
        SettingsButton.Content = Text["Gui.Settings"];
        BesideCheck.Content = Text["Gui.Beside"];
        BrowseButton.Content = Text["Gui.Browse"];
        NameColumn.Header = Text["Gui.Name"];
        StatusColumn.Header = Text["Gui.Status"];
        ReasonColumn.Header = Text["Gui.Reason"];
        SourceColumn.Header = Text["Gui.Source"];
        OutputColumn.Header = Text["Gui.Output"];
        EmptyTitle.Text = Text["Gui.EmptyTitle"];
        EmptyHint.Text = Text["Gui.EmptyHint"];
        RemoveButton.Content = Text["Gui.Remove"];
        ClearButton.Content = Text["Gui.Clear"];
        RemoveButton.ToolTip = HintText.Create(Text["Gui.RemoveHint"]);
        ClearButton.ToolTip = HintText.Create(Text["Gui.ClearHint"]);
        OpenImageButton.Content = Text["Gui.OpenImage"];
        OpenFolderButton.Content = Text["Gui.OpenFolder"];
        RetryButton.Content = Text["Gui.Retry"];
        StopButton.Content = Text["Gui.Stop"];
        StartButton.Content = Text["Gui.Start"];
        foreach (var item in Items) item.Refresh(DisplayCulture);
        RefreshControls();
    }

    private void RefreshControls()
    {
        AddControls.IsEnabled = DestinationControls.IsEnabled = EditControls.IsEnabled = SettingsButton.IsEnabled = !busy;
        BrowseButton.IsEnabled = !preferences.SaveBesideSource;
        bool destinationValid = preferences.SaveBesideSource || !string.IsNullOrWhiteSpace(preferences.OutputDirectory);
        StartButton.IsEnabled = !busy && destinationValid && Items.Any(item => item.Result is null);
        RetryButton.IsEnabled = !busy && destinationValid && Items.Any(item => item.Result?.Status == TrimStatus.Failed);
        StopButton.IsEnabled = busy && !stopRequested;
        RemoveButton.IsEnabled = !busy && ImageList.SelectedItems.Count > 0;
        ClearButton.IsEnabled = !busy && Items.Count > 0;
        bool saved = ImageList.SelectedItem is QueueItem { Result.Status: TrimStatus.Saved };
        OpenImageButton.IsEnabled = OpenFolderButton.IsEnabled = saved;
        EmptyPanel.Visibility = Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        DestinationLabel.Text = preferences.SaveBesideSource ? Text["Gui.BesideHint"] : preferences.OutputDirectory.Length == 0 ? Text["Gui.ChooseDestination"] : preferences.OutputDirectory;
        DestinationLabel.ToolTip = DestinationLabel.Text;
        SummaryLabel.Text = string.Format(DisplayCulture, Text["Gui.Summary"], Items.Count,
            Items.Count(item => item.Result?.Status == TrimStatus.Saved), Items.Count(item => item.Result is not null && item.Result.Status is not (TrimStatus.Saved or TrimStatus.Failed)),
            Items.Count(item => item.Result?.Status == TrimStatus.Failed), Items.Count(item => item.Result is null));
        Progress.Maximum = Math.Max(1, total);
        Progress.Value = completed;
        ActivityLabel.Text = busy && total > 0 ? $"{Text[activity]}  {completed} / {total}" : Text[activity];
    }

    internal async Task AddPathsAsync(IEnumerable<string> paths)
    {
        if (busy) return;
        await DiscoverAsync(paths.ToArray(), false);
    }

    private async Task DiscoverAsync(string[] paths, bool refresh)
    {
        busy = true;
        stopRequested = false;
        discoveryCancellation = new CancellationTokenSource();
        activity = "Gui.Importing";
        total = completed = 0;
        RefreshControls();
        try
        {
            var culture = DisplayCulture;
            var selection = await Task.Run(() =>
            {
                CultureInfo.CurrentUICulture = culture;
                return ImageSelection.Discover(paths, preferences.IncludeSubfolders, preferences.ExcludeProcessedNames, discoveryCancellation.Token);
            });
            if (refresh)
            {
                var included = selection.Images.Select(image => image.SourcePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var item in Items.Where(item => item.Result is null && !included.Contains(item.SourcePath)).ToArray()) Items.Remove(item);
            }
            else
            {
                foreach (string path in paths)
                {
                    string fullPath = Path.GetFullPath(path);
                    if (!importedPaths.Contains(fullPath, StringComparer.OrdinalIgnoreCase)) importedPaths.Add(fullPath);
                }
                foreach (var image in selection.Images) removedPaths.Remove(image.SourcePath);
            }
            var existing = Items.Select(item => item.SourcePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var image in selection.Images)
                if (!removedPaths.Contains(image.SourcePath) && existing.Add(image.SourcePath)) Items.Add(new QueueItem(image, DisplayCulture));
            if (selection.Issues.Count > 0)
                new ResultsWindow(selection.Issues.Select(issue => new TrimResult(issue.SourcePath, TrimStatus.Failed, Error: issue.Reason)).ToArray(), preferences.Theme, DisplayCulture) { Owner = this }.ShowDialog();
        }
        catch (OperationCanceledException) { }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException) { ShowError(exception); }
        finally
        {
            busy = false;
            discoveryCancellation.Dispose();
            discoveryCancellation = null;
            activity = stopRequested ? "Gui.Stopped" : "Gui.Ready";
            RefreshControls();
            if (closingRequested) Close();
        }
    }

    internal async Task RunAsync(bool retry = false)
    {
        if (busy) return;
        var pending = Items.Where(item => retry ? item.Result?.Status == TrimStatus.Failed : item.Result is null).ToArray();
        if (pending.Length == 0 || (!preferences.SaveBesideSource && string.IsNullOrWhiteSpace(preferences.OutputDirectory))) return;
        busy = true;
        stopRequested = false;
        total = pending.Length;
        completed = 0;
        activity = "Gui.Processing";
        RefreshControls();
        try
        {
            var trimmer = new TransparentTrimmer();
            foreach (var item in pending)
            {
                if (stopRequested) break;
                item.Begin();
                string? output = preferences.SaveBesideSource ? null : Path.Combine(preferences.OutputDirectory, item.Image.RelativeDirectory);
                var culture = DisplayCulture;
                var result = await Task.Run(() =>
                {
                    CultureInfo.CurrentUICulture = culture;
                    return trimmer.Trim(item.SourcePath, output);
                });
                item.Complete(result);
                completed++;
                RefreshControls();
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        { ShowError(exception); }
        finally
        {
            busy = false;
            activity = stopRequested ? "Gui.Stopped" : "Gui.Finished";
            RefreshControls();
            if (closingRequested) Close();
        }
    }

    internal void RequestStop() { stopRequested = true; discoveryCancellation?.Cancel(); activity = closingRequested ? "Gui.Closing" : "Gui.Stopping"; RefreshControls(); }

    private async void OnAddFiles(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Multiselect = true, Filter = "PNG / WebP|*.png;*.webp" };
        if (dialog.ShowDialog(this) == true) await AddPathsAsync(dialog.FileNames);
    }

    private async void OnAddFolder(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Multiselect = true };
        if (dialog.ShowDialog(this) == true) await AddPathsAsync(dialog.FolderNames);
    }

    private void OnBrowseDestination(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog();
        if (dialog.ShowDialog(this) != true) return;
        preferences = preferences with { OutputDirectory = dialog.FolderName };
        SavePreferences();
        RefreshControls();
    }

    private async void OnPreferencesChanged(object sender, RoutedEventArgs e)
    {
        if (!initialized || busy) return;
        bool filtersChanged = preferences.IncludeSubfolders != (RecursiveCheck.IsChecked == true) || preferences.ExcludeProcessedNames != (ExcludeCheck.IsChecked == true);
        preferences = preferences with { IncludeSubfolders = RecursiveCheck.IsChecked == true, ExcludeProcessedNames = ExcludeCheck.IsChecked == true, SaveBesideSource = BesideCheck.IsChecked == true };
        SavePreferences();
        RefreshControls();
        if (filtersChanged && importedPaths.Count > 0) await DiscoverAsync(importedPaths.ToArray(), true);
    }

    private void OnSettings(object sender, RoutedEventArgs e)
    {
        new SettingsWindow(preferences, UpdatePreferences) { Owner = this }.ShowDialog();
    }

    internal void UpdatePreferences(AppPreferences value)
    {
        if (busy) return;
        preferences = value;
        Text = new LocalizedText(DisplayCulture);
        initialized = false;
        RecursiveCheck.IsChecked = preferences.IncludeSubfolders;
        ExcludeCheck.IsChecked = preferences.ExcludeProcessedNames;
        BesideCheck.IsChecked = preferences.SaveBesideSource;
        initialized = true;
        SavePreferences();
        CultureInfo.CurrentUICulture = preferences.ResolveCulture(systemCulture);
        ThemeService.Apply(preferences.Theme, this);
        Relabel();
    }

    private void SavePreferences()
    {
        try { preferences.Save(settingsPath); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException) { ShowError(exception); }
    }
    private void ShowError(Exception exception) => System.Windows.MessageBox.Show(this, exception.Message, Text["Gui.Failed"], System.Windows.MessageBoxButton.OK, MessageBoxImage.Error);
    private async void OnStart(object sender, RoutedEventArgs e) => await RunAsync();
    private async void OnRetry(object sender, RoutedEventArgs e) => await RunAsync(true);
    private void OnStop(object sender, RoutedEventArgs e) => RequestStop();
    private void OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) { if (initialized) RefreshControls(); }
    private void OnRemove(object sender, RoutedEventArgs e) { foreach (var item in ImageList.SelectedItems.Cast<QueueItem>().ToArray()) { removedPaths.Add(item.SourcePath); Items.Remove(item); } RefreshControls(); }
    private void OnClear(object sender, RoutedEventArgs e) { Items.Clear(); importedPaths.Clear(); removedPaths.Clear(); total = completed = 0; activity = "Gui.Ready"; RefreshControls(); }
    private void OnDragOver(object sender, DragEventArgs e) { e.Effects = !busy && e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; }
    private async void OnDrop(object sender, DragEventArgs e) { if (!busy && e.Data.GetData(DataFormats.FileDrop) is string[] paths) await AddPathsAsync(paths); e.Handled = true; }
    private void OnOpenImage(object sender, RoutedEventArgs e) { if (ImageList.SelectedItem is QueueItem item) Open(item.OutputPath); }
    private void OnOpenFolder(object sender, RoutedEventArgs e) { if (ImageList.SelectedItem is QueueItem item) Open(Path.GetDirectoryName(item.OutputPath)!); }
    private void Open(string path)
    {
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception exception) when (exception is Win32Exception or IOException or InvalidOperationException) { ShowError(exception); }
    }
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (busy)
        {
            e.Cancel = true;
            closingRequested = true;
            RequestStop();
            return;
        }
        preferences = preferences with { WindowPlacement = WindowPlacementService.Capture(this, lastWindowState == WindowState.Maximized) };
        SavePreferences();
    }
}
