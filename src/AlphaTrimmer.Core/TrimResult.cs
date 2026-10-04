namespace AlphaTrimmer.Core;

public enum TrimStatus
{
    Saved,
    NoMargin,
    FullyTransparent,
    AnimationUnsupported,
    FormatUnsupported,
    Failed
}

public sealed record TrimResult(string SourcePath, TrimStatus Status, string? OutputPath = null, string? Error = null);

public readonly record struct PixelBounds(int X, int Y, int Width, int Height);
