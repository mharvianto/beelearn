namespace BeeCoding.Services.Judge;

public class JudgeOptions
{
    /// <summary>Scratch directory for compiles + sandboxed runs.</summary>
    public string WorkRoot { get; set; } = Path.Combine(Path.GetTempPath(), "beecoding-judge");

    /// <summary>Parallel compile/run slots.</summary>
    public int MaxConcurrent { get; set; } = Math.Max(1, Environment.ProcessorCount / 2);

    public int CompileTimeoutMs { get; set; } = 10_000;

    public int QueueCapacity { get; set; } = 200;

    /// <summary>
    /// Fail startup if bubblewrap can't create namespaces. Set true in production so the
    /// judge never silently degrades to "rlimits only" (no filesystem/network isolation —
    /// student code could read the app's files or open outbound sockets).
    /// </summary>
    public bool RequireSandbox { get; set; }

    /// <summary>Extra wall-clock grace on top of a problem's time limit before the parent force-kills.</summary>
    public int HardWallBufferMs { get; set; } = 800;

    /// <summary>Max stdout/stderr bytes captured per run (rest is truncated).</summary>
    public int MaxOutputBytes { get; set; } = 256 * 1024;

    /// <summary>Ad-hoc "Run" defaults when no problem context is supplied.</summary>
    public int RunTimeLimitMs { get; set; } = 1000;
    public int RunMemoryLimitKb { get; set; } = 32_768;

    /// <summary>Per-user minimum spacing between run/submit requests.</summary>
    public int RateLimitMs { get; set; } = 1500;
}
