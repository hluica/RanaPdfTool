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
    public static int Main(string[] args)
    {
        var services = new ServiceCollection();

        // 注册依赖
        services.AddSingleton<IImageService, ImageService>();
        services.AddSingleton<IPdfService, PdfService>();

        // 创建注册器
        var registrar = new TypeRegistrar(services);

        // 创建 App
        var app = new CommandApp(registrar);

        app.Configure(config =>
        {
            config.SetApplicationName("RanaPdfTool");
            config.SetApplicationVersion(GetAppVersion());

            config.AddCommand<MergeCommand>("merge")
                .WithDescription("Merges images from a folder into a single PDF.");

            config.AddCommand<ModifyCommand>("modify")
                .WithDescription("Resizes PDF pages to a fixed width (A4 width) while maintaining aspect ratio & image quality.");

            config.AddCommand<SplitCommand>("split")
                .WithDescription("Extracts images from a PDF file.");
        });

        return app.Run(args);
    }

    private static string GetAppVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        return !string.IsNullOrWhiteSpace(version) ? version : "unknown";
    }
}
