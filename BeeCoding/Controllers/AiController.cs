using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using BeeCoding.Data;
using BeeCoding.Models;
using BeeCoding.Services;
using BeeCoding.Services.Ai;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BeeCoding.Controllers;

public record AiHintDto(
    int? ProblemId, int? BankProblemId,
    string? Language, string? Code,
    string? Stdin, string? Verdict, string? CompilerOutput, string? Stderr,
    string? Question, string? Lang);   // Lang: "id" | "en" (reply language)

[ApiController]
[Authorize]
[Route("api/ai")]
public class AiController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly BoardService _boards;
    private readonly AiTutorService _ai;
    private readonly AiOptions _opt;

    private static readonly ConcurrentDictionary<int, long> _last = new();

    public AiController(AppDbContext db, BoardService boards, AiTutorService ai, IOptions<AiOptions> opt)
    {
        _db = db;
        _boards = boards;
        _ai = ai;
        _opt = opt.Value;
    }

    [HttpGet("enabled")]
    public IActionResult Enabled() =>
        Ok(new { enabled = _ai.Available, defaultLang = _ai.DefaultReplyLanguage });

    [HttpPost("hint")]
    public async Task<IActionResult> Hint(AiHintDto dto)
    {
        if (!_ai.Available) return NotFound();
        if (RateLimited()) return StatusCode(StatusCodes.Status429TooManyRequests, "Give the AI tutor a few seconds between questions.");

        var (ctx, err) = await BuildContextAsync(dto);
        if (err is not null) return err;

        try
        {
            var reply = await _ai.HintAsync(ctx!, dto.Lang, HttpContext.RequestAborted);
            return Ok(new { reply });
        }
        catch (AiUnavailableException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, ex.Message);
        }
    }

    [HttpPost("hint/stream")]
    public async Task Stream(AiHintDto dto)
    {
        if (!_ai.Available) { Response.StatusCode = StatusCodes.Status404NotFound; return; }
        if (RateLimited()) { Response.StatusCode = StatusCodes.Status429TooManyRequests; return; }

        var (ctx, err) = await BuildContextAsync(dto);
        if (err is not null)
        {
            Response.StatusCode = err is ObjectResult o ? o.StatusCode ?? 400 : 400;
            return;
        }

        Response.StatusCode = 200;
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";   // don't let nginx buffer the stream
        HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

        var ct = HttpContext.RequestAborted;
        try
        {
            await foreach (var chunk in _ai.StreamAsync(ctx!, dto.Lang, ct))
            {
                bool isFinal = chunk.StartsWith(AiTutorService.FinalSentinel, StringComparison.Ordinal);
                string? final = isFinal ? chunk[AiTutorService.FinalSentinel.Length..] : null;
                string? delta = isFinal ? null : chunk;
                await WriteEventAsync(new { final, delta }, ct);
            }
            await WriteEventAsync(new { done = true }, ct);
        }
        catch (AiUnavailableException ex)
        {
            await WriteEventAsync(new { error = ex.Message }, ct);
        }
        catch (OperationCanceledException) { /* client left */ }
    }

    private async Task WriteEventAsync(object payload, CancellationToken ct)
    {
        var line = "data: " + JsonSerializer.Serialize(payload) + "\n\n";
        await Response.Body.WriteAsync(Encoding.UTF8.GetBytes(line), ct);
        await Response.Body.FlushAsync(ct);
    }

    private bool RateLimited()
    {
        var now = DateTime.UtcNow.Ticks;
        var min = TimeSpan.FromSeconds(Math.Max(1, _opt.RateLimitSeconds)).Ticks;
        var prev = _last.GetOrAdd(UserId, 0);
        if (now - prev < min) return true;
        _last[UserId] = now;
        return false;
    }

    private async Task<(AiHintContext? ctx, ObjectResult? err)> BuildContextAsync(AiHintDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Code))
            return (null, new ObjectResult("There's no code to look at yet.") { StatusCode = 400 });
        if (dto.Code.Length > 200_000)
            return (null, new ObjectResult("Code is too large.") { StatusCode = 400 });

        string statement, language;
        List<(string Stdin, string Expected)> samples;

        if (dto.BankProblemId is int bid)
        {
            var b = await _db.BankProblems.Include(x => x.TestCases)
                .FirstOrDefaultAsync(x => x.Id == bid && x.IsPublic);
            if (b is null) return (null, new ObjectResult("Problem not found.") { StatusCode = 404 });
            statement = b.StatementMarkdown; language = b.Language;
            samples = b.TestCases.Where(t => t.IsSample).OrderBy(t => t.Position).ThenBy(t => t.Id)
                .Select(t => (t.Stdin, t.ExpectedStdout)).ToList();
        }
        else if (dto.ProblemId is int pid)
        {
            var p = await _db.Problems.Include(x => x.TestCases).FirstOrDefaultAsync(x => x.Id == pid);
            if (p is null) return (null, new ObjectResult("Problem not found.") { StatusCode = 404 });
            if (await _boards.GetMembershipAsync(p.BoardId, UserId) is null)
                return (null, new ObjectResult("Not a member of this board.") { StatusCode = 403 });
            statement = p.StatementMarkdown; language = p.Language;
            samples = p.TestCases.Where(t => t.IsSample).OrderBy(t => t.Position).ThenBy(t => t.Id)
                .Select(t => (t.Stdin, t.ExpectedStdout)).ToList();
        }
        else
        {
            return (null, new ObjectResult("problemId or bankProblemId is required.") { StatusCode = 400 });
        }

        var ctx = new AiHintContext(
            statement,
            string.IsNullOrWhiteSpace(dto.Language) ? language : dto.Language!,
            dto.Code!, dto.Stdin, dto.Verdict, dto.CompilerOutput, dto.Stderr, dto.Question, samples);
        return (ctx, null);
    }
}
