using System.Security.Cryptography;
using BeeCoding.Data;
using BeeCoding.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace BeeCoding.Services.Lti;

/// <summary>
/// BeeCoding's own RSA signing key for outbound LTI messages — the Deep Linking response
/// JWT, and the JWT-bearer client assertion used to fetch an AGS access token. Generated
/// once (on first use) and persisted in the DB so the `kid` stays stable across restarts;
/// a platform caches our JWKS by `kid`; rotating the key would break signature
/// verification for that platform until its cache expires.
/// </summary>
public class LtiToolKeyService(AppDbContext db)
{
    private readonly AppDbContext _db = db;
    private static readonly SemaphoreSlim _lock = new(1, 1);
    private static RsaSecurityKey? _cached;
    private static string? _cachedKeyId;
    private static RSAParameters _cachedPublicParams;

    private async Task EnsureLoadedAsync()
    {
        if (_cached is not null) return;
        await _lock.WaitAsync();
        try
        {
            if (_cached is not null) return;

            var row = await _db.LtiToolKeys.FindAsync(1);
            if (row is null)
            {
                using var rsa = RSA.Create(2048);
                row = new LtiToolKey
                {
                    Id = 1,
                    KeyId = Guid.NewGuid().ToString("N"),
                    PrivateKeyPem = rsa.ExportPkcs8PrivateKeyPem(),
                };
                _db.LtiToolKeys.Add(row);
                await _db.SaveChangesAsync();
            }

            var loaded = RSA.Create();
            loaded.ImportFromPem(row.PrivateKeyPem);
            _cached = new RsaSecurityKey(loaded) { KeyId = row.KeyId };
            _cachedKeyId = row.KeyId;
            _cachedPublicParams = loaded.ExportParameters(includePrivateParameters: false);
        }
        finally { _lock.Release(); }
    }

    public async Task<SigningCredentials> GetSigningCredentialsAsync()
    {
        await EnsureLoadedAsync();
        return new SigningCredentials(_cached, SecurityAlgorithms.RsaSha256);
    }

    /// <summary>Our public JWKS document — give its URL to every platform we register with.
    /// Built from the public RSA parameters only (modulus + exponent); never touches the
    /// private key material.</summary>
    public async Task<JsonWebKeySet> GetPublicJwksAsync()
    {
        await EnsureLoadedAsync();
        var jwk = new JsonWebKey
        {
            Kty = "RSA",
            Use = "sig",
            Alg = SecurityAlgorithms.RsaSha256,
            KeyId = _cachedKeyId,
            N = Base64UrlEncoder.Encode(_cachedPublicParams.Modulus),
            E = Base64UrlEncoder.Encode(_cachedPublicParams.Exponent),
        };
        var set = new JsonWebKeySet();
        set.Keys.Add(jwk);
        return set;
    }
}
