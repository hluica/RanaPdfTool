using RanaPdfTool.Models;

namespace RanaPdfTool.Utils;

public class NodeHelper
{
    private static readonly HashSet<string> ValidExtensions
        = new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png" };

    public static DirectoryNode BuildDirectoryTree(DirectoryInfo dirInfo, IComparer<string> comparer)
    {
        var node = new DirectoryNode
        {
            Name = dirInfo.Name
        };

        // 1. 获取当前目录下的图片文件并排序
        var files = dirInfo.GetFiles()
            .Where(f => ValidExtensions.Contains(f.Extension))
            .Select(f => f.FullName)
            .OrderBy(f => f, comparer)
            .ToList();

        node.Files.AddRange(files);

        // 2. 获取子目录并递归
        var subDirs = dirInfo.GetDirectories()
            .OrderBy(d => d.Name, comparer);

        foreach (var subDir in subDirs)
        {
            // 递归构建子节点
            var childNode = BuildDirectoryTree(subDir, comparer);

            // 只有当子节点包含文件（或者是其子孙包含文件）时才添加，避免空文件夹占位
            if (childNode.TotalFileCount() > 0)
            {
                node.Children.Add(childNode);
            }
        }

        return node;
    }

    /// <summary>
    /// 扁平化获取所有节点中的文件列表引用，用于预处理循环
    /// </summary>
    public static IEnumerable<List<string>> GetAllFileLists(DirectoryNode node)
    {
        yield return node.Files;
        foreach (var child in node.Children)
        {
            foreach (var list in GetAllFileLists(child))
            {
                yield return list;
            }
        }
    }
}
