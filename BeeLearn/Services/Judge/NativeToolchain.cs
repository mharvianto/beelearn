using System.Diagnostics;
using Microsoft.Extensions.Options;

namespace BeeLearn.Services.Judge;

/// <summary>
/// Locates gcc/g++, builds the small C "runner" helper (fork + setrlimit + wait4),
/// and probes whether bubblewrap can create namespaces in this environment.
/// </summary>
public class NativeToolchain
{
    private readonly ILogger<NativeToolchain> _log;
    public JudgeOptions Options { get; }

    public string GccPath { get; private set; } = "gcc";
    public string GppPath { get; private set; } = "g++";
    public string RunnerPath { get; private set; } = "";
    public bool BwrapUsable { get; private set; }
    public string BwrapPath { get; private set; } = "bwrap";

    public NativeToolchain(IOptions<JudgeOptions> options, ILogger<NativeToolchain> log)
    {
        Options = options.Value;
        _log = log;
    }

    private bool _initialized;

    public void Initialize()
    {
        if (_initialized) return;
        _initialized = true;
        Directory.CreateDirectory(Options.WorkRoot);

        GccPath = Which("gcc") ?? throw new InvalidOperationException("gcc not found on PATH");
        GppPath = Which("g++") ?? throw new InvalidOperationException("g++ not found on PATH");

        // Build the runner helper.
        var srcPath = Path.Combine(Options.WorkRoot, "runner.c");
        RunnerPath = Path.Combine(Options.WorkRoot, "runner");
        File.WriteAllText(srcPath, RunnerSource.C);

        if (!TryCompileRunner(srcPath, RunnerPath, staticLink: true) &&
            !TryCompileRunner(srcPath, RunnerPath, staticLink: false))
        {
            throw new InvalidOperationException("Failed to compile the judge runner helper");
        }
        _log.LogInformation("Judge runner built at {Path}", RunnerPath);

        var bw = Which("bwrap");
        if (bw is not null)
        {
            BwrapPath = bw;
            BwrapUsable = ProbeBwrap(bw);
        }
        _log.LogInformation(
            "Sandbox mode: {Mode} (bwrap {State}). Time/memory limits are always enforced via rlimits.",
            BwrapUsable ? "bubblewrap + rlimits" : "rlimits only",
            bw is null ? "not installed" : BwrapUsable ? "usable" : "present but namespaces are blocked");
    }

    private bool TryCompileRunner(string src, string outPath, bool staticLink)
    {
        try
        {
            var args = staticLink
                ? $"-O2 -static -o \"{outPath}\" \"{src}\""
                : $"-O2 -o \"{outPath}\" \"{src}\"";
            using var p = Process.Start(new ProcessStartInfo(GccPath, args)
            {
                RedirectStandardError = true,
                UseShellExecute = false,
            })!;
            var err = p.StandardError.ReadToEnd();
            p.WaitForExit(30_000);
            if (p.HasExited && p.ExitCode == 0 && File.Exists(outPath)) return true;
            _log.LogWarning("runner compile ({Link}) failed: {Err}", staticLink ? "static" : "dynamic", err);
            return false;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "runner compile ({Link}) threw", staticLink ? "static" : "dynamic");
            return false;
        }
    }

    private bool ProbeBwrap(string bwrapPath)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo(bwrapPath,
                "--ro-bind / / --dev /dev --proc /proc --unshare-all /bin/true")
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            })!;
            p.WaitForExit(5_000);
            return p.HasExited && p.ExitCode == 0;
        }
        catch { return false; }
    }

    private static string? Which(string exe)
    {
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(':'))
        {
            if (string.IsNullOrEmpty(dir)) continue;
            var candidate = Path.Combine(dir, exe);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }
}
