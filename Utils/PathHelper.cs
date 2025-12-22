namespace RanaPdfTool.Utils;

public static class PathHelper
{
    public static string ResolveAbsolutePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        // 确保路径相对于当前的 Shell 工作目录，而不是 Exe 所在目录
        return Path.GetFullPath(path, Directory.GetCurrentDirectory());
    }

    public static string GetUniqueFilePath(string fullPath)
    {
        if (!File.Exists(fullPath))
            return fullPath;

        string directory = Path.GetDirectoryName(fullPath)!;
        string filename = Path.GetFileNameWithoutExtension(fullPath);
        string extension = Path.GetExtension(fullPath);

        int counter = 1;
        string newPath;
        do
        {
            newPath = Path.Combine(directory, $"{filename} ({counter}){extension}");
            counter++;
        } while (File.Exists(newPath));

        return newPath;
    }
}
