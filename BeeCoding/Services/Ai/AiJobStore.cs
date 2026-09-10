using System.Collections.Concurrent;

namespace BeeCoding.Services.Ai;

/// <summary>A detached generate/regenerate-problem job (the client polls its status).</summary>
public sealed class AiJob
{
    public string Id { get; init; } = "";
    public int UserId { get; init; }
    public string Status { get; set; } = "running";   // running | done | error
    public object? Result { get; set; }               // Mapping.ToDto payload on success
    public string? Message { get; set; }
    public string? CompilerOutput { get; set; }
    public string? Stderr { get; set; }
    public DateTime Created { get; init; } = DateTime.UtcNow;
    public DateTime? Finished { get; set; }
}

/// <summary>
/// Registry of running/finished AI problem-generation jobs so the slow AI+judge work can
/// run detached from the HTTP request. Ephemeral (entries expire after ~30 min) — a lost
/// job just fails the poll; successful results are already saved to the owner's bank.
/// The in-memory implementation is per-process; the Redis one is shared across web replicas.
/// </summary>
public interface IAiJobStore
{
    Task<AiJob> CreateAsync(int userId);
    Task<AiJob?> GetAsync(string id);
    Task<int> RunningForAsync(int userId);
    /// <summary>Mark done with a result — no-op if the job is no longer "running".</summary>
    Task CompleteAsync(string id, object result);
    /// <summary>Mark failed — no-op if the job is no longer "running".</summary>
    Task FailAsync(string id, string message, string? compilerOutput = null, string? stderr = null);

    internal static readonly TimeSpan Ttl = TimeSpan.FromMinutes(30);
}

/// <summary>Per-process implementation.</summary>
public sealed class InMemoryAiJobStore : IAiJobStore
{
    private readonly ConcurrentDictionary<string, AiJob> _jobs = new();

    public Task<AiJob> CreateAsync(int userId)
    {
        Prune();
        var j = new AiJob { Id = Guid.NewGuid().ToString("N"), UserId = userId };
        _jobs[j.Id] = j;
        return Task.FromResult(j);
    }

    public Task<AiJob?> GetAsync(string id) =>
        Task.FromResult(_jobs.TryGetValue(id, out var j) ? j : null);

    public Task<int> RunningForAsync(int userId) =>
        Task.FromResult(_jobs.Values.Count(j => j.UserId == userId && j.Status == "running"));

    public Task CompleteAsync(string id, object result)
    {
        if (_jobs.TryGetValue(id, out var j) && j.Status == "running")
        {
            j.Result = result; j.Status = "done"; j.Finished = DateTime.UtcNow;
        }
        return Task.CompletedTask;
    }

    public Task FailAsync(string id, string message, string? compilerOutput = null, string? stderr = null)
    {
        if (_jobs.TryGetValue(id, out var j) && j.Status == "running")
        {
            j.Message = message; j.CompilerOutput = compilerOutput; j.Stderr = stderr;
            j.Status = "error"; j.Finished = DateTime.UtcNow;
        }
        return Task.CompletedTask;
    }

    private void Prune()
    {
        var cutoff = DateTime.UtcNow - IAiJobStore.Ttl;
        foreach (var kv in _jobs)
            if ((kv.Value.Finished ?? kv.Value.Created) < cutoff)
                _jobs.TryRemove(kv.Key, out _);
    }
}
