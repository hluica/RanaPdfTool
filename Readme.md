# RanaPdfTool

RanaPdfTool 是一个基于 .NET 10 的命令行工具，用于处理图片文件和PDF文件之间的转换。

## 说明

- `merge` 命令：递归读取文件夹中的指定类型图片文件，将它们合并为一个PDF文件，使每个页面为一个原图片，且页面大小与图片尺寸相同。
- `modify` 命令：读取 PDF 文件中的每一页，然后重设页面大小，使之宽度统一为 A4 纸宽度，而保持长宽高和页面中的图像对象质量不变。
- `split` 命令：读取 PDF 文件中每一页上的每一个图像对象，以恰当的格式输出为图片文件。

## 命令

<table>
    <thead>
        <tr>
            <th>command</th>
            <th>parameter</th>
            <th>说明</th>
        </tr>
    </thead>
    <tbody>
        <tr>
            <td rowspan="4">merge</td>
            <td>-s|--source &lt;PATH&gt;</td>
            <td>读取图片的源文件夹。</td>
        </tr>
        <tr>
            <td>-d|--destination &lt;PATH&gt;</td>
            <td>PDF 文件的输出位置。<br>如果传入的是文件路径，则生成对应的文件；<br>如果传入的是目录路径，则在对应目录下生成与源文件夹同名的文件。<br>只有以 .pdf 结尾的路径会被视为文件名，其他所有情况都将被视为目录名。</td>
        </tr>
        <tr>
            <td>--raw</td>
            <td>如果存在，则尝试将非 JPEG 图像的原始数据完整存入 PDF 文件而不经过转换。<br>如果不存在，则将所有非 JPEG 格式以 95% 质量参数转换为 JPEG 图片。</td>
        </tr>
        <tr>
            <td>-r|--resize</td>
            <td>如果存在，则尝试在生成 PDF 时就重设页面大小。<br>如果不存在，使用图片原本的大小。</td>
        </tr>
        <tr>
            <td>modify</td>
            <td>-f|--file &lt;FILE&gt;</td>
            <td>读取 PDF 文件的路径。</td>
        </tr>
        <tr>
            <td rowspan="4">split</td>
            <td>-f|--file &lt;FILE&gt;</td>
            <td>读取 PDF 文件的路径。</td>
        </tr>
        <tr>
            <td>-d|--destination &lt;PATH&gt;</td>
            <td>图像文件地输出位置。只能是目录。</td>
        </tr>
        <tr>
            <td>--subfolder</td>
            <td>如果存在，则在指定的输出目录中再创建一个子文件夹，然后把输出的图片文件存入该子文件夹。<br>如果不存在，则直接在指定的输出目录中存储输出的图片</td>
        </tr>
        <tr>
            <td>--raw</td>
            <td>如果存在，则尝试将 PDF 中的非 JPEG 图像对象还原成它原本的格式。<br>如果不存在，则将 PDF 中的非 JPEG 图像对象以 95% 质量参数转换为 JPEG 对象，再输出为文件。<br>无法识别的格式会使用 .dat 格式存储。</td>
        </tr>
    </tbody>
</table>

## 安装

- 本工具作为 dotnet 全局工具安装，须使用 `dotnet pack` 先行打包，然后从本地安装工具。

## 版本历史记录

- v2.3.0 12-24-25: 更改 `merge --destination` 路径验证逻辑，并使用更严格的验证方式。
- v2.2.3 12-23-25: 最初稳定可用版本。

## 许可证

[AGPL](LICENSE.txt)
