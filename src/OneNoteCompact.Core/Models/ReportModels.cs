namespace OneNoteCompact.Core.Models;

public sealed class ImageChange
{
    public required string PageId { get; init; }
    public required string ObjectId { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public int NewWidth { get; init; }
    public int NewHeight { get; init; }
    public long OriginalBytes { get; init; }
    public long NewBytes { get; init; }
    public string OriginalFormat { get; init; } = "unknown";
    public string NewFormat { get; init; } = "unknown";

    public long SavedBytes => OriginalBytes - NewBytes;
}

public sealed class PageReport
{
    public required string PageId { get; init; }
    public required string PageName { get; init; }
    public string? SectionId { get; init; }
    public int ImagesScanned { get; set; }
    public int ImagesChanged { get; set; }
    public List<ImageChange> Changes { get; } = new();
    public List<string> Errors { get; } = new();
    public long OriginalBytes { get; set; }
    public long NewBytes { get; set; }
}

public sealed class CompactRunReport
{
    public DateTimeOffset StartedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CompletedAtUtc { get; set; }
    public CompactOptions Options { get; init; } = new();
    public List<PageReport> Pages { get; } = new();
    public List<string> Warnings { get; } = new();

    public int PagesScanned => Pages.Count;
    public int PagesChanged => Pages.Count(p => p.ImagesChanged > 0);
    public int ImagesScanned => Pages.Sum(p => p.ImagesScanned);
    public int ImagesChanged => Pages.Sum(p => p.ImagesChanged);
    public long OriginalBytes => Pages.Sum(p => p.OriginalBytes);
    public long NewBytes => Pages.Sum(p => p.NewBytes);
    public long SavedBytes => OriginalBytes - NewBytes;
}

public sealed class PageInfo
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string SectionId { get; init; }
    public required string SectionName { get; init; }
    public required string NotebookName { get; init; }
}
