using System.Security.Cryptography;
using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;
using AlphaTrimmer.Core;
using AlphaTrimmer.App;
using ImageMagick;

string directory = Path.Combine(Path.GetTempPath(), "Alpha Trimmer tests 日本語 " + Guid.NewGuid().ToString("N"));
int passed = 0;
var trimmer = new TransparentTrimmer();
try
{
    Directory.CreateDirectory(directory);
    foreach (string fixture in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures")))
        File.Copy(fixture, Path.Combine(directory, Path.GetFileName(fixture)));

    CheckTrim("rgba.png", 3, 4, 3, 2);
    CheckTrim("rgba.webp", 3, 4, 3, 2);
    CheckTrim("lossy.webp", 3, 4, 3, 2);
    CheckTrim("rgb-transparency.png", 2, 3, 1, 1);
    CheckTrim("palette.png", 2, 3, 1, 1);
    CheckTrim("gray-alpha.png", 2, 3, 1, 1);
    CheckTrim("rgba16.png", 2, 3, 2, 1);
    CheckTrim("metadata.png", 3, 4, 3, 2);
    CheckTrim("metadata.webp", 3, 4, 3, 2);
    CheckStatus("transparent.png", TrimStatus.FullyTransparent);
    CheckStatus("transparent.webp", TrimStatus.FullyTransparent);
    CheckStatus("no-margin.png", TrimStatus.NoMargin);
    CheckStatus("opaque.png", TrimStatus.NoMargin);
    CheckStatus("animated.png", TrimStatus.AnimationUnsupported);
    CheckStatus("animated.webp", TrimStatus.AnimationUnsupported);
    CheckStatus("broken.png", TrimStatus.Failed);
    CheckStatus("missing.png", TrimStatus.Failed);
    CheckStatus("image.jpg", TrimStatus.FormatUnsupported);
    File.Copy(Path.Combine(directory, "rgba.png"), Path.Combine(directory, "fake.webp"));
    CheckStatus("fake.webp", TrimStatus.FormatUnsupported);

    string source = Path.Combine(directory, "rgba.png");
    var second = trimmer.Trim(source);
    Assert(second.OutputPath == Path.Combine(directory, "rgba-Trimmed-2.png"), "連番");
    string occupied = Path.Combine(directory, "rgba-Trimmed-3.png");
    Directory.CreateDirectory(occupied);
    Assert(trimmer.Trim(source).OutputPath == Path.Combine(directory, "rgba-Trimmed-4.png"), "同名フォルダー回避");
    var concurrent = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => trimmer.Trim(source))));
    Assert(concurrent.All(result => result.Status == TrimStatus.Saved) && concurrent.Select(result => result.OutputPath).Distinct().Count() == 8, "同時保存で上書きしない");
    Assert(!Directory.GetFiles(directory, ".alpha-trimmer-*.tmp").Any(), "一時ファイルの清掃");
    using (var locked = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.None))
        Assert(trimmer.Trim(source).Status == TrimStatus.Failed, "使用中ファイル");

    foreach (string name in new[] { "metadata.png", "metadata.webp" })
    {
        using var original = new MagickImage(Path.Combine(directory, name));
        using var output = new MagickImage(Path.Combine(directory, Path.GetFileNameWithoutExtension(name) + "-Trimmed-1" + Path.GetExtension(name)));
        foreach (string profile in original.ProfileNames)
        {
            if (profile is "exif" or "xmp")
                continue;
            Assert(original.GetProfile(profile)!.ToByteArray().SequenceEqual(output.GetProfile(profile)!.ToByteArray()), name + " " + profile + "保持");
        }
        Assert(output.GetExifProfile()!.GetValue(ExifTag.Artist)!.Value == "Tatsuya", name + " 作者保持");
        Assert(output.GetExifProfile()!.GetValue(ExifTag.DateTimeOriginal)!.Value == "2025:04:09 12:34:56", name + " 撮影日時保持");
        Assert((uint)output.GetExifProfile()!.GetValue(ExifTag.PixelXDimension)!.Value == 3 && (uint)output.GetExifProfile()!.GetValue(ExifTag.PixelYDimension)!.Value == 2, name + " EXIFサイズ更新");
        using var xmpStream = new MemoryStream(output.GetXmpProfile()!.ToByteArray());
        var xmp = XDocument.Load(xmpStream);
        Assert(xmp.Descendants(XName.Get("creator", "http://purl.org/dc/elements/1.1/")).Single().Value == "Tatsuya", name + " XMP作者保持");
        Assert(xmp.Descendants().Attributes(XName.Get("ImageWidth", "http://ns.adobe.com/tiff/1.0/")).Single().Value == "3" && xmp.Descendants().Attributes(XName.Get("PixelYDimension", "http://ns.adobe.com/exif/1.0/")).Single().Value == "2", name + " XMPサイズ更新");
        if (name.EndsWith("png"))
        {
            Assert(output.GetAttribute("Description") == "透明画像のテスト", "PNGテキスト保持");
            Assert(original.Density.X == output.Density.X && original.Density.Y == output.Density.Y, "PNG解像度保持");
        }
    }
    byte[] png16 = File.ReadAllBytes(Path.Combine(directory, "rgba16-Trimmed-1.png"));
    Assert(png16[24] == 16, "16ビットPNG保持");
    string importRoot = Path.Combine(directory, "folder");
    Directory.CreateDirectory(Path.Combine(importRoot, "sub"));
    File.Copy(Path.Combine(directory, "rgba.png"), Path.Combine(importRoot, "image.png"));
    File.Copy(Path.Combine(directory, "rgba.webp"), Path.Combine(importRoot, "sub", "image.webp"));
    File.Copy(Path.Combine(directory, "rgba.png"), Path.Combine(importRoot, "image-Trimmed-1.png"));
    File.WriteAllText(Path.Combine(importRoot, "image.jpg"), "unsupported");
    var selection = ImageSelection.Discover([importRoot], false, true);
    Assert(selection.Images.Count == 1 && selection.Images[0].RelativeDirectory == "folder", "フォルダー直下のみ追加・処理済み名の除外");
    selection = ImageSelection.Discover([importRoot, Path.Combine(importRoot, "image.png")], true, true);
    Assert(selection.Images.Count == 2 && selection.Images.Any(image => image.RelativeDirectory == Path.Combine("folder", "sub")), "再帰追加・階層保持・重複排除");
    Assert(ImageSelection.Discover([Path.Combine(importRoot, "image-Trimmed-1.png")], false, true).Images.Count == 1, "個別追加は処理済み名も受け入れる");
    Assert(ImageSelection.Discover([importRoot], true, false).Images.Count == 3, "処理済み名の除外解除");
    Assert(ImageSelection.Discover([Path.Combine(importRoot, "missing")], false, true).Issues.Count == 1, "追加できないパスを報告");
    bool cancelled = false;
    try { ImageSelection.Discover([importRoot], true, false, new CancellationToken(true)); }
    catch (OperationCanceledException) { cancelled = true; }
    Assert(cancelled, "画像追加の中止");
    string outputRoot = Path.Combine(directory, "new-output");
    var redirected = trimmer.Trim(Path.Combine(importRoot, "image.png"), outputRoot);
    Assert(redirected.Status == TrimStatus.Saved && redirected.OutputPath == Path.Combine(outputRoot, "image-Trimmed-1.png"), "別フォルダーへの保存と作成");
    string skippedRoot = Path.Combine(directory, "skip-output");
    Assert(trimmer.Trim(Path.Combine(directory, "transparent.png"), skippedRoot).Status == TrimStatus.FullyTransparent && !Directory.Exists(skippedRoot), "スキップ時に出力フォルダーを作らない");
    string preferencesPath = Path.Combine(directory, "settings", "settings.ini");
    var preferences = new AppPreferences { Language = "ja", Theme = "Dark", SaveBesideSource = false, OutputDirectory = outputRoot, IncludeSubfolders = true, ExcludeProcessedNames = false };
    preferences.Save(preferencesPath);
    Assert(AppPreferences.Load(preferencesPath) == preferences, "全設定を保存・復元");
    Assert(File.ReadAllBytes(preferencesPath).Take(2).SequenceEqual(new byte[] { 255, 254 }), "シェル拡張と共有するUTF-16設定ファイル");
    var positionedPreferences = preferences with { WindowPlacement = new WindowPlacement(-1600.25, 80.5, 1120.75, 760.25, true) };
    positionedPreferences.Save(preferencesPath);
    Assert(AppPreferences.Load(preferencesPath) == positionedPreferences, "ウィンドウ位置・サイズ・最大化状態を保存・復元");
    File.WriteAllText(preferencesPath, "[Preferences]\nLanguage=en\nWindowLeft=NaN\nWindowTop=0\nWindowWidth=1120\nWindowHeight=760\n", System.Text.Encoding.Unicode);
    Assert(AppPreferences.Load(preferencesPath).WindowPlacement is null && AppPreferences.Load(preferencesPath).Language == "en", "不正なウィンドウ位置を無視して他設定を保持");
    Assert(preferences.ResolveCulture(CultureInfo.GetCultureInfo("en-US")).Name == "ja", "手動言語選択がシステムより優先");
    Assert((preferences with { Language = "" }).ResolveCulture(CultureInfo.GetCultureInfo("en-US")).Name == "en-US", "システム言語への復帰");
    File.WriteAllText(preferencesPath, "[Preferences]\nLanguage=invalid\nTheme=invalid\n", System.Text.Encoding.Unicode);
    Assert(AppPreferences.Load(preferencesPath).Language == "" && AppPreferences.Load(preferencesPath).Theme == "System", "不明な設定はシステムへ戻す");
    Assert(AppPreferences.Load(Path.Combine(directory, "no-settings.ini")) == new AppPreferences(), "設定ファイルなしの初期値");
    var originalCulture = CultureInfo.CurrentUICulture;
    var partialCatalogs = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = new Dictionary<string, string> { ["App.Close"] = "Close", ["App.ResultsTitle"] = "Results", ["App.CannotStart"] = "Could not start" },
        ["fr"] = new Dictionary<string, string> { ["App.Close"] = "Fermer" },
        ["fr-CA"] = new Dictionary<string, string> { ["App.ResultsTitle"] = "Résultats" }
    };
    var partialText = new LocalizedText(CultureInfo.GetCultureInfo("fr-CA"), partialCatalogs);
    Assert(partialText["App.ResultsTitle"] == "Résultats", "地域固有の翻訳を優先");
    Assert(partialText["App.Close"] == "Fermer", "親言語へフォールバック");
    Assert(partialText["App.CannotStart"] == "Could not start", "未翻訳の項目は英語へフォールバック");
    Assert(new LocalizedText(CultureInfo.InvariantCulture)["App.Close"] == "Close", "言語未指定は英語");
    try
    {
        foreach (string language in new[] { "ja-JP", "en-US", "" })
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
            var text = new UiText(CultureInfo.CurrentUICulture);
            bool japanese = language == "ja-JP";
            Assert(text.Close == (japanese ? "閉じる" : "Close"), language + " 表示言語と英語へのフォールバック");
            var catalog = new LocalizedText(CultureInfo.CurrentUICulture);
            var notices = new List<string> { catalog["App.CannotStart"], catalog["App.InvalidSelectionPath"], text.ResultsTitle };
            foreach (string fixture in new[] { "no-margin.png", "transparent.png", "animated.webp", "image.jpg", "broken.png" })
            {
                var result = trimmer.Trim(Path.Combine(directory, fixture));
                notices.Add(text.Reason(result));
            }
            Assert(notices.All(value => !string.IsNullOrWhiteSpace(value) && (japanese ? value.Any(c => c > 127) : value.All(c => c <= 127 || c == '—'))), language + " 実処理結果と案内の翻訳");
            string truncated = Path.Combine(directory, "truncated.png");
            File.WriteAllBytes(truncated, new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 100, 73, 68, 65, 84 });
            var failure = trimmer.Trim(truncated);
            Assert(failure.Status == TrimStatus.Failed && failure.Error == (japanese ? "画像ファイル内のデータが途中で途切れています。" : "The image file contains incomplete data."), language + " 画像読み取りエラーの翻訳");
        }
    }
    finally
    {
        CultureInfo.CurrentUICulture = originalCulture;
    }
    Console.WriteLine(JsonSerializer.Serialize(new { passed, result = "PASS" }));
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception);
    Environment.ExitCode = 1;
}
finally
{
    try { if (Environment.ExitCode == 0 && Directory.Exists(directory)) Directory.Delete(directory, true); }
    catch (Exception exception)
    {
        Console.Error.WriteLine(exception);
        Environment.ExitCode = 1;
    }
}

