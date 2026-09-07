using System.Collections.Concurrent;
using BeeCoding.Data;
using BeeCoding.Models;
using BeeCoding.Services;
using BeeCoding.Services.Ai;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BeeCoding.Controllers;

public record AiHintDto(
    int? ProblemId, int? BankProblemId,
    string? Language, string? Code,
    string? Stdin, string? Verdict, string? CompilerOutput, string? Stderr,
    string? Question);

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
    public IActionResult Enabled() => Ok(new { enabled = _ai.Available });

    [HttpPost("hint")]
    public async Task<IActionResult> Hint(AiHintDto dto)
    {
        if (!_ai.Available) return NotFound();

        var now = DateTime.UtcNow.Ticks;
        var min = TimeSpan.FromSeconds(Math.Max(1, _opt.RateLimitSeconds)).Ticks;
        var prev = _last.GetOrAdd(UserId, 0);
        if (now - prev < min)
            return StatusCode(StatusCodes.Status429TooManyRequests, "Give the AI tutor a few seconds between questions.");
        _last[UserId] = now;

        if (string.IsNullOrWhiteSpace(dto.Code)) return BadRequest("There's no code to look at yet.");
        if (dto.Code.Length > 200_000) return BadRequest("Code is too large.");

        // Resolve the problem for context, checking the caller may see it.
        string statement;
        string language;
        List<(string Stdin, string Expected)> samples;

        if (dto.BankProblemId is int bid)
        {
            var b = await _db.BankProblems.Include(x => x.TestCases)
                .FirstOrDefaultAsync(x => x.Id == bid && x.IsPublic);
            if (b is null) return NotFound();
            statement = b.StatementMarkdown;
            language = b.Language;
            samples = b.TestCases.Where(t => t.IsSample).OrderBy(t => t.Position).ThenBy(t => t.Id)
                .Select(t => (t.Stdin, t.ExpectedStdout)).ToList();
        }
        else if (dto.ProblemId is int pid)
        {
            var p = await _db.Problems.Include(x => x.TestCases).FirstOrDefaultAsync(x => x.Id == pid);
            if (p is null) return NotFound();
            if (await _boards.GetMembershipAsync(p.BoardId, UserId) is null) return Forbid();
            statement = p.StatementMarkdown;
            language = p.Language;
            samples = p.TestCases.Where(t => t.IsSample).OrderBy(t => t.Position).ThenBy(t => t.Id)
                .Select(t => (t.Stdin, t.ExpectedStdout)).ToList();
        }
        else
        {
            return BadRequest("problemId or bankProblemId is required.");
        }

        var ctx = new AiHintContext(
            statement,
            string.IsNullOrWhiteSpace(dto.Language) ? language : dto.Language!,
            dto.Code!, dto.Stdin, dto.Verdict, dto.CompilerOutput, dto.Stderr, dto.Question, samples);

        try
        {
            var reply = await _ai.HintAsync(ctx, HttpContext.RequestAborted);
            return Ok(new { reply });
        }
        catch (AiUnavailableException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, ex.Message);
        }
    }
}
