# RanaPdfTool

RanaPdfTool 是一个基于 .NET 10 的命令行工具，用于处理图片文件和PDF文件之间的转换。

## 说明

- `merge` 命令：读取图片文件并合并为 PDF。
    - 递归读取文件夹中的 JPEG 和 PNG 类型的图片文件，将它们合并为一个PDF文件。
    - 使每个页面为一个原图片，且页面大小与图片尺寸相同。
    - 当文件夹中存在子文件夹时，会为每个包含图片的子文件夹生成书签。书签会保留原始文件夹结构。
- `modify` 命令：重设 PDF 页面大小。
    - 读取 PDF 文件中的每一页，然后重设页面大小。
    - 新的页面宽度将被统一为 A4 纸的宽度，但保持原始页面的宽高比与页面中的图像对象质量。
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
            <td rowspan="5">merge</td>
            <td>-s|--source &lt;PATH&gt;</td>
            <td>读取图片的源文件夹。<br>将递归读取源文件夹及其子文件夹。使用包含路径信息的自然排序确定 PDF 页面顺序。<br>目前，将被读取的图片文件类型被硬编码为 JPEG 和 PNG。</td>
        </tr>
        <tr>
            <td>-d|--destination &lt;PATH&gt;</td>
            <td>PDF 文件的输出位置。<br>如果传入的是文件路径，则生成对应的文件；<br>如果传入的是目录路径，则在对应目录下生成与源文件夹同名的文件。<br>只有以 .pdf 结尾的路径会被视为文件名，其他所有情况都将被视为目录名。</td>
        </tr>
        <tr>
            <td>-r|--resize</td>
            <td>如果存在，则尝试在生成 PDF 时就重设页面大小。<br>如果不存在，使用图片原本的大小。</td>
        </tr>
        <tr>
            <td>-q|--quality</td>
            <td>介于 1 和 100 之间的整数。如果不显式指定，将使用默认值 90。<br>无法在显式指定该参数的同时指定 --raw 开关。</td>
        </tr>
        <tr>
            <td>--raw</td>
            <td>如果存在，则尝试将非 JPEG 图像的原始数据完整存入 PDF 文件而不经过转换。<br>如果不存在，则将所有非 JPEG 格式以 --quality 指定的质量换为 JPEG 图片。<br>无法在显式指定该开关的同时传入 --quality 参数。</td>
        </tr>
        <tr>
            <td>modify</td>
            <td>-f|--file &lt;FILE&gt;</td>
            <td>读取 PDF 文件的路径。</td>
        </tr>
        <tr>
            <td rowspan="5">split</td>
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
            <td>-q|--quality</td>
            <td>介于 1 和 100 之间的整数。如果不显式指定，将使用默认值 90。<br>无法在显式指定该参数的同时指定 --raw 开关。</td>
        </tr>
        <tr>
            <td>--raw</td>
            <td>如果存在，则尝试将 PDF 中的非 JPEG 图像对象还原成它原本的格式。<br>如果不存在，则将 PDF 中的非 JPEG 图像对象以 --quality 参数指定的质量转换为 JPEG 对象，再输出为文件。<br>无法识别的格式会使用 .dat 格式存储。<br>无法在显式指定该开关的同时传入 --quality 参数。</td>
        </tr>
    </tbody>
</table>

## 安装

- 本工具作为 dotnet 全局工具安装。
- 预先准备好 .NET 10 SDK。
- clone 存储库后，cd 到存储库目录，使用 `dotnet pack` 打包。
- 然后使用 `dotnet tool install --global --add-source .\nupkg\ RanaPdfTool` 从本地安装工具。

## 版本历史记录

- v3.0.0 01-09-26: 更新后端合并文件逻辑，增加 PDF 书签支持。更新依赖库版本。
- v2.5.2 01-07-26: 更改后端服务中存储页面相关数据的结构，以提升性能和内存使用效率。
- v2.5.0 12-25-25: 为 `merge` 和 `split` 命令增加 `-q|--quality` 参数，用以手动指定 JPEG 图像的质量。
- v2.4.0 12-24-25: 使用更细致的错误处理方式，现在将收集运行时的所有错误而不终止运行，并在操作完成后集中向用户展示。更新后端方法以支持实现这一点。
- v2.3.0 12-24-25: 更改 `merge --destination` 路径验证逻辑，并使用更严格的验证方式。
- v2.2.3 12-23-25: 最初稳定可用版本。

## 许可证

[AGPL](LICENSE.txt)
