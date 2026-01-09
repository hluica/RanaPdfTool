namespace RanaPdfTool.Models;

/// <summary>
/// 表示一个包含图片文件的目录节点
/// </summary>
public class DirectoryNode
{
    /// <summary>
    /// 文件夹名称 (将作为书签标题)
    /// </summary>
    public string Name { get; set; }
        = string.Empty;

    /// <summary>
    /// 当前文件夹下的图片文件完整路径列表 (已排序)
    /// </summary>
    public List<string> Files { get; set; }
        = [];

    /// <summary>
    /// 子文件夹节点 (已排序)
    /// </summary>
    public List<DirectoryNode> Children { get; set; }
        = [];

    /// <summary>
    /// 辅助方法：获取树中所有文件的总数
    /// </summary>
    public int TotalFileCount()
        => Files.Count + Children.Sum(c => c.TotalFileCount());
}
