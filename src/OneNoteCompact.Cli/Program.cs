using System.Runtime.InteropServices;
using OneNoteCompact.Cli;
using OneNoteCompact.Core.Services;

CliLogger? logger = null;
try
{
    var options = OptionParser.Parse(args);
    logger = new CliLogger(options.LogFile);

    if (!OneNoteComDiagnostics.RunPreflight(out var preflightMessage))
    {
        logger.Error(preflightMessage);
        return 2;
    }

    logger.Info(preflightMessage);

    if (options.DiagnoseOnly)
    {
        logger.Info("Diagnose-only mode requested. Exiting without processing pages.");
        return 0;
    }

    var gateway = new OneNoteComGateway();
    if (options.ListNotebooks)
    {
        var map = gateway.ListNotebooksAndSections();
        if (map.Count == 0)
        {
            logger.Info("No notebooks returned by OneNote.");
            return 0;
        }

        logger.Info("Notebooks found:");
        foreach (var (notebook, sections) in map.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
        {
            logger.Info($"- {notebook}");
            foreach (var section in sections)
            {
                logger.Info($"  - {section}");
            }
        }

        return 0;
    }

    var runner = new CompactRunner(gateway, new ImageCompressionService());
    var report = runner.Run(options, logger.Info);

    logger.Info(
        $"Done. Pages scanned={report.PagesScanned}, changed={report.PagesChanged}, images changed={report.ImagesChanged}, " +
        $"size: {FormatBytes(report.OriginalBytes)} -> {FormatBytes(report.NewBytes)} (saved {FormatBytes(report.SavedBytes)})");
    return 0;
}
catch (COMException ex)
{
    (logger ?? new CliLogger(null)).Error(OneNoteComDiagnostics.BuildComErrorMessage(ex));
    return 1;
}
catch (Exception ex)
{
    (logger ?? new CliLogger(null)).Error(ex.Message);
    OptionParser.PrintHelp();
    return 1;
}

static string FormatBytes(long bytes)
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
