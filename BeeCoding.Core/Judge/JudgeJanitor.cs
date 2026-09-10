using Microsoft.Extensions.Options;

namespace BeeCoding.Services.Judge;

/// <summary>
/// Sweeps stale <c>job_*</c> scratch directories out of <see cref="JudgeOptions.WorkRoot"/>.
/// Each job normally deletes its own dir in a finally block; this catches the ones left
/// behind when the process was killed mid-run.
/// </summary>
public sealed class JudgeJanitor(IOptions<JudgeOptions> opt, ILogger<JudgeJanitor> log) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan MaxAge = TimeSpan.FromHours(1);

    private readonly JudgeOptions _opt = opt.Value;
    private readonly ILogger<JudgeJanitor> _log = log;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            Sweep();
            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private void Sweep()
    {
        try
        {
            if (!Directory.Exists(_opt.WorkRoot)) return;
            var cutoff = DateTime.UtcNow - MaxAge;
            int removed = 0;
            foreach (var dir in Directory.EnumerateDirectories(_opt.WorkRoot, "job_*"))
            {
                try
                {
                    if (Directory.GetLastWriteTimeUtc(dir) < cutoff)
                    {
                        Directory.Delete(dir, recursive: true);
                        removed++;
                    }
                }
                catch { /* dir in use or vanished — skip */ }
            }
            if (removed > 0) _log.LogInformation("Judge janitor removed {Count} stale scratch dir(s)", removed);
        }
        catch (Exception ex)
        {
            _log.LogDebug(ex, "judge janitor sweep failed");
        }
    }
}
