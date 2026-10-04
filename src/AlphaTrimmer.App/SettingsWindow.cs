using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using System.Windows.Media;
using Wpf.Ui.Appearance;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using AlphaTrimmer.Core;

namespace AlphaTrimmer.App;

internal sealed class SettingsWindow : Window
{
    private readonly System.Windows.Controls.ComboBox language = new();
    private readonly System.Windows.Controls.ComboBox theme = new();
    private readonly System.Windows.Controls.TextBlock languageLabel = new();
    private readonly System.Windows.Controls.TextBlock themeLabel = new();
    private readonly Action<AppPreferences>? preferencesChanged;
    private bool initialized;
    internal AppPreferences Preferences { get; private set; }

    internal SettingsWindow(AppPreferences preferences, Action<AppPreferences>? preferencesChanged = null)
    {
        Preferences = preferences;
        this.preferencesChanged = preferencesChanged;
        Width = 440;
        Height = 260;
        ResizeMode = ResizeMode.NoResize;
        WindowStyle = WindowStyle.SingleBorderWindow;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SetResourceReference(BackgroundProperty, "ApplicationBackgroundBrush");
        SetResourceReference(ForegroundProperty, "TextFillColorPrimaryBrush");
        var panel = new StackPanel { Margin = new Thickness(24) };
        languageLabel.Margin = new Thickness(0, 0, 0, 8);
        themeLabel.Margin = new Thickness(0, 18, 0, 8);
        language.DisplayMemberPath = theme.DisplayMemberPath = "Label";
        language.SelectedValuePath = theme.SelectedValuePath = "Value";
        panel.Children.Add(languageLabel);
        panel.Children.Add(language);
        panel.Children.Add(themeLabel);
        panel.Children.Add(theme);
        Content = panel;
        Relabel();
        language.SelectionChanged += OnChanged;
        theme.SelectionChanged += OnChanged;
        Loaded += (_, _) =>
        {
            ApplicationThemeManager.Changed += OnThemeChanged;
            ThemeService.Apply(Preferences.Theme, this);
            RestoreNativeCaption();
        };
        Closing += (_, _) =>
        {
            ApplicationThemeManager.Changed -= OnThemeChanged;
            if (new WindowInteropHelper(this).Handle != IntPtr.Zero) Wpf.Ui.Appearance.SystemThemeWatcher.UnWatch(this);
        };
    }

    private void Relabel()
    {
        initialized = false;
        var text = new LocalizedText(Preferences.ResolveCulture(Program.SystemCulture));
        Title = text["Gui.Settings"];
        languageLabel.Text = text["Gui.Language"];
        themeLabel.Text = text["Gui.Theme"];
        language.ItemsSource = new[] { new Choice("", text["Gui.System"]) }.Concat(LocalizedText.AvailableCultures.Select(culture => new Choice(culture, CultureInfo.GetCultureInfo(culture).NativeName))).ToArray();
        theme.ItemsSource = new[] { new Choice("System", text["Gui.System"]), new Choice("Light", text["Gui.Light"]), new Choice("Dark", text["Gui.Dark"]) };
        language.SelectedValue = Preferences.Language;
        theme.SelectedValue = Preferences.Theme;
        initialized = true;
    }

    private void OnChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!initialized) return;
        Preferences = Preferences with { Language = language.SelectedValue as string ?? "", Theme = theme.SelectedValue as string ?? "System" };
        preferencesChanged?.Invoke(Preferences);
        Relabel();
        ThemeService.Apply(Preferences.Theme, this);
        RestoreNativeCaption();
    }

    private void OnThemeChanged(ApplicationTheme theme, Color accent) =>
        Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(RestoreNativeCaption));

    private void RestoreNativeCaption()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero) return;
        var options = new NonClientThemeOptions { Mask = 1 };
        Marshal.ThrowExceptionForHR(SetWindowThemeAttribute(handle, 1, ref options, (uint)Marshal.SizeOf<NonClientThemeOptions>()));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NonClientThemeOptions
    {
        public uint Flags;
        public uint Mask;
    }

    [DllImport("uxtheme.dll")]
    private static extern int SetWindowThemeAttribute(IntPtr window, uint attribute, ref NonClientThemeOptions options, uint size);

    private sealed record Choice(string Value, string Label);
}
