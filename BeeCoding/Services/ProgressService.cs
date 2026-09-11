using BeeCoding.Data;
using BeeCoding.Models;
using Microsoft.EntityFrameworkCore;

namespace BeeCoding.Services;

public record ProgressDto(int Xp, int Level, int LevelStartXp, int NextLevelXp, int SolvedCount);

/// <summary>
/// XP / leveling. XP is awarded once per distinct problem the first time a student
/// fully solves it (Accepted, score 1.0). A bank problem and its board copies share
/// one key, so the same problem can't be farmed across boards.
/// </summary>
public class ProgressService(AppDbContext db)
{
    private readonly AppDbContext _db = db;

    public static int XpFor(ProblemLevel level) => level switch
    {
        ProblemLevel.Easy => 10,
        ProblemLevel.Hard => 40,
        _ => 20,
    };

    // Level L needs 25*L*(L-1) total XP: L1=0, L2=50, L3=150, L4=300, L5=500 ...
    public static int LevelStartXp(int level) => 25 * level * (level - 1);

    public static int LevelForXp(int xp) =>
        Math.Max(1, (int)Math.Floor((25 + Math.Sqrt(625 + 100.0 * Math.Max(0, xp))) / 50));

    public static string BankKey(int bankProblemId) => $"bank:{bankProblemId}";

    public static string KeyForBoardProblem(Problem p) =>
        p.SourceBankProblemId is int src ? BankKey(src) : $"board:{p.Id}";

    /// <summary>
    /// Record a solve if it's the user's first for this problem key; returns XP added (0 if already solved).
    /// </summary>
    public async Task<int> AwardSolveAsync(int userId, string problemKey, ProblemLevel level, CancellationToken ct = default)
    {
        if (await _db.SolveRecords.AnyAsync(r => r.UserId == userId && r.ProblemKey == problemKey, ct))
            return 0;

        int xp = XpFor(level);
        _db.SolveRecords.Add(new SolveRecord
        {
            UserId = userId,
            ProblemKey = problemKey,
            Level = level,
            XpAwarded = xp,
        });
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // lost the race on the unique index — someone else recorded it first
            return 0;
        }

        await _db.Users.Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.Xp, u => u.Xp + xp), ct);
        return xp;
    }

    public async Task<ProgressDto> GetAsync(int userId, CancellationToken ct = default)
    {
        var xp = await _db.Users.Where(u => u.Id == userId).Select(u => u.Xp).FirstOrDefaultAsync(ct);
        var solved = await _db.SolveRecords.CountAsync(r => r.UserId == userId, ct);
        int level = LevelForXp(xp);
        return new ProgressDto(xp, level, LevelStartXp(level), LevelStartXp(level + 1), solved);
    }
}
