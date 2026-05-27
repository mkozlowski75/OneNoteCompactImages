using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using Microsoft.CSharp.RuntimeBinder;
using OneNoteCompact.Core.Models;

namespace OneNoteCompact.Core.Services;

public interface IOneNoteGateway
{
    IReadOnlyList<PageInfo> ListPages(CompactOptions options);
    IReadOnlyDictionary<string, IReadOnlyCollection<string>> ListNotebooksAndSections();
    string GetPageContent(string pageId);
    string GetBinaryPageContent(string pageId, string callbackId);
    void NavigateToPage(string pageId);
    void PrefetchByScrolling(int steps, int delayMs);
    void UpdatePageContent(string xml);
}

public sealed class OneNoteComGateway : IOneNoteGateway
{
    private const int HierarchyScopeNotebooks = 2;
    private const int HierarchyScopeSections = 3;
    private const int HierarchyScopePages = 4;
    private const int PageInfoLevelAll = 0;
    private const int XmlSchemaCurrent = 2;
    private const int XmlSchemaLegacy = 0;
    private const int SwRestore = 9;
    private const byte VkPageDown = 0x22;
    private const byte VkPageUp = 0x21;
    private const uint KeyEventFKeyUp = 0x0002;

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    private readonly object _app;
    private readonly Type _type;

    public OneNoteComGateway()
    {
        _type = Type.GetTypeFromProgID("OneNote.Application") ?? throw new InvalidOperationException("OneNote Desktop COM API nicht gefunden.");
        _app = Activator.CreateInstance(_type) ?? throw new InvalidOperationException("OneNote COM-Instanz konnte nicht erstellt werden.");
    }

