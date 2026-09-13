using BeeCoding.Data;
using Microsoft.EntityFrameworkCore;

namespace BeeCoding.Services;

/// <summary>
/// Figures out which organization's AI settings (see AiRuntimeSettings) apply to an
/// AI-invoking action. A board-scoped action (a hint on a board problem, or in a live
/// lecturing session) uses that board's own OrganizationId. A bank-scoped action (problem
/// generation, practice-mode hints on a bank problem with no board) has no board to anchor
/// to, so it falls back to the acting user's own organization — only when unambiguous
/// (a member of exactly one); a user in zero or several organizations just gets the
/// platform default there, same as before this feature existed.
/// </summary>
public class OrgResolver(AppDbContext db)
{
    private readonly AppDbContext _db = db;

    public Task<int?> ForBoardSlugAsync(string slug, CancellationToken ct = default) =>
        _db.Boards.Where(b => b.Slug == slug).Select(b => b.OrganizationId).FirstOrDefaultAsync(ct);

    public Task<int?> ForBoardIdAsync(int boardId, CancellationToken ct = default) =>
        _db.Boards.Where(b => b.Id == boardId).Select(b => b.OrganizationId).FirstOrDefaultAsync(ct);

    public Task<int?> ForBoardProblemIdAsync(int problemId, CancellationToken ct = default) =>
        _db.Problems.Where(p => p.Id == problemId).Select(p => p.Board!.OrganizationId).FirstOrDefaultAsync(ct);

    public async Task<int?> ForUserAsync(int userId, CancellationToken ct = default)
    {
        var orgIds = await _db.OrganizationMemberships.Where(m => m.UserId == userId)
            .Select(m => m.OrganizationId).ToListAsync(ct);
        return orgIds.Count == 1 ? orgIds[0] : null;
    }

    /// <summary>Board problem -> that board's org; board slug (live session) -> that board's
    /// org; bank-only (practice) -> the user's own org, if unambiguous.</summary>
    public async Task<int?> ForHintAsync(int? problemId, string? boardSlug, int userId, CancellationToken ct = default)
    {
        if (problemId is int p) return await ForBoardProblemIdAsync(p, ct);
        if (!string.IsNullOrWhiteSpace(boardSlug)) return await ForBoardSlugAsync(boardSlug, ct);
        return await ForUserAsync(userId, ct);
    }
}
