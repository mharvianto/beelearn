namespace BeeCoding.Services.Ai;

/// <summary>
/// In-memory cache of admin-tunable AI settings: a global pause ("kill switch") + default
/// daily request quota per role, plus per-user overrides (custom quota and/or an outright
/// ban). Populated from the DB at startup and kept in sync by AdminUiController's writes,
/// so the hot path (one check per AI request) never needs a DB round trip for the global
/// settings — only the per-user override lookup, which is an in-memory dictionary hit.
///
/// Caveat: per-instance cache, same as AdminAccess — in a scale-out deployment a change on
/// one instance isn't visible on the others until they restart.
/// </summary>
public class AiRuntimeSettings
{
    private volatile Global _global = new(false, null, 20, 50);
    private volatile Dictionary<int, Override> _overrides = new();

    public bool Paused => _global.Paused;
    public string? PausedReason => _global.PausedReason;
    public int DailyQuotaStudent => _global.QuotaStudent;
    public int DailyQuotaTeacher => _global.QuotaTeacher;

    public void SetGlobal(bool paused, string? reason, int quotaStudent, int quotaTeacher) =>
        _global = new Global(paused, string.IsNullOrWhiteSpace(reason) ? null : reason, quotaStudent, quotaTeacher);

    public void SetOverrides(IEnumerable<(int UserId, int? Quota, bool Banned)> rows) =>
        _overrides = rows.ToDictionary(r => r.UserId, r => new Override(r.Quota, r.Banned));

    public void SetOverride(int userId, int? quota, bool banned)
    {
        var next = new Dictionary<int, Override>(_overrides) { [userId] = new(quota, banned) };
        _overrides = next;
    }

    public void ClearOverride(int userId)
    {
        if (!_overrides.ContainsKey(userId)) return;
        var next = new Dictionary<int, Override>(_overrides);
        next.Remove(userId);
        _overrides = next;
    }

    public Override? OverrideFor(int userId) => _overrides.TryGetValue(userId, out var o) ? o : null;

    /// <summary>Effective (banned, dailyQuota) for a user given their Teacher/Student role.</summary>
    public (bool Banned, int Quota) Effective(int userId, string role)
    {
        var roleDefault = string.Equals(role, "Teacher", StringComparison.OrdinalIgnoreCase) ? _global.QuotaTeacher : _global.QuotaStudent;
        var o = OverrideFor(userId);
        return o is null ? (false, roleDefault) : (o.Banned, o.Quota ?? roleDefault);
    }

    private record Global(bool Paused, string? PausedReason, int QuotaStudent, int QuotaTeacher);
    public record Override(int? Quota, bool Banned);
}
