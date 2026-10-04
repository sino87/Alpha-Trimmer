using ImageMagick;
using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace AlphaTrimmer.Core;

public sealed class TransparentTrimmer
{
    public TrimResult Trim(string sourcePath, string? outputDirectory = null)
    {
        try
        {
            string fullPath = Path.GetFullPath(sourcePath);
            string extension = Path.GetExtension(fullPath).ToLowerInvariant();
            if (extension is not (".png" or ".webp"))
                return new(sourcePath, TrimStatus.FormatUnsupported);

            using var input = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            Span<byte> header = stackalloc byte[12];
            input.ReadExactly(header);
            bool png = extension == ".png";
            if (!(png ? ImageContainer.IsPng(header) : ImageContainer.IsWebP(header)))
                return new(sourcePath, TrimStatus.FormatUnsupported);
            if (ImageContainer.HasAnimation(input, png))
                return new(sourcePath, TrimStatus.AnimationUnsupported);

            using var image = new MagickImage(input, new MagickReadSettings { Format = png ? MagickFormat.Png : MagickFormat.WebP });
            if (!image.HasAlpha)
                return new(sourcePath, TrimStatus.NoMargin);
            PixelBounds? bounds = FindBounds(image);
            if (bounds is null)
                return new(sourcePath, TrimStatus.FullyTransparent);
            PixelBounds area = bounds.Value;
            if (area == new PixelBounds(0, 0, checked((int)image.Width), checked((int)image.Height)))
                return new(sourcePath, TrimStatus.NoMargin);

            image.Crop(new MagickGeometry(area.X, area.Y, (uint)area.Width, (uint)area.Height));
            image.ResetPage();
            UpdateDimensions(image);
            if (png)
                image.Settings.SetDefine(MagickFormat.Png, "include-chunk", "all");
            else
            {
                image.Settings.SetDefine(MagickFormat.WebP, "lossless", true);
                image.Settings.SetDefine(MagickFormat.WebP, "exact", true);
            }

            string directory = outputDirectory is null ? Path.GetDirectoryName(fullPath)! : Path.GetFullPath(outputDirectory);
            if (outputDirectory is not null)
                Directory.CreateDirectory(directory);
            string temporary = Path.Combine(directory, $".alpha-trimmer-{Guid.NewGuid():N}.tmp");
            try
            {
                using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    image.Write(output, png ? MagickFormat.Png : MagickFormat.WebP);
                    output.Flush(true);
                }
                string baseName = Path.GetFileNameWithoutExtension(fullPath);
                for (int number = 1; ; number = checked(number + 1))
                {
                    string destination = Path.Combine(directory, $"{baseName}-Trimmed-{number}{extension}");
                    try
                    {
                        File.Move(temporary, destination, false);
                        return new(sourcePath, TrimStatus.Saved, destination);
                    }
                    catch (IOException) when (File.Exists(destination) || Directory.Exists(destination))
                    {
                    }
                }
            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or MagickException or ArgumentException or OverflowException or XmlException)
        {
            return new(sourcePath, TrimStatus.Failed, Error: exception.Message);
        }
    }

    private static PixelBounds? FindBounds(MagickImage image)
    {
        using var mask = image.Clone();
        mask.Alpha(AlphaOption.Extract);
        mask.Threshold(new Percentage(0));
        mask.Depth = 8;
        byte[] pixels = mask.ToByteArray(MagickFormat.Gray);
        int width = checked((int)image.Width);
        int height = checked((int)image.Height);
        if (pixels.Length != checked(width * height))
            throw new InvalidDataException(FailureText.UnreadableAlpha);
        int left = width, top = height, right = -1, bottom = -1;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                if (pixels[y * width + x] != 0)
                {
                    left = Math.Min(left, x);
                    top = Math.Min(top, y);
                    right = Math.Max(right, x);
                    bottom = Math.Max(bottom, y);
                }
        return right < 0 ? null : new PixelBounds(left, top, right - left + 1, bottom - top + 1);
    }

    private static void UpdateDimensions(MagickImage image)
    {
        var exif = image.GetExifProfile();
        if (exif is not null)
        {
            if (exif.GetValue(ExifTag.PixelXDimension) is not null)
                exif.SetValue(ExifTag.PixelXDimension, new Number(image.Width));
            if (exif.GetValue(ExifTag.PixelYDimension) is not null)
                exif.SetValue(ExifTag.PixelYDimension, new Number(image.Height));
            if (exif.GetValue(ExifTag.ImageWidth) is not null)
                exif.SetValue(ExifTag.ImageWidth, new Number(image.Width));
            if (exif.GetValue(ExifTag.ImageLength) is not null)
                exif.SetValue(ExifTag.ImageLength, new Number(image.Height));
            image.SetProfile(exif);
        }
        var xmp = image.GetXmpProfile();
        if (xmp is null)
            return;
        using var stream = new MemoryStream(xmp.ToByteArray());
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, MaxCharactersInDocument = 10_000_000 });
        var document = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
        XNamespace tiffNamespace = "http://ns.adobe.com/tiff/1.0/";
        XNamespace exifNamespace = "http://ns.adobe.com/exif/1.0/";
        var dimensions = new Dictionary<XName, uint>
        {
            [tiffNamespace + "ImageWidth"] = image.Width,
            [tiffNamespace + "ImageLength"] = image.Height,
            [exifNamespace + "PixelXDimension"] = image.Width,
            [exifNamespace + "PixelYDimension"] = image.Height
        };
        bool changed = false;
        foreach (var element in document.Descendants())
        {
            if (dimensions.TryGetValue(element.Name, out uint value))
            {
                element.Value = value.ToString(CultureInfo.InvariantCulture);
                changed = true;
            }
            foreach (var attribute in element.Attributes())
                if (dimensions.TryGetValue(attribute.Name, out value))
                {
                    attribute.Value = value.ToString(CultureInfo.InvariantCulture);
                    changed = true;
                }
        }
        if (changed)
            image.SetProfile(new XmpProfile(document));
    }
}
