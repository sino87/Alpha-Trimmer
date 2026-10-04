using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace AlphaTrimmer.App;

internal static class HintText
{
    internal static StackPanel Create(string text)
    {
        var panel = new StackPanel { MaxWidth = 460 };
        var paragraphs = text.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);
        for (int index = 0; index < paragraphs.Length; index++)
        {
            var paragraph = CreateParagraph(paragraphs[index]);
            paragraph.Margin = new Thickness(0, 0, 0, index < paragraphs.Length - 1 ? 8 : 0);
            panel.Children.Add(paragraph);
        }
        return panel;
    }

    private static TextBlock CreateParagraph(string text)
    {
        var block = new TextBlock { TextWrapping = TextWrapping.Wrap, MaxWidth = 460 };
        block.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
        int position = 0;
        foreach (Match match in Regex.Matches(text, "`([^`\\r\\n]+)`"))
        {
            block.Inlines.Add(new Run(text[position..match.Index]));
            var code = new Run(match.Groups[1].Value) { FontFamily = new FontFamily("Consolas") };
            code.SetResourceReference(TextElement.BackgroundProperty, "ControlFillColorSecondaryBrush");
            block.Inlines.Add(code);
            position = match.Index + match.Length;
        }
        block.Inlines.Add(new Run(text[position..]));
        return block;
    }
}
