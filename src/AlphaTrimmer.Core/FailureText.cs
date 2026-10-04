using System.Globalization;

namespace AlphaTrimmer.Core;

internal static class FailureText
{
    internal static string TruncatedChunk => new LocalizedText(CultureInfo.CurrentUICulture)["Error.TruncatedChunk"];
    internal static string UnreadableAlpha => new LocalizedText(CultureInfo.CurrentUICulture)["Error.UnreadableAlpha"];
}
