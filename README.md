# OneNoteCompact

Tool zur dauerhaften Komprimierung eingebetteter Bilder in OneNote Desktop-Seiten.

## CLI Beispiel

```powershell
dotnet run --project src/OneNoteCompact.Cli -- --notebook "Mein Notizbuch" --mode smart --max-width 1920 --jpeg-quality 80 --dry-run --report-json .\report.json
```

## Wichtige Optionen

- `--notebook`, `--section`, `--page-id`, `--limit-pages`
- `--mode dimension|targetsize|smart`
- `--max-width`, `--max-height`, `--jpeg-quality`, `--target-kb`
- `--keep-png-alpha`, `--no-upscale`
- `--dry-run`, `--backup off|page|section`, `--on-error continue|stop`
- `--log-file`, `--report-json`

## GUI

```powershell
dotnet run --project src/OneNoteCompact.Gui
```

Die GUI verwendet dieselbe Core-Logik wie die CLI.
