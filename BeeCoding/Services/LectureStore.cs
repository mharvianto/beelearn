using System.Collections.Concurrent;

namespace BeeCoding.Services;

public record Lecture(int BoardId, int ProblemId, string Code, string Language, string TeacherName, DateTime UpdatedAt, string Stdin = "");

/// <summary>
/// In-memory, ephemeral store of the teacher's live editor buffer per (board, problem) while
/// lecturing mode is on. Streamed read-only to students. Not persisted.
/// </summary>
public class LectureStore
{
    private readonly ConcurrentDictionary<(int boardId, int problemId), Lecture> _lectures = new();

    public Lecture Set(int boardId, int problemId, string code, string language, string teacherName, string stdin = "")
    {
        var l = new Lecture(boardId, problemId, code, language, teacherName, DateTime.UtcNow, stdin);
        _lectures[(boardId, problemId)] = l;
        return l;
    }

    public Lecture? Get(int boardId, int problemId) =>
        _lectures.TryGetValue((boardId, problemId), out var l) ? l : null;

    public void RemoveForBoard(int boardId)
    {
        foreach (var key in _lectures.Where(kv => kv.Value.BoardId == boardId).Select(kv => kv.Key).ToList())
            _lectures.TryRemove(key, out _);
    }
}
