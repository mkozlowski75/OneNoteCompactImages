using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace OneNoteCompact.Cli;

internal static class OneNoteComDiagnostics
{
    private const string OneNoteProgId = "OneNote.Application";

    public static bool RunPreflight(out string message)
    {
        if (!OperatingSystem.IsWindows())
        {
            message = $"OneNote COM is only available on Windows. ProgID={OneNoteProgId}";
            return false;
        }

        try
        {
            var type = Type.GetTypeFromProgID(OneNoteProgId);
            if (type is null)
            {
                message = $"{OneNoteProgId} ProgID not found. Install OneNote Desktop (Office).";
                return false;
            }

            var app = Activator.CreateInstance(type);
            if (app is null)
            {
                message = $"OneNote COM type found but instance creation returned null. ProgID={OneNoteProgId}";
                return false;
            }

            Marshal.FinalReleaseComObject(app);
            message = $"OneNote COM preflight OK. ProgID={OneNoteProgId}";
            return true;
        }
        catch (COMException ex)
        {
            message = BuildComErrorMessage(ex);
            return false;
        }
        catch (Exception ex)
        {
            message = $"OneNote COM preflight failed: {ex.Message}. ProgID={OneNoteProgId}";
            return false;
        }
    }

    public static string BuildComErrorMessage(COMException ex)
    {
        var h = unchecked((uint)ex.HResult);
        var hex = $"0x{h:X8}";

        if (h == 0x8002801D)
        {
            return $"OneNote COM type library not registered ({hex}). ProgID={OneNoteProgId}.\n" +
                   BuildTypeLibraryReport() + "\n" +
                   "Fix:\n" +
                   "1) Close OneNote.\n" +
                   "2) Run: \"C:\\Program Files\\Microsoft Office\\root\\Office16\\ONENOTE.EXE\" /regserver\n" +
                   "3) Start and close OneNote once.\n" +
                   "4) If still failing, run Office Quick Repair/Online Repair.";
        }

        if (h == 0x80070520)
        {
            return $"No active interactive logon session for COM ({hex}). ProgID={OneNoteProgId}.\n" +
                   "Fix:\n" +
                   "1) Start OneNote Desktop normally in your user session.\n" +
                   "2) Run this tool in the same user session (not service/task context).\n" +
                   "3) Avoid elevated context if OneNote runs non-elevated.";
        }

        if (h == 0x80040154)
        {
            return $"OneNote COM class not registered ({hex}). ProgID={OneNoteProgId}.\n" +
                   BuildTypeLibraryReport() + "\n" +
                   "Fix: Install/repair OneNote Desktop (Office), then rerun /regserver.";
        }

        return $"OneNote COM error {hex}: {ex.Message}. ProgID={OneNoteProgId}";
    }

    private static string BuildTypeLibraryReport()
    {
        try
        {
            var progIdKeyPath = $@"{OneNoteProgId}\\CLSID";
            using var progIdKey = Registry.ClassesRoot.OpenSubKey(progIdKeyPath);
            var clsid = progIdKey?.GetValue(null)?.ToString();
            if (string.IsNullOrWhiteSpace(clsid))
            {
                return $"TypeLib diagnostic: Could not resolve CLSID from HKCR\\{progIdKeyPath}. ProgID={OneNoteProgId}";
            }

            var clsidKeyPath = $@"CLSID\\{clsid}";
            using var clsidKey = Registry.ClassesRoot.OpenSubKey(clsidKeyPath);
            var typeLib = clsidKey?.OpenSubKey("TypeLib")?.GetValue(null)?.ToString();
            var version = clsidKey?.OpenSubKey("Version")?.GetValue(null)?.ToString();

            if (string.IsNullOrWhiteSpace(typeLib))
            {
                return $"TypeLib diagnostic: ProgID={OneNoteProgId}; CLSID={clsid}; missing HKCR\\{clsidKeyPath}\\TypeLib default value.";
            }

            var tlVersion = string.IsNullOrWhiteSpace(version) ? "1.0" : version;
            var tlBasePath = $@"TypeLib\\{typeLib}\\{tlVersion}\\0";
            var win64PathKey = $@"HKCR\\{tlBasePath}\\win64";
            var win32PathKey = $@"HKCR\\{tlBasePath}\\win32";

            using var tlKey = Registry.ClassesRoot.OpenSubKey(tlBasePath);
            var win64Path = tlKey?.OpenSubKey("win64")?.GetValue(null)?.ToString();
            var win32Path = tlKey?.OpenSubKey("win32")?.GetValue(null)?.ToString();

            var resolvedPath = !string.IsNullOrWhiteSpace(win64Path) ? win64Path : win32Path;
            if (string.IsNullOrWhiteSpace(resolvedPath))
            {
                return $"TypeLib diagnostic: ProgID={OneNoteProgId}; CLSID={clsid}; TypeLib={typeLib}; Version={tlVersion}; RegistryKey=HKCR\\{tlBasePath}; Missing value at {win64PathKey} and {win32PathKey}.";
            }

            var usedKey = !string.IsNullOrWhiteSpace(win64Path) ? win64PathKey : win32PathKey;
            return $"TypeLib diagnostic: ProgID={OneNoteProgId}; CLSID={clsid}; TypeLib={typeLib}; Version={tlVersion}; RegistryValue={usedKey}\\(Default) = {resolvedPath}";
        }
        catch (Exception e)
        {
            return $"TypeLib diagnostic failed: {e.Message}. ProgID={OneNoteProgId}";
        }
    }
}
