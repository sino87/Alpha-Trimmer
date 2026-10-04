using System.Windows;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Wpf.Ui.Markup;

namespace AlphaTrimmer.App;

internal static class ThemeService
{
    internal static void Initialize()
    {
        Application.Current.Resources.MergedDictionaries.Add(new ThemesDictionary());
        Application.Current.Resources.MergedDictionaries.Add(new ControlsDictionary());
    }

    internal static void Apply(string theme, Window? window = null)
    {
        if (window is not null) SystemThemeWatcher.UnWatch(window);
        ApplicationTheme selected = theme switch
        {
            "Light" => ApplicationTheme.Light,
            "Dark" => ApplicationTheme.Dark,
            _ => ApplicationThemeManager.GetSystemTheme() is SystemTheme.Dark ? ApplicationTheme.Dark : ApplicationTheme.Light
        };
        ApplicationThemeManager.Apply(selected, WindowBackdropType.None, true);
        if (theme == "System" && window is not null) SystemThemeWatcher.Watch(window, WindowBackdropType.None, true);
    }
}
