using System.Drawing;
using System.Drawing.Imaging;
using System.Xml.Linq;
using OneNoteCompact.Core.Models;

namespace OneNoteCompact.Core.Services;

public sealed class ImageCompressionService
{
    public (byte[] imageBytes, int width, int height, string format)? TryCompress(byte[] originalBytes, CompactOptions options)
    {
        if (options.SkipSmallImages && originalBytes.Length < options.MinBytesToProcess)
        {
            return null;
        }

        using var sourceStream = new MemoryStream(originalBytes);
        using var image = Image.FromStream(sourceStream, useEmbeddedColorManagement: true, validateImageData: true);

        var (newWidth, newHeight) = CalculateSize(image.Width, image.Height, options);
        var hasAlpha = Image.IsAlphaPixelFormat(image.PixelFormat);
        var originalFormat = GetFormatName(image.RawFormat);

        using var resized = new Bitmap(newWidth, newHeight);
        using (var g = Graphics.FromImage(resized))
        {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.DrawImage(image, 0, 0, newWidth, newHeight);
        }

        if (hasAlpha && options.KeepPngAlpha)
        {
            using var pngStream = new MemoryStream();
            resized.Save(pngStream, ImageFormat.Png);
            var pngBytes = pngStream.ToArray();

            if (pngBytes.Length >= originalBytes.Length)
            {
                return null;
            }

            return (pngBytes, newWidth, newHeight, "png");
        }

        var compressedJpeg = CompressToJpeg(resized, originalBytes.Length, options);
        if (compressedJpeg is null)
        {
            return null;
        }

        if (compressedJpeg.Value.imageBytes.Length >= originalBytes.Length && newWidth == image.Width && newHeight == image.Height)
        {
            return null;
        }

        return compressedJpeg;
    }

    public bool TryRewritePageXml(IOneNoteGateway gateway, string pageId, string pageXml, CompactOptions options, out string updatedXml, out PageReport report)
    {
        updatedXml = pageXml;
        var doc = XDocument.Parse(pageXml);
        var ns = doc.Root?.Name.Namespace ?? XNamespace.None;
        var pageName = doc.Root?.Attribute("name")?.Value ?? pageId;

        report = new PageReport
        {
            PageId = pageId,
            PageName = pageName,
            SectionId = doc.Root?.Attribute("ID")?.Value
        };

        var imageElements = doc.Descendants(ns + "Image")
            .Select(i => new
            {
                Node = i,
                Data = i.Element(ns + "Data"),
                Callback = i.Element(ns + "CallbackID"),
                ObjectId = i.Attribute("objectID")?.Value ?? Guid.NewGuid().ToString("N")
            })
            .ToList();

        var changed = false;

        foreach (var item in imageElements)
        {
            report.ImagesScanned++;

            try
            {
                string base64;
                if (item.Data is not null && !string.IsNullOrWhiteSpace(item.Data.Value))
                {
                    base64 = item.Data.Value;
                }
                else if (item.Callback is not null)
                {
                    var callbackId = item.Callback.Attribute("callbackID")?.Value;
                    if (string.IsNullOrWhiteSpace(callbackId))
                    {
                        report.Errors.Add($"Image {item.ObjectId}: callbackID is missing.");
                        continue;
                    }

                    base64 = gateway.GetBinaryPageContent(pageId, callbackId);
                    if (string.IsNullOrWhiteSpace(base64))
                    {
                        report.Errors.Add($"Image {item.ObjectId}: callbackID {callbackId} returned empty binary content.");
                        continue;
                    }
                }
                else
                {
                    // No inline data and no callback binary source.
                    continue;
                }

                var bytes = Convert.FromBase64String(base64);
                report.OriginalBytes += bytes.Length;

                var compressed = TryCompress(bytes, options);
                if (compressed is null)
                {
                    report.NewBytes += bytes.Length;
                    continue;
                }

                var encoded = Convert.ToBase64String(compressed.Value.imageBytes);
                if (item.Data is null)
                {
                    if (item.Callback is not null)
                    {
                        item.Callback.ReplaceWith(new XElement(ns + "Data", encoded));
                    }
                    else
                    {
                        item.Node.Add(new XElement(ns + "Data", encoded));
                    }
                }
                else
                {
                    item.Data.Value = encoded;
                }
                report.NewBytes += compressed.Value.imageBytes.Length;
                report.ImagesChanged++;
                changed = true;

                report.Changes.Add(new ImageChange
                {
                    PageId = pageId,
                    ObjectId = item.ObjectId,
                    OriginalBytes = bytes.Length,
                    NewBytes = compressed.Value.imageBytes.Length,
                    OriginalFormat = InferEncodedImageFormat(bytes),
                    NewFormat = compressed.Value.format,
                    Width = ReadSize(bytes).width,
                    Height = ReadSize(bytes).height,
                    NewWidth = compressed.Value.width,
                    NewHeight = compressed.Value.height
                });
            }
            catch (Exception ex)
            {
                report.Errors.Add($"Image {item.ObjectId}: {ex.Message}");
                report.NewBytes += 0;
            }
        }

        if (!changed)
        {
            return false;
        }

        updatedXml = doc.ToString(SaveOptions.DisableFormatting);
        return true;
    }

