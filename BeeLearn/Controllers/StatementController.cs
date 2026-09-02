using System.Security.Cryptography;
using System.Text;
using BeeLearn.Data;
using BeeLearn.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BeeLearn.Controllers;

[ApiController]
[Authorize]
public class StatementController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly BoardService _boards;
    private readonly StatementImageService _images;

    public StatementController(AppDbContext db, BoardService boards, StatementImageService images)
    {
        _db = db;
        _boards = boards;
        _images = images;
    }

    /// <summary>
    /// Problem statement + samples rendered to a PNG (no text reaches the client) and
    /// returned AES-GCM encrypted. The identity watermark is baked into the pixels.
    /// </summary>
    [HttpGet("api/problems/{problemId:int}/statement")]
    public async Task<ActionResult<object>> Get(int problemId, [FromQuery] string? theme)
    {
        var problem = await _db.Problems.Include(p => p.TestCases)
            .FirstOrDefaultAsync(p => p.Id == problemId);
        if (problem is null) return NotFound();

        var membership = await _boards.GetMembershipAsync(problem.BoardId, UserId);
        if (membership is null) return Forbid();

        var samples = problem.TestCases
            .Where(t => t.IsSample)
            .OrderBy(t => t.Position).ThenBy(t => t.Id)
            .Select(t => (t.Stdin, t.ExpectedStdout))
            .ToList();

        bool dark = string.Equals(theme, "dark", StringComparison.OrdinalIgnoreCase);
        var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                    ?? User.Identity?.Name ?? UserId.ToString();
        var watermark = $"{email} · {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC";

        var fingerprint = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(problem.StatementMarkdown + "" +
                string.Join("", samples.Select(s => s.Stdin + "" + s.ExpectedStdout)))))[..16];
        var cacheKey = $"{problemId}:{fingerprint}:{(dark ? "d" : "l")}";

        var png = _images.RenderPng(cacheKey, problem.StatementMarkdown, samples, watermark, dark);

        // AES-256-GCM; fresh key + nonce per request, delivered in the same authenticated response.
        var key = RandomNumberGenerator.GetBytes(32);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var cipher = new byte[png.Length];
        var tag = new byte[16];
        using (var gcm = new AesGcm(key, 16))
            gcm.Encrypt(nonce, png, cipher, tag);

        var payload = new byte[cipher.Length + tag.Length];
        Buffer.BlockCopy(cipher, 0, payload, 0, cipher.Length);
        Buffer.BlockCopy(tag, 0, payload, cipher.Length, tag.Length);

        Response.Headers.CacheControl = "no-store, private";
        return new
        {
            alg = "AES-GCM",
            key = Convert.ToBase64String(key),
            iv = Convert.ToBase64String(nonce),
            data = Convert.ToBase64String(payload),
        };
    }
}
