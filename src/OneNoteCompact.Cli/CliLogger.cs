namespace OneNoteCompact.Cli;

internal sealed class CliLogger
{
    private readonly string? _logFile;

    public CliLogger(string? logFile)
    {
        _logFile = string.IsNullOrWhiteSpace(logFile) ? null : logFile;
    }

    public void Info(string message)
    {
        Write(message, isError: false);
    }

    public void Error(string message)
    {
        Write(message, isError: true);
    }

    private void Write(string message, bool isError)
    {
        var line = $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}] {(isError ? "ERROR" : "INFO")} {message}";

        if (isError)
        {
            Console.Error.WriteLine(message);
        }
        else
        {
            Console.WriteLine(message);
        }

        if (_logFile is null)
        {
            return;
        }

        var dir = Path.GetDirectoryName(_logFile);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.AppendAllText(_logFile, line + Environment.NewLine);
    }
}
