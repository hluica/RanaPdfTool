using System.Reflection;

using Microsoft.Extensions.DependencyInjection;

using RanaPdfTool.Commands;
using RanaPdfTool.Infrastructure;
using RanaPdfTool.Services;
using RanaPdfTool.Services.Interfaces;

using Spectre.Console.Cli;

namespace RanaPdfTool;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var services = new ServiceCollection();

        // 注册依赖
        _ = services
            .AddSingleton<IImageService, ImageService>()
            .AddSingleton<IPdfService, PdfService>();

        // 创建注册器
        var registrar = new TypeRegistrar(services);

        // 创建 App
        var app = new CommandApp(registrar);

        app.Configure(config =>
        {
            _ = config.SetApplicationName("RanaPdfTool")
                .SetApplicationVersion(GetAppVersion());

            _ = config.AddCommand<MergeCommand>("merge")
                .WithDescription("Merges images from a folder into a single PDF.");

            _ = config.AddCommand<ResizeCommand>("resize")
                .WithDescription("Resizes PDF pages to a fixed width (A4 width) while maintaining aspect ratio & image quality.");

            _ = config.AddCommand<ExtractCommand>("extract")
                .WithDescription("Extracts images from a PDF file.");
        });

        return await app.RunAsync(args);
    }

    private static string GetAppVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        string? version = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        return !string.IsNullOrWhiteSpace(version) ? version : "unknown";
    }
}
