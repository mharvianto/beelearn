using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using BeeCoding.Models;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace BeeCoding.Services.Judge;

/// <summary>
/// Broker-backed queue so the judge can run as a separate deployment.
///  • jobs      — a Redis list, RPUSH by producers / LPOP by the judge worker
///  • run reply — a pub/sub channel; the judge PUBLISHes {id,result}, the producer that is
///                awaiting that correlation id resolves its local TaskCompletionSource
///
/// Caveat: LPOP is at-most-once — a job popped by a judge that then crashes is lost. For
/// production use Redis Streams + consumer groups (XREADGROUP/XACK) instead; the interface
/// does not change. Submission jobs must also be idempotent (re-judge is safe here because
/// ProcessSubmissionAsync overwrites the verdict).
/// </summary>
public sealed class RedisJudgeQueue : IJudgeQueue, IJudgeJobSource, IAsyncDisposable
{
    private readonly IDatabase _db;
    private readonly ISubscriber _sub;
    private readonly JudgeQueueOptions _o;
    private readonly ILogger<RedisJudgeQueue> _log;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<RunResultDto>> _pending = new();
    private readonly RedisChannel _resultsChannel;
    private readonly string _jobsKey;

    public RedisJudgeQueue(IConnectionMultiplexer mux, IOptions<JudgeOptions> judgeOptions, ILogger<RedisJudgeQueue> log)
    {
        _o = judgeOptions.Value.Queue;
        _log = log;
        _db = mux.GetDatabase();
        _sub = mux.GetSubscriber();
        _jobsKey = _o.KeyPrefix + "jobs";
        _resultsChannel = RedisChannel.Literal(_o.KeyPrefix + "run-results");

        _sub.Subscribe(_resultsChannel, (_, msg) =>
        {
            try
            {
                var node = JsonNode.Parse((string)msg!)!;
                var id = (string)node["id"]!;
                var result = node["result"].Deserialize<RunResultDto>(Json);
                if (result is not null && _pending.TryGetValue(id, out var tcs)) tcs.TrySetResult(result);
            }
            catch (Exception ex) { _log.LogWarning(ex, "bad judge run-result message"); }
        });
    }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // ---- producer -----------------------------------------------------------
    public async ValueTask EnqueueAsync(JudgeJob job, CancellationToken ct = default) =>
        await _db.ListRightPushAsync(_jobsKey, Encode(job));

    public async Task<RunResultDto> EnqueueRunAsync(
        string language, string code, string stdin, int timeLimitMs, int memoryLimitKb, CancellationToken ct = default)
    {
        var id = Guid.NewGuid().ToString("N");
        var tcs = new TaskCompletionSource<RunResultDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = tcs;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(5, _o.RunReplyTimeoutSeconds)));
        try
        {
            await _db.ListRightPushAsync(_jobsKey,
                Encode(new RunJob(language, code, stdin, timeLimitMs, memoryLimitKb, id)));
            return await tcs.Task.WaitAsync(timeout.Token);
        }
        finally { _pending.TryRemove(id, out _); }
    }

    // ---- consumer (judge worker) -----------------------------------------------------------
    public async IAsyncEnumerable<JudgeJob> ReadAllAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            RedisValue v;
            try { v = await _db.ListLeftPopAsync(_jobsKey); }
            catch (Exception ex) { _log.LogWarning(ex, "judge queue pop failed"); await Delay(ct); continue; }

            if (v.IsNullOrEmpty) { await Delay(ct); continue; }

            JudgeJob? job = null;
            try { job = Decode(v!); }
            catch (Exception ex) { _log.LogWarning(ex, "undecodable judge job dropped: {Raw}", v); }
            if (job is not null) yield return job;
        }
    }

    public ValueTask ReportRunResultAsync(RunJob job, RunResultDto result)
    {
        var payload = new JsonObject
        {
            ["id"] = job.CorrelationId,
            ["result"] = JsonSerializer.SerializeToNode(result, Json),
        };
        return new ValueTask(_sub.PublishAsync(_resultsChannel, payload.ToJsonString()));
    }

    private async Task Delay(CancellationToken ct)
    {
        try { await Task.Delay(Math.Max(50, _o.PollMs), ct); } catch (OperationCanceledException) { }
    }

    // ---- (de)serialisation of the JudgeJob hierarchy -----------------------------
    private static string Encode(JudgeJob job) => job switch
    {
        RunJob r => new JsonObject { ["kind"] = "run", ["data"] = JsonSerializer.SerializeToNode(r, Json) }.ToJsonString(),
        SubmissionJob s => new JsonObject { ["kind"] = "submission", ["id"] = s.SubmissionId }.ToJsonString(),
        BankSubmissionJob b => new JsonObject { ["kind"] = "bank", ["id"] = b.BankSubmissionId }.ToJsonString(),
        _ => throw new NotSupportedException(job.GetType().Name),
    };

    private static JudgeJob Decode(string raw)
    {
        var n = JsonNode.Parse(raw)!;
        return (string)n["kind"]! switch
        {
            "run" => n["data"].Deserialize<RunJob>(Json)!,
            "submission" => new SubmissionJob((int)n["id"]!),
            "bank" => new BankSubmissionJob((int)n["id"]!),
            var k => throw new NotSupportedException(k),
        };
    }

    public async ValueTask DisposeAsync()
    {
        try { await _sub.UnsubscribeAsync(_resultsChannel); } catch { /* ignore */ }
    }
}