void Assert(bool condition, string label)
{
    if (!condition)
        throw new InvalidOperationException(label);
    passed++;
    Console.WriteLine("PASS " + label);
}

void CheckStatus(string name, TrimStatus expected)
{
    int count = Directory.GetFiles(directory).Length;
    var result = trimmer.Trim(Path.Combine(directory, name));
    Assert(result.Status == expected, name + " " + result.Status + " " + result.Error);
    Assert(Directory.GetFiles(directory).Length == count, name + " 保存しない");
}

void CheckTrim(string name, int x, int y, uint width, uint height)
{
    string source = Path.Combine(directory, name);
    byte[] hash = SHA256.HashData(File.ReadAllBytes(source));
    var result = trimmer.Trim(source);
    Assert(result.Status == TrimStatus.Saved, name + " 保存 " + result.Error);
    Assert(hash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(source))), name + " 元画像保持");
    using var original = new MagickImage(source);
    using var output = new MagickImage(result.OutputPath!);
    Assert(output.Width == width && output.Height == height, name + " 領域");
    original.Crop(new MagickGeometry(x, y, width, height));
    original.ResetPage();
    original.Depth = 16;
    output.Depth = 16;
    Assert(original.ToByteArray(MagickFormat.Rgba).SequenceEqual(output.ToByteArray(MagickFormat.Rgba)), name + " 全ピクセル一致");
}
