using System.Globalization;
using AlphaTrimmer.Core;

namespace AlphaTrimmer.App;

internal sealed class UiText(CultureInfo culture)
{
    private readonly LocalizedText text = new(culture);

    internal string ResultsTitle => text["App.ResultsTitle"];
    internal string Close => text["App.Close"];

    internal string Reason(TrimResult result) => result.Status switch
    {
        TrimStatus.NoMargin => text["Result.NoMargin"],
        TrimStatus.FullyTransparent => text["Result.FullyTransparent"],
        TrimStatus.AnimationUnsupported => text["Result.AnimationUnsupported"],
        TrimStatus.FormatUnsupported => text["Result.FormatUnsupported"],
        _ => $"{text["Result.Failed"]}\n{result.Error}"
    };
}
