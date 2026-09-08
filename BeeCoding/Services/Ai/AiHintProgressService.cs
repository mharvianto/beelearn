using BeeCoding.Data;
using BeeCoding.Models;
using Microsoft.EntityFrameworkCore;

namespace BeeCoding.Services.Ai;

/// <summary>
/// Tracks how many times a user has asked the AI tutor for a hint on the same problem
/// recently, so the tutor can escalate detail. Resets after a quiet gap.
/// </summary>
public class AiHintProgressService
{
    private readonly AppDbContext _db;
    public AiHintProgressService(AppDbContext db) => _db = db;

    private static readonly TimeSpan ResetAfter = TimeSpan.FromHours(6);
    public const int MaxLevel = 4;

    /// <summary>Bump the counter for (user, problem) and return the current hint level (1..MaxLevel).</summary>
    public async Task<int> BumpAsync(int userId, string problemKey, CancellationToken ct = default)
    {
        var row = await _db.AiHintProgresses
            .FirstOrDefaultAsync(x => x.UserId == userId && x.ProblemKey == problemKey, ct);
        var now = DateTime.UtcNow;

        if (row is null)
        {
            row = new AiHintProgress { UserId = userId, ProblemKey = problemKey, Count = 0 };
            _db.AiHintProgresses.Add(row);
        }
        else if (now - row.UpdatedAt > ResetAfter)
        {
            row.Count = 0;   // been a while — start gently again
        }

        row.Count += 1;
        row.UpdatedAt = now;
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            _db.ChangeTracker.Clear();
            var existing = await _db.AiHintProgresses.FirstAsync(x => x.UserId == userId && x.ProblemKey == problemKey, ct);
            if (now - existing.UpdatedAt > ResetAfter) existing.Count = 0;
            existing.Count += 1;
            existing.UpdatedAt = now;
            await _db.SaveChangesAsync(ct);
            return Math.Clamp(existing.Count, 1, MaxLevel);
        }
        return Math.Clamp(row.Count, 1, MaxLevel);
    }
}
