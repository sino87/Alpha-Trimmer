using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using AlphaTrimmer.Core;
using Wpf.Ui.Controls;

namespace AlphaTrimmer.App;

internal sealed class ResultsWindow : FluentWindow
{
    internal ResultsWindow(TrimResult[] results, string? theme = null, CultureInfo? culture = null)
    {
        var text = new UiText(culture ?? CultureInfo.CurrentUICulture);
        Title = text.ResultsTitle;
        Width = 780;
        Height = 460;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        SetResourceReference(BackgroundProperty, "ApplicationBackgroundBrush");
        SetResourceReference(ForegroundProperty, "TextFillColorPrimaryBrush");
        var panel = new DockPanel { Margin = new Thickness(20) };
        var close = new Wpf.Ui.Controls.Button { Content = text.Close, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0), IsCancel = true, IsDefault = true };
        close.Click += (_, _) => Close();
        DockPanel.SetDock(close, Dock.Bottom);
        panel.Children.Add(close);
        panel.Children.Add(new System.Windows.Controls.TextBox
        {
            IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            Text = string.Join(Environment.NewLine + Environment.NewLine, results.Select(result => $"{result.SourcePath}{Environment.NewLine}{text.Reason(result)}"))
        });
        var layout = new DockPanel();
        var titleBar = new TitleBar { Title = Title, ShowMinimize = true, ShowMaximize = true, ShowClose = true };
        DockPanel.SetDock(titleBar, Dock.Top);
        layout.Children.Add(titleBar);
        layout.Children.Add(panel);
        Content = layout;
        Loaded += (_, _) => ThemeService.Apply(theme ?? AppPreferences.Load().Theme, this);
    }
}
