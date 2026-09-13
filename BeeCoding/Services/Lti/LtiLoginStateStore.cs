using System.Collections.Concurrent;

namespace BeeCoding.Services.Lti;

/// <summary>One pending OIDC third-party login — created at /lti/login, consumed (once) at
/// /lti/launch. `Nonce` guards the id_token against replay; `State` guards the round trip
/// itself against CSRF.</summary>
public record LtiLoginState(int PlatformId, string Nonce, string TargetLinkUri, DateTime ExpiresAt);

/// <summary>A validated launch, staged for the Deep Linking picker SPA page — the browser
/// carries only an opaque token; everything the platform needs back (its own return URL,
/// the echoed `data` opaque string) stays server-side.</summary>
public record LtiDeepLinkContext(
    int PlatformId, string PlatformName, string DeploymentId, string ReturnUrl, string? Data, DateTime ExpiresAt);

/// <summary>
/// Short-lived, in-memory state for the LTI login/launch dance and the Deep Linking hop —
/// both live only a few minutes and are single-instance-safe the same way the judge's
/// in-process job registries are (see AiJobStore). A scale-out deployment would need a
/// shared (Redis) backend here, same caveat as AdminAccess's DB-admin cache.
/// </summary>
public class LtiLoginStateStore
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);
    private readonly ConcurrentDictionary<string, LtiLoginState> _logins = new();
    private readonly ConcurrentDictionary<string, LtiDeepLinkContext> _deepLinks = new();

    public string StartLogin(int platformId, string nonce, string targetLinkUri)
    {
        Sweep();
        var state = Guid.NewGuid().ToString("N");
        _logins[state] = new LtiLoginState(platformId, nonce, targetLinkUri, DateTime.UtcNow + Ttl);
        return state;
    }

    /// <summary>One-shot: removes the entry so a replayed `state` can never validate twice.</summary>
    public LtiLoginState? ConsumeLogin(string state) =>
        _logins.TryRemove(state, out var v) && v.ExpiresAt > DateTime.UtcNow ? v : null;

    public string StageDeepLink(LtiDeepLinkContext ctx)
    {
        Sweep();
        var token = Guid.NewGuid().ToString("N");
        _deepLinks[token] = ctx with { ExpiresAt = DateTime.UtcNow + Ttl };
        return token;
    }

    /// <summary>Not one-shot — the picker page reads it once, then the select POST reads it
    /// again to build the response, so it stays until it expires.</summary>
    public LtiDeepLinkContext? GetDeepLink(string token) =>
        _deepLinks.TryGetValue(token, out var v) && v.ExpiresAt > DateTime.UtcNow ? v : null;

    private void Sweep()
    {
        var now = DateTime.UtcNow;
        foreach (var (k, v) in _logins) if (v.ExpiresAt <= now) _logins.TryRemove(k, out _);
        foreach (var (k, v) in _deepLinks) if (v.ExpiresAt <= now) _deepLinks.TryRemove(k, out _);
    }
}
