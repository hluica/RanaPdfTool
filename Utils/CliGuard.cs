using Spectre.Console;

namespace RanaPdfTool.Utils;

public static class CliGuard
{
    /// <summary>
    /// 执行有返回值的操作
    /// </summary>
    /// <typeparam name="TResult">返回值类型</typeparam>
    /// <typeparam name="TExpected">预期的“业务逻辑”异常类型</typeparam>
    /// <param name="action">要执行的函数</param>
    /// <param name="userMessage">当捕获到 TExpected 时向用户展示的友好提示</param>
    /// <returns>返回 (是否成功, 结果数据)</returns>
    public static (bool Success, TResult? Data) TryRun<TResult, TExpected>(
        Func<TResult> action,
        string userMessage)
        where TExpected : Exception
    {
        try
        {
            // 尝试执行
            var result = action();
            return (true, result);
        }
        catch (TExpected)
        {
            // 场景 1：预期错误 -> 打印用户定义的友好信息（不带堆栈，不带原异常信息）
            StdErr.Console.MarkupLine($"[red][[ERROR]][/] {Markup.Escape(userMessage)}");
            return (false, default);
        }
        catch (Exception ex)
        {
            // 场景 2：意外崩溃 -> 甩出完整堆栈供开发者调试
            StdErr.Console.MarkupLine("[red][[ERROR]][/] [white]Unexpected Error Happened:[/]");
            StdErr.Console.WriteException(ex, ExceptionFormats.ShortenEverything);
            return (false, default);
        }
    }

    /// <summary>
    /// 执行没有返回值的操作（重载版本）
    /// </summary>
    public static bool TryRun<TExpected>(
        Action action,
        string userMessage)
        where TExpected : Exception
    {
        try
        {
            action();
            return true;
        }
        catch (TExpected)
        {
            StdErr.Console.MarkupLine($"[red][[ERROR]][/] {Markup.Escape(userMessage)}");
            return false;
        }
        catch (Exception ex)
        {
            StdErr.Console.MarkupLine("[red][[ERROR]][/] [white]Unexpected Error Happened:[/]");
            StdErr.Console.WriteException(ex, ExceptionFormats.ShortenEverything);
            return false;
        }
    }
}
