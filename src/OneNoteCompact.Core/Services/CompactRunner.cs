using System.Text.Json;
using System.Xml.Linq;
using OneNoteCompact.Core.Models;

namespace OneNoteCompact.Core.Services;

public sealed class CompactRunner
{
    private readonly IOneNoteGateway _gateway;
    private readonly ImageCompressionService _compression;

    public CompactRunner(IOneNoteGateway gateway, ImageCompressionService compression)
    {
        _gateway = gateway;
        _compression = compression;
    }

    public CompactRunReport Run(CompactOptions options, Action<string>? log = null)
    {
        var report = new CompactRunReport { Options = options, StartedAtUtc = DateTimeOffset.UtcNow };

        var pages = _gateway.ListPages(options);
        log?.Invoke($"Found {pages.Count} matching page(s).");

        foreach (var page in pages)
        {
            try
            {
                if (options.PrefetchPages)
                {
                    _gateway.NavigateToPage(page.Id);
                    if (options.PrefetchDelayMs > 0)
                    {
                        Thread.Sleep(options.PrefetchDelayMs);
                    }

                    if (options.PrefetchScroll)
                    {
                        _gateway.PrefetchByScrolling(options.PrefetchScrollSteps, options.PrefetchScrollDelayMs);
                    }
                }

                var originalXml = _gateway.GetPageContent(page.Id);
                if (options.SkipMarkedPages && ContainsMarker(originalXml, options.PageMarkerText))
                {
                    var skipped = new PageReport
                    {
                        PageId = page.Id,
                        PageName = page.Name,
                        SectionId = page.SectionId
                    };
                    report.Pages.Add(skipped);
                    log?.Invoke($"[{page.Name}] skipped (marker found: {options.PageMarkerText}).");
                    continue;
                }

                var changed = _compression.TryRewritePageXml(_gateway, page.Id, originalXml, options, out var updatedXml, out var pageReport);
                var shouldMark = options.AutoMarkProcessedPages && !ContainsMarker(updatedXml, options.PageMarkerText);
                if (shouldMark)
                {
                    updatedXml = EnsureMarker(updatedXml, options.PageMarkerText);
                }

                report.Pages.Add(pageReport);
                log?.Invoke(
                    $"[{page.Name}] scanned={pageReport.ImagesScanned}, changed={pageReport.ImagesChanged}, " +
                    $"size: {FormatBytes(pageReport.OriginalBytes)} -> {FormatBytes(pageReport.NewBytes)} " +
                    $"(saved {FormatBytes(pageReport.OriginalBytes - pageReport.NewBytes)})");

                if ((!changed && !shouldMark) || options.DryRun)
                {
                    continue;
                }

                WriteBackup(page, originalXml, options);
                _gateway.UpdatePageContent(updatedXml);
            }
            catch (Exception ex)
            {
                var hresultText = $"0x{ex.HResult:X8}";
                var message = $"Page {page.Id} ({page.Name}) failed: {ex.GetType().Name} (HRESULT {hresultText}): {ex.Message}";
                report.Warnings.Add(message);
                log?.Invoke(message);

                if (options.OnError == OnErrorMode.Stop)
                {
                    throw;
                }
            }
        }

        report.CompletedAtUtc = DateTimeOffset.UtcNow;
        WriteOutputs(options, report, log);
        return report;
    }

    private static void WriteBackup(PageInfo page, string originalXml, CompactOptions options)
    {
        if (options.BackupMode == BackupMode.Off)
        {
            return;
        }

        Directory.CreateDirectory(options.BackupDirectory);
        var scope = options.BackupMode == BackupMode.Section ? $"section-{Sanitize(page.SectionName)}" : $"page-{Sanitize(page.Name)}";
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var fileName = $"{timestamp}_{scope}_{Sanitize(page.Id)}.xml";
        File.WriteAllText(Path.Combine(options.BackupDirectory, fileName), originalXml);
    }

    private static string Sanitize(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
    }

    private static void WriteOutputs(CompactOptions options, CompactRunReport report, Action<string>? log)
    {
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });

        if (!string.IsNullOrWhiteSpace(options.ReportJson))
        {
            File.WriteAllText(options.ReportJson, json);
            log?.Invoke($"Report written: {options.ReportJson}");
        }

        if (!string.IsNullOrWhiteSpace(options.LogFile))
        {
            File.AppendAllText(options.LogFile, $"{DateTimeOffset.Now:u} {json}{Environment.NewLine}");
        }
    }

    private static string FormatBytes(long bytes)
    {
        const double kb = 1024d;
        const double mb = kb * 1024d;
        const double gb = mb * 1024d;

        var abs = Math.Abs(bytes);
        if (abs >= gb) return $"{bytes / gb:0.##} GB";
        if (abs >= mb) return $"{bytes / mb:0.##} MB";
        if (abs >= kb) return $"{bytes / kb:0.##} KB";
        return $"{bytes} B";
    }

    private static bool ContainsMarker(string pageXml, string markerText)
    {
        try
        {
            var doc = XDocument.Parse(pageXml);
            var root = doc.Root;
            if (root is null)
            {
                return false;
            }

            var ns = root.Name.Namespace;
            return doc.Descendants(ns + "T").Any(t =>
                t.Value.Contains(markerText, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    private static string EnsureMarker(string pageXml, string markerText)
    {
        var doc = XDocument.Parse(pageXml);
        var root = doc.Root ?? throw new InvalidOperationException("Page XML has no root element.");
        var ns = root.Name.Namespace;

        if (doc.Descendants(ns + "T").Any(t => t.Value.Contains(markerText, StringComparison.OrdinalIgnoreCase)))
        {
            return pageXml;
        }

        var outline = new XElement(ns + "Outline",
            new XElement(ns + "Position",
                new XAttribute("x", "36"),
                new XAttribute("y", "56")),
            new XElement(ns + "Size",
                new XAttribute("width", "200"),
                new XAttribute("height", "30")),
            new XElement(ns + "OEChildren",
                new XElement(ns + "OE",
                    new XElement(ns + "T", markerText))));

        root.Add(outline);
        return doc.ToString(SaveOptions.DisableFormatting);
    }
}
