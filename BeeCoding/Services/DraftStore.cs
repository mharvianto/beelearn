using System.Collections.Concurrent;

namespace BeeCoding.Services;

public record Draft(int BoardId, int ProblemId, int UserId, string AuthorName, string Code, DateTime UpdatedAt);

/// <summary>
/// Ephemeral store of students' live editor buffers, streamed to staff so a teacher can
/// watch work-in-progress without the student running or submitting. Not durable — an
/// in-memory implementation is per-process; the Redis one is shared across web replicas.
/// </summary>
public interface IDraftStore
{
    ValueTask<Draft> SetAsync(int boardId, int problemId, int userId, string authorName, string code);
    ValueTask<IReadOnlyList<Draft>> ForBoardAsync(int boardId);
    ValueTask RemoveForBoardAsync(int boardId);
}

/// <summary>Per-process implementation. Fine for a single node; loses data on restart.</summary>
public sealed class InMemoryDraftStore : IDraftStore
{
    private readonly ConcurrentDictionary<(int problemId, int userId), Draft> _drafts = new();

    public ValueTask<Draft> SetAsync(int boardId, int problemId, int userId, string authorName, string code)
    {
        var d = new Draft(boardId, problemId, userId, authorName, code, DateTime.UtcNow);
        _drafts[(problemId, userId)] = d;
        return ValueTask.FromResult(d);
    }

    public ValueTask<IReadOnlyList<Draft>> ForBoardAsync(int boardId) =>
        ValueTask.FromResult<IReadOnlyList<Draft>>(_drafts.Values.Where(d => d.BoardId == boardId).ToList());

    public ValueTask RemoveForBoardAsync(int boardId)
    {
        foreach (var key in _drafts.Where(kv => kv.Value.BoardId == boardId).Select(kv => kv.Key).ToList())
            _drafts.TryRemove(key, out _);
        return ValueTask.CompletedTask;
    }
}
