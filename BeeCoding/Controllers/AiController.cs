using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using BeeCoding.Data;
using BeeCoding.Models;
using BeeCoding.Services;
using BeeCoding.Services.Ai;
using BeeCoding.Services.Judge;
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

public record AiGenerateDto(string? Idea, string? Level, string? Language, int? Count, string? Lang);

[ApiController]
[Authorize]
[Route("api/ai")]
public class AiController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly BoardService _boards;
    private readonly AiTutorService _ai;
    private readonly AiUsageService _usage;
    private readonly AiHintProgressService _prog;
    private readonly JudgeQueue _queue;
    private readonly AiOptions _opt;

    private static readonly ConcurrentDictionary<int, long> _last = new();

    public AiController(AppDbContext db, BoardService boards, AiTutorService ai,
        AiUsageService usage, AiHintProgressService prog, JudgeQueue queue, IOptions<AiOptions> opt)
    {
        _db = db;
        _boards = boards;
        _ai = ai;
        _usage = usage;
        _prog = prog;
        _queue = queue;
        _opt = opt.Value;
    }

    [HttpGet("enabled")]
    public IActionResult Enabled() =>
        Ok(new { enabled = _ai.Available, defaultLang = _ai.DefaultReplyLanguage });

    [HttpGet("usage")]
    public async Task<IActionResult> Usage() => Ok(await _usage.SummaryAsync(UserId));

    /// <summary>
    /// Teacher-only. Ask the AI to draft a full bank problem from an idea, then compile and
    /// run its reference solution against the AI's inputs so the stored expected outputs are
    /// the program's ACTUAL output (not the model's claim). Saved to the caller's bank as
    /// private, returned for review/editing.
    /// </summary>
    [HttpPost("generate-problem")]
    public async Task<IActionResult> GenerateProblem(AiGenerateDto dto)
    {
        if (!_ai.Available) return NotFound();
        if (CurrentRole != "Teacher") return StatusCode(StatusCodes.Status403Forbidden, "Teachers only.");
        if (string.IsNullOrWhiteSpace(dto.Idea)) return BadRequest("Describe the idea or topic.");
        if (RateLimited()) return StatusCode(StatusCodes.Status429TooManyRequests, "Give the AI a few seconds.");

        var wantLang = dto.Language is "c" ? "c" : "cpp";
        var level = dto.Level is "Easy" or "Medium" or "Hard" ? dto.Level! : "Medium";
        var count = dto.Count is >= 3 and <= 15 ? dto.Count!.Value : 10;

        AiTutorService.AiGenResult gen;
        try
        {
            gen = await _ai.GenerateProblemAsync(dto.Idea!, level, wantLang, count,
                string.IsNullOrWhiteSpace(dto.Lang) ? _ai.DefaultReplyLanguage : dto.Lang!, HttpContext.RequestAborted);
        }
        catch (AiUnavailableException ex) { return StatusCode(StatusCodes.Status502BadGateway, ex.Message); }
        await _usage.RecordAsync(UserId, gen.PromptTokens, gen.CompletionTokens);

        var gp = gen.Problem;
        var refLang = gp.Language is "c" ? "c" : "cpp";
        var built = new List<(string Stdin, string Expected, bool IsSample)>();

        int i = 0;
        foreach (var t in gp.Tests.Take(15))
        {
            var job = new RunJob(refLang, gp.ReferenceSolution, t.Stdin ?? "", gp.TimeLimitMs, gp.MemoryLimitKb,
                new TaskCompletionSource<RunResultDto>(TaskCreationOptions.RunContinuationsAsynchronously));
            RunResultDto res;
            try { res = await _queue.EnqueueRunAsync(job, HttpContext.RequestAborted); }
            catch { return StatusCode(StatusCodes.Status504GatewayTimeout, "Timed out validating the generated problem."); }

            if (!res.CompileOk)
                return UnprocessableEntity(new { message = "The AI's reference solution didn't compile — try again or rephrase the idea.", compilerOutput = res.CompilerOutput });
            if (res.TimedOut || res.Signal != 0 || res.ExitCode != 0)
                return UnprocessableEntity(new { message = $"The AI's reference solution failed on test #{i + 1} (signal {res.Signal}, exit {res.ExitCode}) — try again or rephrase.", stderr = res.Stderr });

            built.Add((t.Stdin ?? "", res.Stdout ?? "", t.IsSample));
            i++;
        }
        if (built.Count < 2)
            return UnprocessableEntity(new { message = "The AI didn't produce enough usable tests — try again." });

        if (!built.Any(x => x.IsSample)) built[0] = (built[0].Stdin, built[0].Expected, true);
        if (built.All(x => x.IsSample)) built[^1] = (built[^1].Stdin, built[^1].Expected, false);

        var problem = new BankProblem
        {
            OwnerId = UserId,
            Title = gp.Title.Trim(),
            StatementMarkdown = gp.StatementMarkdown ?? "",
            Language = refLang,
            StarterCode = gp.StarterCode ?? "",
            Level = Mapping.ParseLevel(gp.Level),
            Tags = Mapping.NormalizeTags(gp.Tags),
            TimeLimitMs = gp.TimeLimitMs,
            MemoryLimitKb = gp.MemoryLimitKb,
            IsPublic = false,
        };
        int pos = 0;
        foreach (var (stdin, expected, isSample) in built)
            problem.TestCases.Add(new BankTestCase
            {
                Stdin = stdin, ExpectedStdout = expected, IsSample = isSample,
                Points = isSample ? 0 : 1, Position = pos++,
            });

        _db.BankProblems.Add(problem);
        await _db.SaveChangesAsync();
        await _db.Entry(problem).Reference(x => x.Owner).LoadAsync();
        return Ok(Mapping.ToDto(problem, UserId));
    }

    /// <summary>Forget the progressive-hint level for one problem — the next hint starts gentle again.</summary>
    [HttpPost("hint-progress/reset")]
    public async Task<IActionResult> ResetHintProgress(AiHintDto dto)
    {
        var key = ProblemKey(dto);
        if (key is null) return BadRequest("problemId or bankProblemId is required.");
        await _prog.ResetAsync(UserId, key, HttpContext.RequestAborted);
        return NoContent();
    }

    private static string? ProblemKey(AiHintDto dto) =>
        dto.BankProblemId is int b ? $"bank:{b}" : dto.ProblemId is int p ? $"board:{p}" : null;

    [HttpPost("hint")]
    public async Task<IActionResult> Hint(AiHintDto dto)
    {
        if (!_ai.Available) return NotFound();
        if (RateLimited()) return StatusCode(StatusCodes.Status429TooManyRequests, "Give the AI tutor a few seconds between questions.");

        var (ctx, err) = await BuildContextAsync(dto);
        if (err is not null) return err;

        var level = await _prog.BumpAsync(UserId, ProblemKey(dto)!, AiHintProgressService.HashCode(dto.Code), HttpContext.RequestAborted);
        try
        {
            var r = await _ai.HintAsync(ctx!, dto.Lang, level, HttpContext.RequestAborted);
            await _usage.RecordAsync(UserId, r.PromptTokens, r.CompletionTokens);
            return Ok(new { reply = r.Text, level });
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
        var level = await _prog.BumpAsync(UserId, ProblemKey(dto)!, AiHintProgressService.HashCode(dto.Code), ct);
        await WriteEventAsync(new { level }, ct);

        try
        {
            await foreach (var chunk in _ai.StreamAsync(ctx!, dto.Lang, level, ct))
            {
                if (chunk.StartsWith(AiTutorService.UsageSentinel, StringComparison.Ordinal))
                {
                    var parts = chunk[AiTutorService.UsageSentinel.Length..].Split(' ');
                    if (parts.Length == 2 && int.TryParse(parts[0], out var pt) && int.TryParse(parts[1], out var ctk))
                        await _usage.RecordAsync(UserId, pt, ctk, CancellationToken.None);
                    continue;
                }
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
