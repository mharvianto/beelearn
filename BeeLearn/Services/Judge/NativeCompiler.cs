using System.Diagnostics;
using System.Text;

namespace BeeLearn.Services.Judge;

public record CompileResult(bool Ok, string Output, string? ExePath);

public class NativeCompiler
{
    private readonly NativeToolchain _tc;
    private readonly ILogger<NativeCompiler> _log;

    public NativeCompiler(NativeToolchain tc, ILogger<NativeCompiler> log)
    {
        _tc = tc;
        _log = log;
    }

    public static string SourceName(string language) => Normalize(language) == "c" ? "main.c" : "main.cpp";

    public static string Normalize(string language) =>
        (language ?? "").Trim().ToLowerInvariant() is "c" ? "c" : "cpp";

    public async Task<CompileResult> CompileAsync(string workDir, string language, string code, CancellationToken ct)
    {
        var lang = Normalize(language);
        var srcPath = Path.Combine(workDir, SourceName(lang));
        var exePath = Path.Combine(workDir, "prog");
        await File.WriteAllTextAsync(srcPath, code, ct);

        var (exe, args) = lang == "c"
            ? (_tc.GccPath, $"-O2 -std=gnu11 -pipe -o \"{exePath}\" \"{srcPath}\" -lm")
            : (_tc.GppPath, $"-O2 -std=gnu++17 -pipe -o \"{exePath}\" \"{srcPath}\"");

        var psi = new ProcessStartInfo(exe, args)
        {
            WorkingDirectory = workDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var p = new Process { StartInfo = psi };
        var sb = new StringBuilder();
        p.OutputDataReceived += (_, e) => { if (e.Data != null) sb.AppendLine(e.Data); };
        p.ErrorDataReceived += (_, e) => { if (e.Data != null) sb.AppendLine(e.Data); };

        try
        {
            p.Start();
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(_tc.Options.CompileTimeoutMs);
            await p.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            TryKill(p);
            return new CompileResult(false, sb.ToString() + "\n[compilation timed out]", null);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "compiler launch failed");
            return new CompileResult(false, "internal compiler error", null);
        }

        var ok = p.ExitCode == 0 && File.Exists(exePath);
        var output = sb.ToString().Replace(workDir + "/", "").Replace(workDir, "").Trim();
        return new CompileResult(ok, output, ok ? exePath : null);
    }

    private static void TryKill(Process p)
    {
        try { if (!p.HasExited) p.Kill(entireProcessTree: true); } catch { }
    }
}
