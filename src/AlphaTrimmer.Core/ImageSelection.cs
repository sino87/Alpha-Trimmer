using System.Text.RegularExpressions;

namespace AlphaTrimmer.Core;

public sealed record SelectedImage(string SourcePath, string RelativeDirectory);
public sealed record SelectionIssue(string SourcePath, string Reason);
public sealed record ImageSelection(IReadOnlyList<SelectedImage> Images, IReadOnlyList<SelectionIssue> Issues)
{
    private static readonly Regex ProcessedName = new(@"-Trimmed-[0-9]+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static ImageSelection Discover(IEnumerable<string> paths, bool includeSubfolders, bool excludeProcessedNames, CancellationToken cancellationToken = default)
    {
        var images = new List<SelectedImage>();
        var issues = new List<SelectionIssue>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                string fullPath = Path.GetFullPath(path);
                if (Directory.Exists(fullPath))
                {
                    string rootName = new DirectoryInfo(fullPath).Name.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Replace(":", "");
                    Visit(fullPath, fullPath, rootName);
                }
                else if (File.Exists(fullPath)) Add(fullPath, "", false);
                else issues.Add(new(path, new LocalizedText(System.Globalization.CultureInfo.CurrentUICulture)["Error.MissingPath"]));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
                issues.Add(new(path, exception.Message));
            }
        }
        return new(images, issues);

        void Add(string file, string relativeDirectory, bool fromFolder)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string extension = Path.GetExtension(file);
            if (!extension.Equals(".png", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".webp", StringComparison.OrdinalIgnoreCase)) return;
            if (fromFolder && excludeProcessedNames && ProcessedName.IsMatch(Path.GetFileNameWithoutExtension(file))) return;
            if (seen.Add(file)) images.Add(new(file, relativeDirectory));
        }

        void Visit(string directory, string root, string rootName)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                string relative = Path.GetRelativePath(root, directory);
                string destination = relative == "." ? rootName : Path.Combine(rootName, relative);
                foreach (string file in Directory.EnumerateFiles(directory)) Add(file, destination, true);
                if (!includeSubfolders) return;
                foreach (string child in Directory.EnumerateDirectories(directory))
                    if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0) Visit(child, root, rootName);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                issues.Add(new(directory, exception.Message));
            }
        }
    }
}
