using BeeLearn.Models;

namespace BeeLearn.Services.Judge;

public static class VerdictEvaluator
{
    private const int SIGABRT = 6;
    private const int SIGSEGV = 11;
    private const int SIGKILL = 9;
    private const int SIGXCPU = 24;
    private const int SIGXFSZ = 25;

    /// <summary>Verdict for a single test-case execution (no output comparison here).</summary>
    public static Verdict ClassifyRun(ExecResult r, int timeLimitMs, int memoryLimitKb)
    {
        if (r.TimedOut || r.Signal is SIGXCPU or SIGKILL || r.WallMs >= timeLimitMs + 400)
            return Verdict.TimeLimit;

        // RLIMIT_AS is a *virtual address space* cap, so an allocation failure often shows
        // only a modest RSS. Trust the allocator's own words first, then RSS as a fallback.
        bool allocatorComplained =
            r.Stderr.Contains("bad_alloc", StringComparison.OrdinalIgnoreCase) ||
            r.Stderr.Contains("out of memory", StringComparison.OrdinalIgnoreCase) ||
            r.Stderr.Contains("std::length_error", StringComparison.OrdinalIgnoreCase) ||
            r.Stderr.Contains("Cannot allocate memory", StringComparison.OrdinalIgnoreCase);
        bool rssNearLimit = r.PeakKb * 1024L >= memoryLimitKb * 1024L * 70 / 100;

        if ((r.Signal is SIGABRT or SIGSEGV) && (allocatorComplained || rssNearLimit))
            return Verdict.MemoryLimit;

        if (r.Signal != 0 || r.ExitCode != 0 || r.StatsMissing)
            return Verdict.RuntimeError;

        return Verdict.Accepted;
    }

    /// <summary>Right-trim each line, drop trailing blank lines. Used for output comparison.</summary>
    public static string Normalize(string s)
    {
        var lines = s.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        int end = lines.Length;
        while (end > 0 && lines[end - 1].TrimEnd().Length == 0) end--;
        return string.Join('\n', lines.Take(end).Select(l => l.TrimEnd()));
    }

    public static bool OutputMatches(string actual, string expected) =>
        Normalize(actual) == Normalize(expected);
}
