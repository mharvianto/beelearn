namespace BeeCoding.Services.Ai;

/// <summary>
/// In-memory cache of which AI provider/credential to bill — layered the same way as
/// AiRuntimeSettings' pause/quota (a platform-wide DB override on top of appsettings.json,
/// plus an optional override per organization on top of that). Populated at startup and
/// kept in sync by AdminUiController's/OrgAdminController's writes, so resolving the
/// effective config on every AI call (AiTutorService.Effective()) is a couple of in-memory
/// dictionary lookups — no DB round trip.
///
/// Caveat: per-instance cache, same as AdminAccess/AiRuntimeSettings.
/// </summary>
public class AiProviderRuntime
{
    public record Config(string? ApiKey, string? BaseUrl, string? Model, string? GenerateModel)
    {
        public bool IsEmpty => ApiKey is null && BaseUrl is null && Model is null && GenerateModel is null;
    }

    private static readonly Config Nothing = new(null, null, null, null);
    private volatile Config _platform = Nothing;
    private volatile Dictionary<int, Config> _orgs = new();

    private static string? Norm(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;
    private static Config Build(string? apiKey, string? baseUrl, string? model, string? generateModel) =>
        new(Norm(apiKey), Norm(baseUrl), Norm(model), Norm(generateModel));

    public void SetPlatform(string? apiKey, string? baseUrl, string? model, string? generateModel) =>
        _platform = Build(apiKey, baseUrl, model, generateModel);

    /// <summary>Null when the platform has no DB override on any field (pure appsettings.json).</summary>
    public Config? Platform => _platform.IsEmpty ? null : _platform;

    public void SetOrgs(IEnumerable<(int OrganizationId, string? ApiKey, string? BaseUrl, string? Model, string? GenerateModel)> rows) =>
        _orgs = rows.ToDictionary(r => r.OrganizationId, r => Build(r.ApiKey, r.BaseUrl, r.Model, r.GenerateModel));

    public void SetOrg(int organizationId, string? apiKey, string? baseUrl, string? model, string? generateModel)
    {
        var next = new Dictionary<int, Config>(_orgs) { [organizationId] = Build(apiKey, baseUrl, model, generateModel) };
        _orgs = next;
    }

    public void ClearOrg(int organizationId)
    {
        if (!_orgs.ContainsKey(organizationId)) return;
        var next = new Dictionary<int, Config>(_orgs);
        next.Remove(organizationId);
        _orgs = next;
    }

    public Config? Org(int organizationId) => _orgs.TryGetValue(organizationId, out var c) && !c.IsEmpty ? c : null;
}
