using BeeLearn.Data;
using BeeLearn.Models;
using BeeLearn.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BeeLearn.Services.Judge;

public class JudgeWorker : BackgroundService
{
    private readonly JudgeQueue _queue;
    private readonly IServiceScopeFactory _scopes;
    private readonly NativeCompiler _compiler;
    private readonly NativeSandbox _sandbox;
    private readonly IBoardNotifier _notifier;
    private readonly NativeToolchain _toolchain;
    private readonly JudgeOptions _opt;
    private readonly ILogger<JudgeWorker> _log;

    public JudgeWorker(
        JudgeQueue queue, IServiceScopeFactory scopes, NativeCompiler compiler,
        NativeSandbox sandbox, IBoardNotifier notifier, NativeToolchain toolchain,
        IOptions<JudgeOptions> opt, ILogger<JudgeWorker> log)
    {
        _queue = queue;
        _scopes = scopes;
        _compiler = compiler;
        _sandbox = sandbox;
        _notifier = notifier;
        _toolchain = toolchain;
        _opt = opt.Value;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _toolchain.Initialize();
        using var slots = new SemaphoreSlim(_opt.MaxConcurrent);

        await foreach (var job in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            await slots.WaitAsync(stoppingToken);
            _ = Task.Run(async () =>
            {
                try { await DispatchAsync(job, stoppingToken); }
                catch (Exception ex)
                {
                    _log.LogError(ex, "judge job crashed");
                    if (job is RunJob rj)
                        rj.Completion.TrySetResult(new RunResultDto(false, "internal judge error", "", "", 0, 0, false, 0, 0));
                }
                finally { slots.Release(); }
            }, stoppingToken);
        }
    }

    private Task DispatchAsync(JudgeJob job, CancellationToken ct) => job switch
    {
        RunJob r => ProcessRunAsync(r, ct),
        SubmissionJob s => ProcessSubmissionAsync(s.SubmissionId, ct),
        _ => Task.CompletedTask,
    };

    private string NewWorkDir()
    {
        var dir = Path.Combine(_opt.WorkRoot, "job_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void CleanUp(string dir)
    {
        try { Directory.Delete(dir, recursive: true); } catch { }
    }

    // ---------- ad-hoc run ----------
    private async Task ProcessRunAsync(RunJob job, CancellationToken ct)
    {
        var dir = NewWorkDir();
        try
        {
            var compile = await _compiler.CompileAsync(dir, job.Language, job.Code, ct);
            if (!compile.Ok)
            {
                job.Completion.TrySetResult(new RunResultDto(false, compile.Output, "", "", 0, 0, false, 0, 0));
                return;
            }

            var exec = await _sandbox.ExecuteAsync(
                dir, compile.ExePath!, job.Stdin ?? "", job.TimeLimitMs, job.MemoryLimitKb, ct);
            var verdict = VerdictEvaluator.ClassifyRun(exec, job.TimeLimitMs, job.MemoryLimitKb);

            job.Completion.TrySetResult(new RunResultDto(
                true, compile.Output,
                exec.Stdout, exec.Stderr,
                exec.WallMs, exec.PeakKb,
                verdict == Verdict.TimeLimit,
                exec.ExitCode, exec.Signal));
        }
        finally { CleanUp(dir); }
    }

    // ---------- submission judging ----------
    private async Task ProcessSubmissionAsync(int submissionId, CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var sub = await db.Submissions
            .Include(s => s.Problem!).ThenInclude(p => p.TestCases)
            .Include(s => s.Problem!).ThenInclude(p => p.Board)
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.Id == submissionId, ct);
        if (sub is null || sub.Problem is null) return;

        var problem = sub.Problem;
        var boardId = problem.BoardId;
        var authorName = sub.User?.DisplayName ?? "student";

        sub.Status = SubmissionStatus.Running;
        await db.SaveChangesAsync(ct);
        await _notifier.ProgressChangedAsync(boardId, problem.Id, sub.UserId);

        var dir = NewWorkDir();
        try
        {
            var compile = await _compiler.CompileAsync(dir, problem.Language, sub.Code, ct);
            if (!compile.Ok)
            {
                Finish(sub, Verdict.CompileError, 0, 0, 0, compile.Output);
            }
            else
            {
                var tests = problem.TestCases.OrderBy(t => t.Position).ThenBy(t => t.Id).ToList();
                int totalPoints = Math.Max(1, tests.Sum(t => Math.Max(0, t.Points)));
                int passedPoints = 0, maxMs = 0, maxKb = 0;
                Verdict verdict = Verdict.Accepted;

                foreach (var t in tests)
                {
                    var exec = await _sandbox.ExecuteAsync(
                        dir, compile.ExePath!, t.Stdin ?? "", problem.TimeLimitMs, problem.MemoryLimitKb, ct);
                    maxMs = Math.Max(maxMs, exec.WallMs);
                    maxKb = Math.Max(maxKb, exec.PeakKb);

                    var cls = VerdictEvaluator.ClassifyRun(exec, problem.TimeLimitMs, problem.MemoryLimitKb);
                    if (cls != Verdict.Accepted) { verdict = cls; break; }
                    if (!VerdictEvaluator.OutputMatches(exec.Stdout, t.ExpectedStdout)) { verdict = Verdict.WrongAnswer; break; }
                    passedPoints += Math.Max(0, t.Points);
                }

                double score = verdict == Verdict.Accepted ? 1.0 : (double)passedPoints / totalPoints;
                Finish(sub, verdict, score, maxMs, maxKb, "");
            }

            await db.SaveChangesAsync(ct);
        }
        finally { CleanUp(dir); }

        await _notifier.ProgressChangedAsync(boardId, problem.Id, sub.UserId);
        await _notifier.WallChangedAsync(boardId);
        await _notifier.SubmissionResultAsync(sub.UserId,
            Mapping.ToDto(sub, sub.UserId, canSeeCode: true, authorName));
    }

    private static void Finish(Submission sub, Verdict v, double score, int ms, int kb, string compilerOut)
    {
        sub.Status = SubmissionStatus.Done;
        sub.Verdict = v;
        sub.Score = score;
        sub.RuntimeMs = ms;
        sub.MemoryKb = kb;
        sub.CompilerOutput = compilerOut;
        sub.JudgedAt = DateTime.UtcNow;
    }
}
