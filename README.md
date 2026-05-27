# OneNoteCompact

OneNoteCompact is a Windows tool for **permanent** compression of embedded images in **OneNote Desktop** pages.
It rewrites image data in page XML (not just visual display size), so notebooks can use less storage.

## Features

- Permanent image recompression in OneNote pages
- CLI-first workflow with optional GUI
- Dry-run mode with savings estimation
- Notebook/section/page targeting
- Page prefetch with automatic scrolling (down and back up)
- Optional page marker workflow to skip already processed pages
- JSON reporting and log file output

## Requirements

- Windows
- OneNote Desktop (COM available, ProgID `OneNote.Application`)
- .NET SDK (project currently targets .NET 10)

## Quick Start

Dry run for one section:

```powershell
dotnet run --project src/OneNoteCompact.Cli -- --notebook "My Notebook" --section "Travel" --dry-run --log-file .\onenotecompact.log
```

Real run with max image dimensions:

```powershell
dotnet run --project src/OneNoteCompact.Cli -- --notebook "My Notebook" --section "Travel" --max-width 1024 --max-height 1024 --log-file .\onenotecompact.log
```

## Common Options

Target selection:

- `--notebook <name>`
- `--section <name>`
- `--page <name>`
- `--page-id <id>`
- `--limit-pages <n>`

Compression:

- `--mode <dimension|targetsize|smart>`
- `--max-width <px>`
- `--max-height <px>`
- `--jpeg-quality <1-100>`
- `--target-kb <n>`
- `--min-quality <1-100>`
- `--max-quality <1-100>`
- `--keep-png-alpha` / `--no-keep-png-alpha`
- `--no-upscale` / `--allow-upscale`
- `--min-bytes-to-process <n>`
- `--skip-small-images` / `--process-small-images`

Execution and safety:

- `--dry-run`
- `--diagnose-only`
- `--list-notebooks`
- `--backup <off|page|section>`
- `--backup-directory <path>`
- `--on-error <continue|stop>`

Prefetch / loading assistance:

- `--prefetch-pages`
- `--prefetch-delay-ms <n>`
- `--prefetch-scroll`
- `--prefetch-scroll-steps <n>`
- `--prefetch-scroll-delay-ms <n>`

Page marker workflow:

- `--skip-marked-pages` / `--no-skip-marked-pages`
- `--auto-mark-processed-pages`
- `--page-marker-text <text>`

Output:

- `--log-file <path>`
- `--report-json <path>`

## Example: Dry Run with Prefetch + Scroll

```powershell
dotnet run --project src/OneNoteCompact.Cli -- --notebook "My Notebook" --section "Travel" --dry-run --prefetch-pages --prefetch-scroll --log-file .\onenotecompact.log --report-json .\report.json
```

## GUI

```powershell
dotnet run --project src/OneNoteCompact.Gui
```

The GUI uses the same core logic as the CLI.

## Troubleshooting

- `TYPE_E_LIBNOTREGISTERED (0x8002801D)`:
  - Close OneNote
  - Run: `"C:\Program Files\Microsoft Office\root\Office16\ONENOTE.EXE" /regserver`
  - Open and close OneNote once
- Empty hierarchy/page XML:
  - Open OneNote Desktop and make sure the notebook is synced and accessible
- Some images fail with binary callback errors:
  - Use page prefetch and scroll options so OneNote loads deferred image content first
