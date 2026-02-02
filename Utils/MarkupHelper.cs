using Spectre.Console;

namespace RanaPdfTool.Utils;

public static class MarkupHelper
{
    public static string FileLinkMarkup(string target)
        => FileLinkMarkup(target, target);

    public static string FileLinkMarkup(string path, string displayText)
    {
        string safeUri = path
            .Replace("\\", "/")
            .Replace(" ", "%20")
            .Replace("[", "%5B")
            .Replace("]", "%5D")
            .TrimStart('/');

        return $"[link=file:///{safeUri}]{Markup.Escape(displayText)}[/]";
    }
}
