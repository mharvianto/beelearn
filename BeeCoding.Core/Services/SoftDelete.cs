namespace BeeCoding.Services;

/// <summary>
/// Shared policy for soft-deleted rows (User/Board/Problem/BankProblem): a delete just sets
/// <c>DeletedAt</c>, and the caller has a short window to undo it. There is no background
/// purge — past that window the row simply stays hidden (see the per-entity query filters
/// in AppDbContext for Board/Problem/BankProblem; User has no query filter, see its
/// DeletedAt doc comment).
/// </summary>
public static class SoftDelete
{
    public static readonly TimeSpan UndoWindow = TimeSpan.FromSeconds(30);

    /// <summary>True if the row was deleted and is still within the undo window.</summary>
    public static bool CanRestore(DateTime? deletedAt) =>
        deletedAt is { } at && DateTime.UtcNow - at <= UndoWindow;
}
