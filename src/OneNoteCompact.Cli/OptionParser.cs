using OneNoteCompact.Core.Models;

namespace OneNoteCompact.Cli;

internal static class OptionParser
{
    public static CompactOptions Parse(string[] args)
    {
        var options = new CompactOptions();

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            string NextValue()
            {
                if (i + 1 >= args.Length)
                {
                    throw new ArgumentException($"Missing value for {arg}");
                }

                i++;
                return args[i];
            }

            switch (arg)
            {
                case "--notebook": options.Notebook = NextValue(); break;
                case "--section": options.Section = NextValue(); break;
                case "--page": options.Page = NextValue(); break;
                case "--page-id": options.PageId = NextValue(); break;
                case "--limit-pages": options.LimitPages = int.Parse(NextValue()); break;

                case "--mode": options.Mode = Enum.Parse<CompressionMode>(NextValue().Replace("-", string.Empty), true); break;
                case "--max-width": options.MaxWidth = int.Parse(NextValue()); break;
                case "--max-height": options.MaxHeight = int.Parse(NextValue()); break;
                case "--jpeg-quality": options.JpegQuality = int.Parse(NextValue()); break;
                case "--target-kb": options.TargetKb = int.Parse(NextValue()); break;
                case "--min-quality": options.MinQuality = int.Parse(NextValue()); break;
                case "--max-quality": options.MaxQuality = int.Parse(NextValue()); break;
                case "--keep-png-alpha": options.KeepPngAlpha = true; break;
                case "--no-keep-png-alpha": options.KeepPngAlpha = false; break;
                case "--no-upscale": options.NoUpscale = true; break;
                case "--allow-upscale": options.NoUpscale = false; break;
                case "--min-bytes-to-process": options.MinBytesToProcess = int.Parse(NextValue()); break;
                case "--skip-small-images": options.SkipSmallImages = true; break;
                case "--process-small-images": options.SkipSmallImages = false; break;

                case "--dry-run": options.DryRun = true; break;
                case "--diagnose-only": options.DiagnoseOnly = true; break;
                case "--list-notebooks": options.ListNotebooks = true; break;
                case "--prefetch-pages": options.PrefetchPages = true; break;
                case "--prefetch-delay-ms": options.PrefetchDelayMs = int.Parse(NextValue()); break;
                case "--prefetch-scroll": options.PrefetchScroll = true; break;
                case "--prefetch-scroll-steps": options.PrefetchScrollSteps = int.Parse(NextValue()); break;
                case "--prefetch-scroll-delay-ms": options.PrefetchScrollDelayMs = int.Parse(NextValue()); break;
                case "--skip-marked-pages": options.SkipMarkedPages = true; break;
                case "--no-skip-marked-pages": options.SkipMarkedPages = false; break;
                case "--auto-mark-processed-pages": options.AutoMarkProcessedPages = true; break;
                case "--page-marker-text": options.PageMarkerText = NextValue(); break;
                case "--backup": options.BackupMode = Enum.Parse<BackupMode>(NextValue(), true); break;
                case "--backup-directory": options.BackupDirectory = NextValue(); break;
                case "--on-error": options.OnError = Enum.Parse<OnErrorMode>(NextValue(), true); break;

                case "--log-file": options.LogFile = NextValue(); break;
                case "--report-json": options.ReportJson = NextValue(); break;

                case "--help":
                case "-h":
                    PrintHelp();
                    Environment.Exit(0);
                    break;

                default:
                    throw new ArgumentException($"Unknown argument: {arg}");
            }
        }

        Validate(options);
        return options;
    }

    public static void PrintHelp()
    {
        Console.WriteLine("OneNoteCompact CLI");
        Console.WriteLine("Usage: onenotecompact [options]");
        Console.WriteLine("--notebook <name> --section <name> --page <name> --page-id <id> --limit-pages <n>");
        Console.WriteLine("--mode <dimension|targetsize|smart> --max-width <px> --max-height <px>");
        Console.WriteLine("--jpeg-quality <1-100> --target-kb <n> --min-quality <1-100> --max-quality <1-100>");
        Console.WriteLine("--keep-png-alpha|--no-keep-png-alpha --no-upscale|--allow-upscale");
        Console.WriteLine("--min-bytes-to-process <n> --skip-small-images|--process-small-images");
        Console.WriteLine("--dry-run --diagnose-only --list-notebooks --prefetch-pages --prefetch-delay-ms <n> --prefetch-scroll --prefetch-scroll-steps <n> --prefetch-scroll-delay-ms <n>");
        Console.WriteLine("--skip-marked-pages|--no-skip-marked-pages --auto-mark-processed-pages --page-marker-text <text>");
        Console.WriteLine("--backup <off|page|section> --backup-directory <path>");
        Console.WriteLine("--on-error <continue|stop> --log-file <path> --report-json <path>");
    }

    private static void Validate(CompactOptions options)
    {
        if (options.MaxWidth <= 0 || options.MaxHeight <= 0)
        {
            throw new ArgumentException("Max dimensions must be > 0");
        }

        if (options.MinQuality < 1 || options.MaxQuality > 100 || options.MinQuality > options.MaxQuality)
        {
            throw new ArgumentException("Quality bounds are invalid");
        }

        if (options.PrefetchDelayMs < 0)
        {
            throw new ArgumentException("Prefetch delay must be >= 0");
        }

        if (options.PrefetchScrollSteps < 0)
        {
            throw new ArgumentException("Prefetch scroll steps must be >= 0");
        }

        if (options.PrefetchScrollDelayMs < 0)
        {
            throw new ArgumentException("Prefetch scroll delay must be >= 0");
        }

        if (string.IsNullOrWhiteSpace(options.PageMarkerText))
        {
            throw new ArgumentException("Page marker text must not be empty");
        }
    }
}
