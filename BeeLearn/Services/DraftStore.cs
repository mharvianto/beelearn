using System.Collections.Concurrent;

namespace BeeLearn.Services;

public record Draft(int BoardId, int ProblemId, int UserId, string AuthorName, string Code, DateTime UpdatedAt);

/// <summary>
/// In-memory, ephemeral store of students' live editor buffers, streamed to staff so a
/// teacher can watch work-in-progress without the student running or submitting.
/// Not persisted — cleared on restart.
/// </summary>
public class DraftStore
{
    private readonly ConcurrentDictionary<(int problemId, int userId), Draft> _drafts = new();

    public Draft Set(int boardId, int problemId, int userId, string authorName, string code)
    {
        var d = new Draft(boardId, problemId, userId, authorName, code, DateTime.UtcNow);
        _drafts[(problemId, userId)] = d;
        return d;
    }

    public IReadOnlyList<Draft> ForBoard(int boardId) =>
        _drafts.Values.Where(d => d.BoardId == boardId).ToList();

    public void RemoveForBoard(int boardId)
    {
        foreach (var key in _drafts.Where(kv => kv.Value.BoardId == boardId).Select(kv => kv.Key).ToList())
            _drafts.TryRemove(key, out _);
    }
}
