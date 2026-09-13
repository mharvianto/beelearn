using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Text.Json;
using BeeCoding.Models;

namespace BeeCoding.Services.Lti;

/// <summary>
/// Client-credentials access tokens for calling a platform's Assignment and Grade
/// Services (AGS) — LTI Advantage services authenticate the tool with a JWT-bearer
/// client assertion (RFC 7523), signed with our own key, not a shared secret.
/// </summary>
public class LtiTokenService(IHttpClientFactory httpFactory, LtiToolKeyService keys)
{
    private const string ScoreScope =
        "https://purl.imsglobal.org/auth/lineitem https://purl.imsglobal.org/auth/lineitem.readonly " +
        "https://purl.imsglobal.org/auth/result.readonly https://purl.imsglobal.org/auth/score";

    private readonly IHttpClientFactory _http = httpFactory;
    private readonly LtiToolKeyService _keys = keys;
    private static readonly ConcurrentDictionary<int, (string Token, DateTime ExpiresAt)> _cache = new();

    public async Task<string> GetAccessTokenAsync(LtiPlatform platform, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(platform.Id, out var hit) && hit.ExpiresAt > DateTime.UtcNow.AddSeconds(30))
            return hit.Token;

        var creds = await _keys.GetSigningCredentialsAsync();
        var now = DateTimeOffset.UtcNow;
        var assertion = new JwtSecurityToken(new JwtHeader(creds), new JwtPayload
        {
            ["iss"] = platform.ClientId,
            ["sub"] = platform.ClientId,
            ["aud"] = platform.AuthTokenUrl,
            ["iat"] = now.ToUnixTimeSeconds(),
            ["exp"] = now.AddMinutes(5).ToUnixTimeSeconds(),
            ["jti"] = Guid.NewGuid().ToString("N"),
        });
        var assertionJwt = new JwtSecurityTokenHandler().WriteToken(assertion);

        var client = _http.CreateClient("lti");
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_assertion_type"] = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer",
            ["client_assertion"] = assertionJwt,
            ["scope"] = ScoreScope,
        });
        var res = await client.PostAsync(platform.AuthTokenUrl, form, ct);
        res.EnsureSuccessStatusCode();
        var json = await res.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        var token = json.GetProperty("access_token").GetString()!;
        var expiresIn = json.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 3600;

        _cache[platform.Id] = (token, DateTime.UtcNow.AddSeconds(expiresIn));
        return token;
    }
}
