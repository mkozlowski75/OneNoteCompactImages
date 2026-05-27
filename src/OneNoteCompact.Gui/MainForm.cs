using OneNoteCompact.Core.Models;
using OneNoteCompact.Core.Services;

namespace OneNoteCompact.Gui;

public sealed class MainForm : Form
{
    private readonly TextBox _notebook = new() { PlaceholderText = "Notebook (optional)", Width = 250 };
    private readonly TextBox _section = new() { PlaceholderText = "Section (optional)", Width = 250 };
    private readonly NumericUpDown _maxWidth = new() { Minimum = 256, Maximum = 8000, Value = 1920 };
    private readonly NumericUpDown _maxHeight = new() { Minimum = 256, Maximum = 8000, Value = 1920 };
    private readonly NumericUpDown _quality = new() { Minimum = 1, Maximum = 100, Value = 80 };
    private readonly NumericUpDown _targetKb = new() { Minimum = 32, Maximum = 10000, Value = 350 };
    private readonly ComboBox _mode = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
    private readonly ComboBox _backup = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
    private readonly CheckBox _dryRun = new() { Text = "Dry Run", Checked = true };
    private readonly CheckBox _keepAlpha = new() { Text = "Keep PNG Alpha", Checked = true };
    private readonly Button _run = new() { Text = "Start", Width = 120 };
    private readonly TextBox _log = new() { Multiline = true, ScrollBars = ScrollBars.Vertical, Width = 760, Height = 280 };

    public MainForm()
    {
        Text = "OneNoteCompact";
        Width = 820;
        Height = 520;

        _mode.Items.AddRange(new object[] { CompressionMode.Dimension, CompressionMode.TargetSize, CompressionMode.Smart });
        _mode.SelectedItem = CompressionMode.Smart;
        _backup.Items.AddRange(new object[] { BackupMode.Off, BackupMode.Page, BackupMode.Section });
        _backup.SelectedItem = BackupMode.Page;

        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(10), AutoScroll = true };
        panel.Controls.AddRange(new Control[]
        {
            _notebook, _section,
            new Label { Text = "Max Width" }, _maxWidth,
            new Label { Text = "Max Height" }, _maxHeight,
            new Label { Text = "JPEG Quality" }, _quality,
            new Label { Text = "Target KB" }, _targetKb,
            new Label { Text = "Mode" }, _mode,
            new Label { Text = "Backup" }, _backup,
            _dryRun, _keepAlpha, _run, _log
        });

        _run.Click += async (_, _) => await RunAsync();
        Controls.Add(panel);
    }

    private async Task RunAsync()
    {
        _run.Enabled = false;
        _log.Clear();

        try
        {
            var options = new CompactOptions
            {
                Notebook = string.IsNullOrWhiteSpace(_notebook.Text) ? null : _notebook.Text,
                Section = string.IsNullOrWhiteSpace(_section.Text) ? null : _section.Text,
                MaxWidth = (int)_maxWidth.Value,
                MaxHeight = (int)_maxHeight.Value,
                JpegQuality = (int)_quality.Value,
                TargetKb = (int)_targetKb.Value,
                Mode = (CompressionMode)_mode.SelectedItem!,
                BackupMode = (BackupMode)_backup.SelectedItem!,
                DryRun = _dryRun.Checked,
                KeepPngAlpha = _keepAlpha.Checked,
                ReportJson = Path.Combine(AppContext.BaseDirectory, "last-run-report.json")
            };

            await Task.Run(() =>
            {
                var runner = new CompactRunner(new OneNoteComGateway(), new ImageCompressionService());
                runner.Run(options, WriteLog);
            });

            WriteLog("Completed.");
        }
        catch (Exception ex)
        {
            WriteLog($"Error: {ex.Message}");
        }
        finally
        {
            _run.Enabled = true;
        }
    }

    private void WriteLog(string text)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => WriteLog(text));
            return;
        }

        _log.AppendText(text + Environment.NewLine);
    }
}
