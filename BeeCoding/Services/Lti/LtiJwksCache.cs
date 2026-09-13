using System.Collections.Concurrent;
using Microsoft.IdentityModel.Tokens;

namespace BeeCoding.Services.Lti;

/// <summary>Fetches and caches a platform's public keyset (its `key_set_url`) — used to
/// verify the signature on every launch id_token from that platform.</summary>
public class LtiJwksCache(IHttpClientFactory httpFactory)
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(30);
    private readonly IHttpClientFactory _http = httpFactory;
    private static readonly ConcurrentDictionary<string, (JsonWebKeySet Keys, DateTime FetchedAt)> _cache = new();

    public async Task<JsonWebKeySet> GetAsync(string jwksUrl, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(jwksUrl, out var hit) && DateTime.UtcNow - hit.FetchedAt < CacheTtl)
            return hit.Keys;

        var client = _http.CreateClient("lti");
        var json = await client.GetStringAsync(jwksUrl, ct);
        var keys = JsonWebKeySet.Create(json);
        _cache[jwksUrl] = (keys, DateTime.UtcNow);
        return keys;
    }
}
