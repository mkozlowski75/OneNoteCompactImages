namespace OneNoteCompact.Core.Models;

public enum CompressionMode
{
    Dimension,
    TargetSize,
    Smart
}

public enum BackupMode
{
    Off,
    Page,
    Section
}

public enum OnErrorMode
{
    Continue,
    Stop
}

public sealed class CompactOptions
{
    public string? Notebook { get; set; }
    public string? Section { get; set; }
    public string? Page { get; set; }
    public string? PageId { get; set; }
    public int? LimitPages { get; set; }

    public CompressionMode Mode { get; set; } = CompressionMode.Smart;
    public int MaxWidth { get; set; } = 1920;
    public int MaxHeight { get; set; } = 1920;
    public int JpegQuality { get; set; } = 80;
    public int TargetKb { get; set; } = 350;
    public int MinQuality { get; set; } = 60;
    public int MaxQuality { get; set; } = 90;

    public bool KeepPngAlpha { get; set; } = true;
    public bool NoUpscale { get; set; } = true;
    public int MinBytesToProcess { get; set; } = 32 * 1024;
    public bool SkipSmallImages { get; set; } = true;

    public bool DryRun { get; set; }
    public bool DiagnoseOnly { get; set; }
    public bool ListNotebooks { get; set; }
    public bool PrefetchPages { get; set; }
    public int PrefetchDelayMs { get; set; } = 400;
    public bool PrefetchScroll { get; set; }
    public int PrefetchScrollSteps { get; set; } = 20;
    public int PrefetchScrollDelayMs { get; set; } = 300;
    public bool SkipMarkedPages { get; set; } = true;
    public bool AutoMarkProcessedPages { get; set; }
    public string PageMarkerText { get; set; } = "[ONC_SKIP]";
    public BackupMode BackupMode { get; set; } = BackupMode.Page;
    public OnErrorMode OnError { get; set; } = OnErrorMode.Continue;

    public string? LogFile { get; set; }
    public string? ReportJson { get; set; }
    public string BackupDirectory { get; set; } = Path.Combine(AppContext.BaseDirectory, "backups");
}
