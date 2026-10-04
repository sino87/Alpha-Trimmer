using System.Globalization;
using System.Text;
using System.Windows;
using AlphaTrimmer.Core;

namespace AlphaTrimmer.App;

internal static class Program
{
    internal static CultureInfo SystemCulture { get; private set; } = CultureInfo.CurrentUICulture;
    [STAThread]
    private static int Main(string[] args)
    {
        SystemCulture = CultureInfo.CurrentUICulture;
        var preferences = AppPreferences.Load();
        CultureInfo.CurrentUICulture = preferences.ResolveCulture(CultureInfo.CurrentUICulture);
        var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        ThemeService.Initialize();
        ThemeService.Apply(preferences.Theme);
        var text = new LocalizedText(CultureInfo.CurrentUICulture);
        try
        {
            if (args.Length == 0)
            {
                application.ShutdownMode = ShutdownMode.OnMainWindowClose;
                return application.Run(new MainWindow(preferences));
            }
            string[] paths = args;
            if (args.Length == 2 && args[0] == "--selection")
            {
                string selection = Path.GetFullPath(args[1]);
                string selectionRoot = Path.Combine(Path.GetTempPath(), "AlphaTrimmer", "Selections") + Path.DirectorySeparatorChar;
                if (!selection.StartsWith(selectionRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException(text["App.InvalidSelectionPath"]);
                try { paths = File.ReadAllLines(selection, Encoding.Unicode); }
                finally { File.Delete(selection); }
            }
            var trimmer = new TransparentTrimmer();
            var results = Task.Run(() => paths.Select(path => trimmer.Trim(path)).ToArray()).GetAwaiter().GetResult();
            var skipped = results.Where(result => result.Status != TrimStatus.Saved).ToArray();
            if (skipped.Length > 0) new ResultsWindow(skipped, preferences.Theme).ShowDialog();
            return results.Any(result => result.Status == TrimStatus.Failed) ? 1 : 0;
        }
        catch (Exception exception)
        {
            System.Windows.MessageBox.Show($"{text["App.CannotStart"]}\n{exception.Message}", "Alpha Trimmer", MessageBoxButton.OK, MessageBoxImage.Error);
            return 1;
        }
    }
}
