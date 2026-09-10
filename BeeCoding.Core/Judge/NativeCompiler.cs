using System.Diagnostics;
using System.Text;

namespace BeeCoding.Services.Judge;

public record CompileResult(bool Ok, string Output, string? ExePath);

public class NativeCompiler(NativeToolchain tc, ILogger<NativeCompiler> log)
{
    private readonly NativeToolchain _tc = tc;
    private readonly ILogger<NativeCompiler> _log = log;

    public static string SourceName(string language) => Normalize(language) == "c" ? "main.c" : "main.cpp";

    public static string Normalize(string language) =>
        (language ?? "").Trim().ToLowerInvariant() is "c" ? "c" : "cpp";

    public async Task<CompileResult> CompileAsync(string workDir, string language, string code, CancellationToken ct)
    {
        var lang = Normalize(language);
        var srcPath = Path.Combine(workDir, SourceName(lang));
        var exePath = Path.Combine(workDir, "prog");
        await File.WriteAllTextAsync(srcPath, code, ct);

        var (compilerPath, compilerArgs) = lang == "c"
            ? (_tc.GccPath, $"-O2 -std=gnu11 -pipe -o \"{exePath}\" \"{srcPath}\" -lm")
            : (_tc.GppPath, $"-O2 -std=gnu++17 -pipe -o \"{exePath}\" \"{srcPath}\"");

        // The compiler processes fully untrusted source. Jail it the same way runs are
        // jailed when bwrap is usable, so `#include "/etc/passwd"` (or #embed) can't read
        // host files and leak them back in the compile-error output.
        ProcessStartInfo psi;
        if (_tc.BwrapUsable)
        {
            var b = new StringBuilder();
            b.Append("--unshare-all --die-with-parent --new-session --clearenv ");
            b.Append("--setenv PATH /usr/bin:/bin --setenv TMPDIR /tmp ");
            b.Append("--ro-bind /usr /usr --tmpfs /tmp --proc /proc --dev /dev ");
            b.Append("--symlink usr/lib /lib --symlink usr/lib64 /lib64 ");
            b.Append("--symlink usr/bin /bin --symlink usr/sbin /sbin ");
            b.Append($"--bind \"{workDir}\" \"{workDir}\" --chdir \"{workDir}\" ");
            b.Append($"-- \"{compilerPath}\" {compilerArgs}");
            psi = new ProcessStartInfo(_tc.BwrapPath, b.ToString());
        }
        else
        {
            psi = new ProcessStartInfo(compilerPath, compilerArgs);
        }

        psi.WorkingDirectory = workDir;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        psi.UseShellExecute = false;

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
