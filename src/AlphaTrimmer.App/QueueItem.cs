using System.ComponentModel;
using System.Globalization;
using AlphaTrimmer.Core;

namespace AlphaTrimmer.App;

internal sealed class QueueItem(SelectedImage image, CultureInfo displayCulture) : INotifyPropertyChanged
{
    private LocalizedText text = new(displayCulture);
    private UiText uiText = new(displayCulture);
    public SelectedImage Image { get; } = image;
    public string SourcePath => Image.SourcePath;
    public string Name => Path.GetFileName(SourcePath);
    public TrimResult? Result { get; private set; }
    public bool Processing { get; private set; }
    public string OutputPath => Result?.OutputPath ?? "";
    public string Status => text[Processing ? "Gui.Processing" : Result is null ? "Gui.Pending" : Result.Status == TrimStatus.Saved ? "Gui.Saved" : Result.Status == TrimStatus.Failed ? "Gui.Failed" : "Gui.Skipped"];
    public string Reason => Result is null || Result.Status == TrimStatus.Saved ? "" : uiText.Reason(Result);
    public event PropertyChangedEventHandler? PropertyChanged;
    internal void Begin() { Processing = true; Refresh(); }
    internal void Complete(TrimResult result) { Result = result; Processing = false; Refresh(); }
    internal void Refresh(CultureInfo? displayCulture = null)
    {
        if (displayCulture is not null)
        {
            text = new LocalizedText(displayCulture);
            uiText = new UiText(displayCulture);
        }
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }
}
