using System.Globalization;
using System.Text;

namespace AlphaTrimmer.Core;

public sealed record WindowPlacement(double Left, double Top, double Width, double Height, bool Maximized = false);

public sealed record AppPreferences
{
    public bool SaveBesideSource { get; init; } = true;
    public string OutputDirectory { get; init; } = "";
    public bool IncludeSubfolders { get; init; }
    public bool ExcludeProcessedNames { get; init; } = true;
    public string Language { get; init; } = "";
    public string Theme { get; init; } = "System";
    public WindowPlacement? WindowPlacement { get; init; }
    public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AlphaTrimmer", "settings.ini");

    public CultureInfo ResolveCulture(CultureInfo systemCulture) => Language.Length == 0 ? systemCulture : CultureInfo.GetCultureInfo(Language);

    public static AppPreferences Load(string? path = null)
    {
        try
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            bool preferencesSection = false;
            foreach (string line in File.ReadAllLines(path ?? DefaultPath, Encoding.Unicode))
            {
                if (line.StartsWith('['))
                {
                    preferencesSection = line.Trim().Equals("[Preferences]", StringComparison.OrdinalIgnoreCase);
                    continue;
                }
                int separator = line.IndexOf('=');
                if (preferencesSection && separator > 0)
                    values[line[..separator].Trim()] = line[(separator + 1)..];
            }
            string Read(string key, string fallback) => values.GetValueOrDefault(key, fallback);
            string language = Read("Language", "");
            double? Number(string key) => double.TryParse(Read(key, ""), NumberStyles.Float, CultureInfo.InvariantCulture, out double value) && double.IsFinite(value) ? value : null;
            WindowPlacement? placement = Number("WindowLeft") is double left && Number("WindowTop") is double top &&
                Number("WindowWidth") is double width && width > 0 && Number("WindowHeight") is double height && height > 0
                ? new(left, top, width, height, Read("WindowMaximized", "false") == "true") : null;
            return new()
            {
                SaveBesideSource = Read("SaveBesideSource", "true") != "false",
                OutputDirectory = Read("OutputDirectory", ""),
                IncludeSubfolders = Read("IncludeSubfolders", "false") == "true",
                ExcludeProcessedNames = Read("ExcludeProcessedNames", "true") != "false",
                Language = LocalizedText.AvailableCultures.Contains(language, StringComparer.OrdinalIgnoreCase) ? language : "",
                Theme = Read("Theme", "System") is "Light" or "Dark" ? Read("Theme", "System") : "System",
                WindowPlacement = placement
            };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new();
        }
    }

    public void Save(string? path = null)
    {
        string destination = Path.GetFullPath(path ?? DefaultPath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            string[] lines = ["[Preferences]", $"Language={Language}", $"Theme={Theme}", $"SaveBesideSource={SaveBesideSource.ToString().ToLowerInvariant()}",
                $"OutputDirectory={OutputDirectory}", $"IncludeSubfolders={IncludeSubfolders.ToString().ToLowerInvariant()}", $"ExcludeProcessedNames={ExcludeProcessedNames.ToString().ToLowerInvariant()}"];
            if (WindowPlacement is { } placement)
                lines = [.. lines, $"WindowLeft={placement.Left.ToString(CultureInfo.InvariantCulture)}", $"WindowTop={placement.Top.ToString(CultureInfo.InvariantCulture)}",
                    $"WindowWidth={placement.Width.ToString(CultureInfo.InvariantCulture)}", $"WindowHeight={placement.Height.ToString(CultureInfo.InvariantCulture)}", $"WindowMaximized={placement.Maximized.ToString().ToLowerInvariant()}"];
            if (lines.Any(line => line.Contains('\r') || line.Contains('\n')))
                throw new ArgumentException("Settings values must not contain line breaks.");
            File.WriteAllLines(temporary, lines, Encoding.Unicode);
            File.Move(temporary, destination, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