    public IReadOnlyList<PageInfo> ListPages(CompactOptions options)
    {
        var hierarchyXml = ReadHierarchyXml(HierarchyScopePages, HierarchyScopeSections);
        if (string.IsNullOrWhiteSpace(hierarchyXml))
        {
            throw new InvalidOperationException(
                "OneNote returned empty hierarchy XML. Open OneNote Desktop with the target notebook synchronized, then retry.");
        }

        var doc = XDocument.Parse(hierarchyXml);
        if (doc.Root is null)
        {
            throw new InvalidOperationException("OneNote hierarchy XML has no root element.");
        }

        var ns = doc.Root.Name.Namespace;

        var pages =
            from notebook in doc.Descendants(ns + "Notebook")
            from section in notebook.Descendants(ns + "Section")
            from page in section.Descendants(ns + "Page")
            let notebookName = notebook.Attribute("name")?.Value ?? string.Empty
            let sectionName = section.Attribute("name")?.Value ?? string.Empty
            let pageName = page.Attribute("name")?.Value ?? string.Empty
            let pageId = page.Attribute("ID")?.Value
            let sectionId = section.Attribute("ID")?.Value
            where pageId is not null && sectionId is not null
            select new PageInfo
            {
                Id = pageId,
                Name = pageName,
                SectionId = sectionId,
                SectionName = sectionName,
                NotebookName = notebookName
            };

        var filtered = pages.Where(p =>
            (string.IsNullOrWhiteSpace(options.Notebook) || p.NotebookName.Contains(options.Notebook, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrWhiteSpace(options.Section) || p.SectionName.Contains(options.Section, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrWhiteSpace(options.Page) || p.Name.Contains(options.Page, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrWhiteSpace(options.PageId) || string.Equals(options.PageId, p.Id, StringComparison.OrdinalIgnoreCase)));

        if (options.LimitPages is > 0)
        {
            filtered = filtered.Take(options.LimitPages.Value);
        }

        var result = filtered.ToList();
        if (result.Count == 0)
        {
            var notebookText = string.IsNullOrWhiteSpace(options.Notebook) ? "(no notebook filter)" : options.Notebook;
            throw new InvalidOperationException($"No pages found for notebook filter: {notebookText}");
        }

        return result;
    }

    public string GetPageContent(string pageId)
    {
        var pageXml = ReadPageContentXml(pageId);
        if (string.IsNullOrWhiteSpace(pageXml))
        {
            throw new InvalidOperationException($"OneNote returned empty page XML for page ID: {pageId}");
        }

        return pageXml;
    }

    public void UpdatePageContent(string xml)
    {
        dynamic app = _app;
        app.UpdatePageContent(xml);
    }

    public string GetBinaryPageContent(string pageId, string callbackId)
    {
        dynamic app = _app;
        string base64;
        app.GetBinaryPageContent(pageId, callbackId, out base64);
        return base64 ?? string.Empty;
    }

    public void NavigateToPage(string pageId)
    {
        dynamic app = _app;
        app.NavigateTo(pageId);
    }

    public void PrefetchByScrolling(int steps, int delayMs)
    {
        if (steps <= 0)
        {
            return;
        }

        var oneNoteWindow = FindOneNoteMainWindow();
        if (oneNoteWindow == IntPtr.Zero)
        {
            return;
        }

        ShowWindowAsync(oneNoteWindow, SwRestore);
        SetForegroundWindow(oneNoteWindow);
        Thread.Sleep(150);

        for (var i = 0; i < steps; i++)
        {
            keybd_event(VkPageDown, 0, 0, UIntPtr.Zero);
            keybd_event(VkPageDown, 0, KeyEventFKeyUp, UIntPtr.Zero);
            if (delayMs > 0)
            {
                Thread.Sleep(delayMs);
            }
        }

        for (var i = 0; i < steps; i++)
        {
            keybd_event(VkPageUp, 0, 0, UIntPtr.Zero);
            keybd_event(VkPageUp, 0, KeyEventFKeyUp, UIntPtr.Zero);
            if (delayMs > 0)
            {
                Thread.Sleep(delayMs);
            }
        }
    }

    private static IntPtr FindOneNoteMainWindow()
    {
        var process = System.Diagnostics.Process
            .GetProcessesByName("ONENOTE")
            .Where(p => p.MainWindowHandle != IntPtr.Zero)
            .OrderByDescending(p => p.StartTime)
            .FirstOrDefault();

        return process?.MainWindowHandle ?? IntPtr.Zero;
    }

    public IReadOnlyDictionary<string, IReadOnlyCollection<string>> ListNotebooksAndSections()
    {
        var hierarchyXml = ReadHierarchyXml(HierarchyScopeNotebooks, HierarchyScopeSections, HierarchyScopePages);
        if (string.IsNullOrWhiteSpace(hierarchyXml))
        {
            throw new InvalidOperationException(
                "OneNote returned empty hierarchy XML. Open OneNote Desktop with the target notebook synchronized, then retry.");
        }

        var doc = XDocument.Parse(hierarchyXml);
        if (doc.Root is null)
        {
            throw new InvalidOperationException("OneNote hierarchy XML has no root element.");
        }

        var ns = doc.Root.Name.Namespace;
        var result = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var notebook in doc.Descendants(ns + "Notebook"))
        {
            var notebookName = notebook.Attribute("name")?.Value ?? "(unnamed notebook)";
            var sectionNames = notebook
                .Descendants(ns + "Section")
                .Select(s => s.Attribute("name")?.Value ?? "(unnamed section)")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            result[notebookName] = sectionNames;
        }

        return result;
    }

    private string ReadHierarchyXml(params int[] preferredScopes)
    {
        var scopes = preferredScopes.Length > 0
            ? preferredScopes
            : new[] { HierarchyScopeNotebooks, HierarchyScopeSections, HierarchyScopePages };

        foreach (var scope in scopes)
        {
            var xml = TryInvokeByRefString("GetHierarchy", new object?[] { string.Empty, scope, string.Empty, XmlSchemaCurrent }, 2);
            if (!string.IsNullOrWhiteSpace(xml))
            {
                return xml;
            }

            xml = TryInvokeByRefString("GetHierarchy", new object?[] { string.Empty, scope, string.Empty, XmlSchemaLegacy }, 2);
            if (!string.IsNullOrWhiteSpace(xml))
            {
                return xml;
            }

            xml = TryInvokeByRefString("GetHierarchy", new object?[] { string.Empty, scope, string.Empty }, 2);
            if (!string.IsNullOrWhiteSpace(xml))
            {
                return xml;
            }
        }

        return string.Empty;
    }

    private string ReadPageContentXml(string pageId)
    {
        var xml = TryInvokeByRefString("GetPageContent", new object?[] { pageId, string.Empty }, 1);
        if (!string.IsNullOrWhiteSpace(xml))
        {
            return xml;
        }

        // Fallbacks for other interop variants.
        xml = TryInvokeByRefString("GetPageContent", new object?[] { pageId, PageInfoLevelAll, string.Empty }, 2);
        if (!string.IsNullOrWhiteSpace(xml))
        {
            return xml;
        }

        xml = TryInvokeByRefString("GetPageContent", new object?[] { pageId, PageInfoLevelAll, string.Empty, XmlSchemaCurrent }, 2);
        if (!string.IsNullOrWhiteSpace(xml))
        {
            return xml;
        }

        xml = TryInvokeByRefString("GetPageContent", new object?[] { pageId, PageInfoLevelAll, string.Empty, XmlSchemaLegacy }, 2);
        if (!string.IsNullOrWhiteSpace(xml))
        {
            return xml;
        }

        return TryInvokeByRefString("GetPageContent", new object?[] { pageId, PageInfoLevelAll, string.Empty }, 2);
    }

    private string TryInvokeByRefString(string methodName, object?[] args, int outIndex)
    {
        try
        {
            dynamic app = _app;
            string xml;

            if (string.Equals(methodName, "GetHierarchy", StringComparison.Ordinal))
            {
                if (args.Length == 4)
                {
                    app.GetHierarchy((string)args[0]!, (int)args[1]!, out xml, (int)args[3]!);
                    return xml ?? string.Empty;
                }

                app.GetHierarchy((string)args[0]!, (int)args[1]!, out xml);
                return xml ?? string.Empty;
            }

            if (string.Equals(methodName, "GetPageContent", StringComparison.Ordinal))
            {
                if (args.Length == 2)
                {
                    app.GetPageContent((string)args[0]!, out xml);
                    return xml ?? string.Empty;
                }

                if (args.Length == 4)
                {
                    app.GetPageContent((string)args[0]!, (int)args[1]!, out xml, (int)args[3]!);
                    return xml ?? string.Empty;
                }

                app.GetPageContent((string)args[0]!, (int)args[1]!, out xml);
                return xml ?? string.Empty;
            }

            return string.Empty;
        }
        catch (RuntimeBinderException)
        {
            return string.Empty;
        }
        catch (TargetInvocationException)
        {
            return string.Empty;
        }
    }
}
