using System.IdentityModel.Tokens.Jwt;
using BeeCoding.Models;
using Microsoft.IdentityModel.Tokens;

namespace BeeCoding.Services.Lti;

public class LtiLaunchValidator(LtiJwksCache jwks)
{
    private readonly LtiJwksCache _jwks = jwks;

    /// <summary>Verifies signature (against the platform's JWKS), issuer, audience,
    /// lifetime, and nonce (replay protection — the nonce must match the one this app
    /// generated for the login it initiated). Returns the decoded token on success.</summary>
    public async Task<(JwtSecurityToken? Token, string? Error)> ValidateAsync(
        string idToken, LtiPlatform platform, string expectedNonce, CancellationToken ct = default)
    {
        JsonWebKeySet keys;
        try { keys = await _jwks.GetAsync(platform.JwksUrl, ct); }
        catch (Exception ex) { return (null, $"couldn't fetch the platform's JWKS: {ex.Message}"); }

        var handler = new JwtSecurityTokenHandler();
        var validationParams = new TokenValidationParameters
        {
            ValidIssuer = platform.Issuer,
            ValidAudience = platform.ClientId,
            IssuerSigningKeys = keys.GetSigningKeys(),
            ValidateIssuerSigningKey = true,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2),
        };

        JwtSecurityToken jwt;
        try
        {
            handler.ValidateToken(idToken, validationParams, out var validated);
            jwt = (JwtSecurityToken)validated;
        }
        catch (Exception ex) { return (null, $"invalid id_token: {ex.Message}"); }

        var nonce = jwt.Claims.FirstOrDefault(c => c.Type == "nonce")?.Value;
        if (string.IsNullOrEmpty(nonce) || nonce != expectedNonce)
            return (null, "nonce mismatch — possible replay");

        return (jwt, null);
    }
}
