namespace BeeCoding.Services.Ai;

/// <summary>
/// In-memory cache of admin-tunable AI settings: a platform-wide pause ("kill switch") +
/// default daily request quota per role, optionally overridden per organization, plus
/// per-user overrides (custom quota and/or an outright ban). Populated from the DB at
/// startup and kept in sync by AdminUiController's/OrgAdminController's writes, so the hot
/// path (one check per AI request) never needs a DB round trip for the pause/quota —
/// only the per-user override lookup, which is an in-memory dictionary hit.
///
/// The platform pause always wins over an organization's own settings — an org can only
/// ever restrict itself further, never override the platform kill switch.
///
/// Caveat: per-instance cache, same as AdminAccess — in a scale-out deployment a change on
/// one instance isn't visible on the others until they restart.
/// </summary>
public class AiRuntimeSettings
{
    private volatile Global _platform = new(false, null, 20, 50);
    private volatile Dictionary<int, Global> _orgs = new();
    private volatile Dictionary<int, Override> _overrides = new();

    public bool Paused => _platform.Paused;
    public string? PausedReason => _platform.PausedReason;
    public int DailyQuotaStudent => _platform.QuotaStudent;
    public int DailyQuotaTeacher => _platform.QuotaTeacher;

    private static string? Norm(string? reason) => string.IsNullOrWhiteSpace(reason) ? null : reason;

    public void SetGlobal(bool paused, string? reason, int quotaStudent, int quotaTeacher) =>
        _platform = new Global(paused, Norm(reason), quotaStudent, quotaTeacher);

    public void SetOrgs(IEnumerable<(int OrganizationId, bool Paused, string? Reason, int QuotaStudent, int QuotaTeacher)> rows) =>
        _orgs = rows.ToDictionary(r => r.OrganizationId, r => new Global(r.Paused, Norm(r.Reason), r.QuotaStudent, r.QuotaTeacher));

    public void SetOrg(int organizationId, bool paused, string? reason, int quotaStudent, int quotaTeacher)
    {
        var next = new Dictionary<int, Global>(_orgs) { [organizationId] = new(paused, Norm(reason), quotaStudent, quotaTeacher) };
        _orgs = next;
    }

    public void ClearOrg(int organizationId)
    {
        if (!_orgs.ContainsKey(organizationId)) return;
        var next = new Dictionary<int, Global>(_orgs);
        next.Remove(organizationId);
        _orgs = next;
    }

    public Global? OrgSettings(int organizationId) => _orgs.TryGetValue(organizationId, out var g) ? g : null;

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

    /// <summary>Effective (blocked, dailyQuota, reason-if-blocked) for a user with the given
    /// role, acting within <paramref name="organizationId"/> (null = no org context, so the
    /// platform default applies directly). The platform pause always wins; an org's own
    /// pause blocks only that org.</summary>
    public (bool Blocked, int Quota, string? Reason) Effective(int userId, string role, int? organizationId)
    {
        if (_platform.Paused)
            return (true, 0, _platform.PausedReason ?? "AI is temporarily paused by an admin.");

        var scope = organizationId is { } orgId && _orgs.TryGetValue(orgId, out var org) ? org : _platform;
        if (scope.Paused)
            return (true, 0, scope.PausedReason ?? "AI is temporarily paused for your organization.");

        var roleDefault = string.Equals(role, "Teacher", StringComparison.OrdinalIgnoreCase) ? scope.QuotaTeacher : scope.QuotaStudent;
        var o = OverrideFor(userId);
        if (o is { Banned: true }) return (true, 0, "Your AI access has been disabled by an admin.");
        return (false, o?.Quota ?? roleDefault, null);
    }

    public record Global(bool Paused, string? PausedReason, int QuotaStudent, int QuotaTeacher);
    public record Override(int? Quota, bool Banned);
}
