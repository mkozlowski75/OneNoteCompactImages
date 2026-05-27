using System.Drawing;
using System.Drawing.Imaging;
using OneNoteCompact.Core.Models;
using OneNoteCompact.Core.Services;

var failures = new List<string>();

Run("NoUpscaleKeepsSize", NoUpscaleKeepsSize);
Run("TargetSizeLowersBytes", TargetSizeLowersBytes);
Run("PngAlphaCanStayPng", PngAlphaCanStayPng);

if (failures.Count > 0)
{
    Console.Error.WriteLine("Tests failed:");
    foreach (var failure in failures)
    {
        Console.Error.WriteLine(" - " + failure);
    }

    return 1;
}

Console.WriteLine("All tests passed.");
return 0;

void Run(string name, Action test)
{
    try
    {
        test();
        Console.WriteLine($"[PASS] {name}");
    }
    catch (Exception ex)
    {
        failures.Add($"{name}: {ex.Message}");
    }
}

void NoUpscaleKeepsSize()
{
    var bytes = CreateJpeg(400, 300, 90);
    var service = new ImageCompressionService();
    var options = new CompactOptions { MaxWidth = 1920, MaxHeight = 1920, NoUpscale = true, MinBytesToProcess = 0, SkipSmallImages = false };

    var result = service.TryCompress(bytes, options);
    if (result is null)
    {
        throw new InvalidOperationException("Expected compressed result.");
    }

    if (result.Value.width != 400 || result.Value.height != 300)
    {
        throw new InvalidOperationException("Image should not upscale.");
    }
}

void TargetSizeLowersBytes()
{
    var bytes = CreateJpeg(2400, 1600, 95);
    var service = new ImageCompressionService();
    var options = new CompactOptions
    {
        Mode = CompressionMode.TargetSize,
        TargetKb = 150,
        MaxWidth = 1600,
        MaxHeight = 1600,
        MinQuality = 40,
        MaxQuality = 90,
        MinBytesToProcess = 0,
        SkipSmallImages = false
    };

    var result = service.TryCompress(bytes, options);
    if (result is null)
    {
        throw new InvalidOperationException("Expected result for target-size mode.");
    }

    if (result.Value.imageBytes.Length >= bytes.Length)
    {
        throw new InvalidOperationException("Expected reduced output size.");
    }
}

void PngAlphaCanStayPng()
{
    var bytes = CreatePngWithAlpha(800, 600);
    var service = new ImageCompressionService();
    var options = new CompactOptions
    {
        KeepPngAlpha = true,
        MaxWidth = 400,
        MaxHeight = 400,
        MinBytesToProcess = 0,
        SkipSmallImages = false
    };

    var result = service.TryCompress(bytes, options);
    if (result is null)
    {
        throw new InvalidOperationException("Expected alpha-preserving result.");
    }

    if (!string.Equals(result.Value.format, "png", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException("Expected PNG output for alpha image.");
    }
}

byte[] CreateJpeg(int width, int height, int quality)
{
    using var bmp = new Bitmap(width, height);
    using (var g = Graphics.FromImage(bmp))
    {
        g.Clear(Color.OrangeRed);
        g.FillEllipse(Brushes.DarkBlue, 20, 20, width - 40, height - 40);
    }

    var codec = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
    using var ms = new MemoryStream();
    using var parameters = new EncoderParameters(1);
    parameters.Param[0] = new EncoderParameter(Encoder.Quality, quality);
    bmp.Save(ms, codec, parameters);
    return ms.ToArray();
}

byte[] CreatePngWithAlpha(int width, int height)
{
    using var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
    using (var g = Graphics.FromImage(bmp))
    {
        g.Clear(Color.Transparent);
        g.FillRectangle(new SolidBrush(Color.FromArgb(128, Color.Green)), 20, 20, width - 40, height - 40);
    }

    using var ms = new MemoryStream();
    bmp.Save(ms, ImageFormat.Png);
    return ms.ToArray();
}
