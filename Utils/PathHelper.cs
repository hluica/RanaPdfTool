namespace RanaPdfTool.Utils;

public static class PathHelper
{
    public static string ResolveAbsolutePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be empty.");

        // 预检查：防止 GetFullPath 抛出 ArgumentException (非法字符)
        if (path.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            throw new ArgumentException($"Path contains invalid characters: {path}");

        // 确保路径相对于当前的 Shell 工作目录
        return Path.GetFullPath(path, Directory.GetCurrentDirectory());
    }

    public static string GetUniqueFilePath(string fullPath)
    {
        // 如果该路径甚至都不是有效的文件路径格式，直接返回（后续写入会报错，这里不做过深验证）
        if (string.IsNullOrWhiteSpace(fullPath))
            return fullPath;

        string directory = Path.GetDirectoryName(fullPath)!;
        string filename = Path.GetFileNameWithoutExtension(fullPath);
        string extension = Path.GetExtension(fullPath);

        string newPath = fullPath;
        int counter = 1;

        // 循环检查：
        // 1. File.Exists: 文件是否存在
        // 2. Directory.Exists: 是否有一个同名的文件夹存在（Windows/Linux 都不允许文件和文件夹重名）
        while (File.Exists(newPath) || Directory.Exists(newPath))
        {
            newPath = Path.Combine(directory, $"{filename} ({counter}){extension}");
            counter++;

            // 防止极端情况下的死循环
            if (counter > 10000)
                throw new IOException($"Unable to find a unique file path for {fullPath} after 10000 attempts.");
        }

        return newPath;
    }
}
