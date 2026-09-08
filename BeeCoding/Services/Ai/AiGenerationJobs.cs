using System.Collections.Concurrent;

namespace BeeCoding.Services.Ai;

/// <summary>
/// In-memory registry of running/finished <c>POST /api/ai/generate-problem</c> jobs so the
/// slow AI+judge work can run detached from the HTTP request. Entries self-prune after 20
/// minutes. Not persisted — a restart drops them (the successful ones are already saved to
/// the owner's bank).
/// </summary>
public sealed class AiGenerationJobs
{
    public sealed class Job
    {
        public Guid Id { get; init; }
        public int UserId { get; init; }
        public string Status { get; set; } = "running";   // running | done | error
        public object? Result { get; set; }               // Mapping.ToDto payload on success
        public string? Message { get; set; }              // failure message
        public string? CompilerOutput { get; set; }
        public string? Stderr { get; set; }
        public DateTime Created { get; } = DateTime.UtcNow;
        public DateTime? Finished { get; set; }
    }

    private readonly ConcurrentDictionary<Guid, Job> _jobs = new();

    public Job Create(int userId)
    {
        Prune();
        var j = new Job { Id = Guid.NewGuid(), UserId = userId };
        _jobs[j.Id] = j;
        return j;
    }

    public Job? Get(Guid id) => _jobs.TryGetValue(id, out var j) ? j : null;

    public int RunningFor(int userId) =>
        _jobs.Values.Count(j => j.UserId == userId && j.Status == "running");

    private void Prune()
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-20);
        foreach (var kv in _jobs)
            if ((kv.Value.Finished ?? kv.Value.Created) < cutoff)
                _jobs.TryRemove(kv.Key, out _);
    }
}
