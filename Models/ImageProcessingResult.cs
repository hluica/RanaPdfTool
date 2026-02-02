namespace RanaPdfTool.Models;

/// <summary>
/// 用于承载 Commands/MergeCommand.cs 中图片处理结果的信息载体
/// </summary>
/// <param name="ParentList">引用原始树节点中的文件列表</param>
/// <param name="Index">文件在列表中的原始位置</param>
/// <param name="OriginalPath">原始路径</param>
/// <param name="NewPath">处理后的（临时）路径</param>
/// <param name="IsSuccess">是否处理成功</param>
/// <param name="Exception">错误信息</param>
public record ImageProcessingResult(
    List<string> ParentList,
    int Index,
    string OriginalPath,
    string? NewPath,
    bool IsSuccess,
    Exception? Exception
);
