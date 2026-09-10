using System.Collections.Concurrent;
using System.Threading.Channels;
using BeeCoding.Models;
using Microsoft.Extensions.Options;

namespace BeeCoding.Services.Judge;

public abstract record JudgeJob;

public sealed record SubmissionJob(int SubmissionId) : JudgeJob;

public sealed record BankSubmissionJob(int BankSubmissionId) : JudgeJob;

/// <summary>
/// An ad-hoc compile+run. <see cref="CorrelationId"/> ties the result (delivered via
/// <see cref="IJudgeJobSource.ReportRunResultAsync"/>) back to the awaiting producer — the
/// job no longer carries a live <c>TaskCompletionSource</c>, so it can cross a broker.
/// </summary>
public sealed record RunJob(
    string Language,
    string Code,
    string Stdin,
    int TimeLimitMs,
    int MemoryLimitKb,
    string CorrelationId) : JudgeJob;

/// <summary>Producer side — used by the controllers.</summary>
public interface IJudgeQueue
{
    /// <summary>Fire-and-forget: a submission is judged and its verdict pushed over SignalR later.</summary>
    ValueTask EnqueueAsync(JudgeJob job, CancellationToken ct = default);

    /// <summary>Request/response: enqueue an ad-hoc run and await its result.</summary>
    Task<RunResultDto> EnqueueRunAsync(
        string language, string code, string stdin, int timeLimitMs, int memoryLimitKb, CancellationToken ct = default);
}

/// <summary>Consumer side — used by <see cref="JudgeWorker"/>.</summary>
public interface IJudgeJobSource
{
    IAsyncEnumerable<JudgeJob> ReadAllAsync(CancellationToken ct);

    /// <summary>Deliver a completed run's result back to whoever called <c>EnqueueRunAsync</c>.</summary>
    ValueTask ReportRunResultAsync(RunJob job, RunResultDto result);
}

/// <summary>
/// Single-process queue: a bounded <see cref="Channel{T}"/> plus a local registry that
/// matches <see cref="RunJob.CorrelationId"/> to the awaiting <c>TaskCompletionSource</c>.
/// </summary>
public sealed class InProcessJudgeQueue : IJudgeQueue, IJudgeJobSource
{
    private readonly Channel<JudgeJob> _channel;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<RunResultDto>> _pending = new();

    public InProcessJudgeQueue(IOptions<JudgeOptions> options)
    {
        _channel = Channel.CreateBounded<JudgeJob>(new BoundedChannelOptions(options.Value.QueueCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
        });
    }

    public ValueTask EnqueueAsync(JudgeJob job, CancellationToken ct = default) =>
        _channel.Writer.WriteAsync(job, ct);

    public async Task<RunResultDto> EnqueueRunAsync(
        string language, string code, string stdin, int timeLimitMs, int memoryLimitKb, CancellationToken ct = default)
    {
        var id = Guid.NewGuid().ToString("N");
        var tcs = new TaskCompletionSource<RunResultDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = tcs;
        try
        {
            await _channel.Writer.WriteAsync(new RunJob(language, code, stdin, timeLimitMs, memoryLimitKb, id), ct);
            return await tcs.Task.WaitAsync(ct);
        }
        finally { _pending.TryRemove(id, out _); }
    }

    public IAsyncEnumerable<JudgeJob> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);

    public ValueTask ReportRunResultAsync(RunJob job, RunResultDto result)
    {
        if (_pending.TryGetValue(job.CorrelationId, out var tcs)) tcs.TrySetResult(result);
        return ValueTask.CompletedTask;
    }
}
