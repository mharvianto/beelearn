using System.Collections.Concurrent;

namespace BeeLearn.Services;

public record PresenceUser(int UserId, string DisplayName, bool IsStaff);

/// <summary>In-memory map of live SignalR connections per board.</summary>
public class PresenceTracker
{
    private record Entry(int BoardId, int UserId, string DisplayName, bool IsStaff);

    private readonly ConcurrentDictionary<string, Entry> _byConnection = new();

    public void Add(string connectionId, int boardId, int userId, string name, bool isStaff) =>
        _byConnection[connectionId] = new Entry(boardId, userId, name, isStaff);

    public int? Remove(string connectionId) =>
        _byConnection.TryRemove(connectionId, out var e) ? e.BoardId : null;

    public IReadOnlyList<PresenceUser> ForBoard(int boardId) =>
        _byConnection.Values
            .Where(e => e.BoardId == boardId)
            .GroupBy(e => e.UserId)
            .Select(g => new PresenceUser(g.Key, g.First().DisplayName, g.First().IsStaff))
            .OrderBy(u => u.DisplayName)
            .ToList();
}
