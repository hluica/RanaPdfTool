# RanaPdfTool

[![GitHub Release](https://img.shields.io/github/v/release/hluica/RanaPdfTool)](https://github.com/hluica/RanaPdfTool/releases/latest)
[![Ask DeepWiki](https://deepwiki.com/badge.svg)](https://deepwiki.com/hluica/RanaPdfTool)


RanaPdfTool 是一个基于 .NET 10 的命令行工具，用于处理图片文件和PDF文件之间的转换。

## 说明

- `merge` 命令：读取图片文件并合并为 PDF。
    - 递归读取文件夹中的 JPEG 和 PNG 类型的图片文件，将它们合并为一个PDF文件。
    - 使每个页面为一个原图片，且页面大小与图片尺寸相同。
    - 当文件夹中存在子文件夹时，会为每个包含图片的子文件夹生成书签。书签会保留原始文件夹结构。
- `resize` 命令：重设 PDF 页面大小。
    - 读取 PDF 文件中的每一页，然后重设页面大小。
    - 新的页面宽度将被统一为 A4 纸的宽度（595.276点 / 210毫米），但保持原始页面的宽高比与页面中的图像对象质量。
- `extract` 命令：读取 PDF 文件中每一页上的每一个图像对象，以恰当的格式输出为图片文件。

> [!note]
> 
> **平台差异功能**
> 
> 在 Windows 平台上，本程序的进度条组件将可以跟随系统主题色而改变自身的颜色。

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
            <td rowspan="5"><code>merge</code></td>
            <td><code>-s|--source &lt;PATH&gt;</code></td>
            <td>读取图片的源文件夹。<br>将递归读取源文件夹及其子文件夹。使用包含路径信息的自然排序确定 PDF 页面顺序。<br>如果存在子文件夹，会使用其名称作为 PDF 的书签。<br>目前，将被读取的图片文件类型被硬编码为 JPEG 和 PNG。</td>
        </tr>
        <tr>
            <td><code>-d|--destination &lt;PATH&gt;</code></td>
            <td>PDF 文件的输出位置。<br>如果传入的是文件路径，则生成对应的文件；<br>如果传入的是目录路径，则在对应目录下生成与源文件夹同名的文件。<br>只有以 .pdf 结尾的路径会被视为文件名，其他所有情况都将被视为目录名。</td>
        </tr>
        <tr>
            <td><code>-r|--resize</code></td>
            <td>如果存在，则尝试在生成 PDF 时重设页面大小。<br>如果不存在，则保留图片原本的大小。<br>重设的页面大小将具有 A4 纸的宽度（595.276点 / 210毫米），但保持原始图片的宽高比。</td>
        </tr>
        <tr>
            <td><code>-q|--quality</code></td>
            <td>介于 1 和 100 之间的整数。如果不显式指定，将使用默认值 90。<br>指定 JPEG 图片的质量参数。<br>无法在显式指定该参数的同时传入 <code>--raw</code> 开关。</td>
        </tr>
        <tr>
            <td><code>--raw</code></td>
            <td>如果存在，则尝试将非 JPEG 图像的原始数据完整存入 PDF 文件而不经过转换。<br>如果不存在，则将所有非 JPEG 格式以 <code>--quality</code> 指定的质量换为 JPEG 图片。<br>无法在显式指定该开关的同时传入 <code>--quality</code> 参数。</td>
        </tr>
        <tr>
            <td rowspan="3"><code>resize</code></td>
            <td><code>-f|--file &lt;FILE&gt;</code></td>
            <td>读取 PDF 文件的路径。</td>
        </tr>
        <tr>
            <td><code>-o|--overwrite</code></td>
            <td>覆盖原始 PDF 文件而不是创建新文件。</td>
        </tr>
        <tr>
            <td><code>-s|--suffix &lt;SUFFIX&gt;</code></td>
            <td>新 PDF 文件的后缀。<br>默认的后缀是 <code>resized</code>，文件名和后缀之间存在不可更改的下划线。<br>如果存在 <code>--overwrite</code> 开关，则忽略此参数。</td>
        </tr>
        <tr>
            <td rowspan="5"><code>extract</code></td>
            <td><code>-f|--file &lt;FILE&gt;</code></td>
            <td>读取 PDF 文件的路径。</td>
        </tr>
        <tr>
            <td><code>-d|--destination &lt;PATH&gt;</code></td>
            <td>图像文件的输出位置。只能是目录。</td>
        </tr>
        <tr>
            <td><code>--subfolder</code></td>
            <td>如果存在，则在指定的输出目录中创建一个与文件同名的子文件夹，将提取出的图片文件存入该子文件夹。<br>如果不存在，则直接在指定的输出目录中存储输出的图片</td>
        </tr>
        <tr>
            <td><code>-q|--quality</code></td>
            <td>介于 1 和 100 之间的整数。如果不显式指定，将使用默认值 90。<br>指定其他格式图像转换为 JPEG 格式时使用的质量参数。<br>无法在显式指定该参数的同时传入 <code>--raw</code> 开关。</td>
        </tr>
        <tr>
            <td><code>--raw</code></td>
            <td>如果存在，则尝试将 PDF 中的非 JPEG 编码图像还原成它原本的格式。<br>如果不存在，则将 PDF 中的非 JPEG 编码图像以 <code>--quality</code> 参数指定的质量转换为 JPEG 编码，再输出为文件。<br>无法识别的格式会使用 .dat 格式存储。<br>无法在显式指定该开关的同时传入 <code>--quality</code> 参数。</td>
        </tr>
    </tbody>
</table>

## 安装

RanaPdfTool 是一个 .NET 全局工具。获取软件包 `.nupkg` 文件后，可以通过以下命令安装：
```powershell
dotnet tool install RanaPdfTool --global --add-source <Source_Path> [--version <Version>]
```
其中 `<Source_Path>` 是包含 `.nupkg` 文件的目录路径（允许使用相对路径）；`<Version>` 是 `.nupkg` 文件名中列出的版本，当 `<Source_Path>` 目录内只有一个 `.nupkg` 文件时可以省略。

您可以从 Release 页面下载 `.nupkg` 文件，或者手动构建：
- 安装 .NET SDK；
- Clone 存储库，进入仓库根目录；
- 执行 `dotnet pack` 指令。
- 生成的`.nupkg` 文件将存储在仓库的 `/nupkg` 目录下。

## 开发

### 依赖项

- [itext](https://github.com/itext/itext-dotnet): 用于为读写 PDF 文件提供支持。
- [Microsoft.IO.RecyclableMemoryStream](https://github.com/microsoft/Microsoft.IO.RecyclableMemoryStream): 用于配置高性能的池化内存流。
- [NaturalSort.Extension](https://github.com/tompazourek/NaturalSort.Extension): 用于为配置自然排序比较器提供支持。
- [SixLabors.ImageSharp](https://github.com/SixLabors/ImageSharp): 用于为图片重编码（合并时）及格式检测（提取时）提供支持。
- [Spectre.Console](https://github.com/spectreconsole/spectre.console) 与 Spectre.Console.Cli: 作为终端文本格式化工具和控制台程序框架，为终端渲染与参数解析提供支持。

### 版本历史记录

| 版本号 | 发布日期 | 更新内容                                                                                                                       |
| ------ | -------- | ------------------------------------------------------------------------------------------------------------------------------ |
| v4.2.4 | 26-03-09 | 优化错误信息输出后端；调整输出信息细节。                                                                                       |
| v4.2.3 | 26-03-07 | 将错误信息输出到 stderr ；优化 Windows 上获取系统颜色的逻辑。                                                                  |
| v4.2.2 | 26-02-28 | 修复路径解析中潜在的错误；优化终端输出信息的显示效果，支持在 Windows 平台上显示系统主题色。                                    |
| v4.2.1 | 26-02-28 | 更新 TUI 进度条组件的显示效果。                                                                                                |
| v4.2.0 | 26-02-26 | 为 `resize` 命令添加 `--suffix` 和 `--overwrite` 参数，提高命令可定制性；更新 `resize` 命令生成文件的默认后缀。                |
| v4.1.4 | 26-02-19 | 更新依赖：Microsoft.Extensions.DependencyInjection, 10.0.2 -> 10.0.3                                                           |
| v4.1.3 | 26-02-11 | 为 RecyclableMemoryStream 提供自定义配置，以优化内存使用；使用 Server GC 和 Concurrent GC，以避免内存占用过高。                |
| v4.1.2 | 26-02-09 | 继续改进并行代码。                                                                                                             |
| v4.1.1 | 26-02-09 | 使用 Microsoft.IO.RecyclableMemoryStream 替换标准的 System.IO.MemoryStream 以取得更高性能。                                    |
| v4.1.0 | 26-02-09 | 修复并行代码中的性能问题。现在IO密集型操作和CPU密集型操作已经被完全分离。                                                      |
| v4.0.2 | 26-02-06 | 修复并行代码中的性能问题。                                                                                                     |
| v4.0.0 | 26-02-06 | 使用生产者-消费者模型优化并行代码。                                                                                            |
| v3.2.4 | 26-02-05 | 优化代码。                                                                                                                     |
| v3.2.3 | 26-02-04 | 优化 TUI 进度条组件的显示效果，同时调整后台代码的进度回调方式。                                                                |
| v3.2.1 | 26-02-03 | 使用模式匹配替代嵌套 if-else 和三目运算符，以提升代码可读性。更新 Readme。                                                     |
| v3.2.0 | 26-02-02 | 为终端输出的路径信息增加超链接功能，支持直接跳转到对应文件/目录；更改终端进度条显示信息；更改命令名称。                        |
| v3.1.0 | 26-02-02 | 更新后端图片处理逻辑，使用并行处理以提升处理速度。                                                                             |
| v3.0.4 | 26-01-17 | 更新后端计算逻辑，提高 PDF 页面大小的计算精度；更新依赖版本；更新 Readme。                                                     |
| v3.0.2 | 26-01-09 | 修复错误信息显示方式。为部分方法和接口提供XML注释。                                                                            |
| v3.0.1 | 26-01-09 | 更新后端合并文件逻辑，增加 PDF 书签支持；更改错误信息显示逻辑；更新依赖库版本。                                                |
| v2.5.2 | 26-01-07 | 更改后端服务中存储页面相关数据的结构，以提升性能和内存使用效率。                                                               |
| v2.5.0 | 25-12-25 | 为 `merge` 和 `extract` 命令增加 `-q \| --quality` 参数，用以手动指定 JPEG 图像的质量。                                        |
| v2.4.0 | 25-12-24 | 使用更细致的错误处理方式，现在将收集运行时的所有错误而不终止运行，并在操作完成后集中向用户展示。更新后端方法以支持实现这一点。 |
| v2.3.0 | 25-12-24 | 更改 `merge --destination` 路径验证逻辑，并使用更严格的验证方式。                                                              |
| v2.2.3 | 25-12-23 | 最初稳定可用版本。                                                                                                             |

## 许可证

由于 iText 的许可证限制，本项目使用 [AGPLv3](LICENSE.txt)。