    private static (int width, int height) CalculateSize(int width, int height, CompactOptions options)
    {
        var targetWidth = width;
        var targetHeight = height;

        if (options.NoUpscale)
        {
            targetWidth = Math.Min(targetWidth, options.MaxWidth);
            targetHeight = Math.Min(targetHeight, options.MaxHeight);
        }

        var ratioX = (double)options.MaxWidth / width;
        var ratioY = (double)options.MaxHeight / height;
        var ratio = Math.Min(ratioX, ratioY);

        if (options.NoUpscale)
        {
            ratio = Math.Min(1.0, ratio);
        }

        if (ratio <= 0)
        {
            ratio = 1.0;
        }

        return ((int)Math.Max(1, Math.Round(width * ratio)), (int)Math.Max(1, Math.Round(height * ratio)));
    }

    private static (byte[] imageBytes, int width, int height, string format)? CompressToJpeg(Bitmap bitmap, long originalBytes, CompactOptions options)
    {
        var encoder = ImageCodecInfo.GetImageEncoders().FirstOrDefault(c => c.FormatID == ImageFormat.Jpeg.Guid);
        if (encoder is null)
        {
            return null;
        }

        var quality = options.JpegQuality;

        if (options.Mode is CompressionMode.TargetSize or CompressionMode.Smart)
        {
            var targetBytes = options.TargetKb * 1024L;
            quality = Math.Clamp(options.MaxQuality, options.MinQuality, 100);

            while (quality >= options.MinQuality)
            {
                var candidate = SaveJpeg(bitmap, encoder, quality);
                if (candidate.Length <= targetBytes)
                {
                    return (candidate, bitmap.Width, bitmap.Height, "jpeg");
                }

                quality -= 5;
            }

            if (options.Mode == CompressionMode.TargetSize)
            {
                return null;
            }
        }

        quality = Math.Clamp(quality, options.MinQuality, options.MaxQuality);
        var fallback = SaveJpeg(bitmap, encoder, quality);

        return fallback.Length < originalBytes
            ? (fallback, bitmap.Width, bitmap.Height, "jpeg")
            : null;
    }

    private static byte[] SaveJpeg(Bitmap bitmap, ImageCodecInfo encoder, int quality)
    {
        using var stream = new MemoryStream();
        using var parameters = new EncoderParameters(1);
        parameters.Param[0] = new EncoderParameter(Encoder.Quality, quality);
        bitmap.Save(stream, encoder, parameters);
        return stream.ToArray();
    }

    private static string GetFormatName(ImageFormat format)
    {
        if (format.Guid == ImageFormat.Jpeg.Guid) return "jpeg";
        if (format.Guid == ImageFormat.Png.Guid) return "png";
        if (format.Guid == ImageFormat.Gif.Guid) return "gif";
        if (format.Guid == ImageFormat.Bmp.Guid) return "bmp";
        return "unknown";
    }

    private static (int width, int height) ReadSize(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        using var image = Image.FromStream(ms);
        return (image.Width, image.Height);
    }

    private static string InferEncodedImageFormat(byte[] bytes)
    {
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xD8) return "jpeg";
        if (bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50) return "png";
        return "unknown";
    }
}
