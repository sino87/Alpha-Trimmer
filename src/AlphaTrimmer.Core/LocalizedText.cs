using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace AlphaTrimmer.Core;

public sealed class LocalizedText
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Catalogs = LoadCatalogs();
    private readonly CultureInfo culture;
    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> catalogs;
    public static IEnumerable<string> AvailableCultures => Catalogs.Keys.OrderBy(name => name == "en" ? "" : name, StringComparer.Ordinal);

    public LocalizedText(CultureInfo culture) : this(culture, Catalogs) { }

    internal LocalizedText(CultureInfo culture, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> catalogs)
    {
        this.culture = culture;
        this.catalogs = catalogs;
    }

    public string this[string key]
    {
        get
        {
            for (var candidate = culture; candidate.Name.Length > 0; candidate = candidate.Parent)
                if (catalogs.TryGetValue(candidate.Name, out var strings) && strings.TryGetValue(key, out string? value))
                    return value;
            return catalogs["en"][key];
        }
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> LoadCatalogs()
    {
        Assembly assembly = typeof(LocalizedText).Assembly;
        var catalogs = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        foreach (string name in assembly.GetManifestResourceNames().Where(name => name.StartsWith("AlphaTrimmer.Localization.", StringComparison.Ordinal)))
        {
            using var stream = assembly.GetManifestResourceStream(name)!;
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;
            catalogs.Add(root.GetProperty("culture").GetString()!, root.GetProperty("strings").EnumerateObject().ToDictionary(property => property.Name, property => property.Value.GetString()!, StringComparer.Ordinal));
        }
        return catalogs;
    }
}
