using BeeCoding.Data;
using BeeCoding.Models;
using BeeCoding.Services;
using BeeCoding.Services.Judge;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BeeCoding.Controllers;

[Authorize]
[Route("api/run")]
public class RunController : ApiControllerBase
{
    private readonly IJudgeQueue _queue;
    private readonly RateLimiter _rate;
    private readonly JudgeOptions _opt;
    private readonly AppDbContext _db;
    private readonly BoardService _boards;

    public RunController(IJudgeQueue queue, RateLimiter rate, IOptions<JudgeOptions> opt,
        AppDbContext db, BoardService boards)
    {
        _queue = queue;
        _rate = rate;
        _opt = opt.Value;
        _db = db;
        _boards = boards;
    }

    /// <summary>Ad-hoc compile + run with custom stdin. Not judged, not persisted.</summary>
    [HttpPost]
    public async Task<ActionResult<RunResultDto>> Run(RunDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Code)) return BadRequest("Code is empty.");
        if (dto.Code.Length > 200_000) return BadRequest("Code is too large.");
        if ((dto.Stdin?.Length ?? 0) > 256_000) return BadRequest("Stdin is too large.");
        if (!_rate.TryAcquire(UserId)) return StatusCode(429, "Slow down a moment and try again.");

        // If this Run is tied to a problem, apply that problem's header restrictions here too
        // (Submit already does) so students can't sidestep them via the Run box.
        var (bh, bs) = await RestrictionsForAsync(dto.ProblemId, dto.BankProblemId);
        if (SourcePolicy.Violation(dto.Code, bh, bs) is { } denied)
            return new RunResultDto(false, denied, "", "", 0, 0, false, 0, 0);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            return await _queue.EnqueueRunAsync(
                dto.Language, dto.Code, dto.Stdin ?? "", _opt.RunTimeLimitMs, _opt.RunMemoryLimitKb, timeout.Token);
        }
        catch (OperationCanceledException)
        {
            return StatusCode(504, "The judge is busy. Try again shortly.");
        }
    }

    private async Task<(string? Headers, string? Symbols)> RestrictionsForAsync(int? problemId, int? bankProblemId)
    {
        if (bankProblemId is int bid)
        {
            var b = await _db.BankProblems.Where(b => b.Id == bid && b.IsPublic)
                .Select(b => new { b.BannedHeaders, b.BannedSymbols }).FirstOrDefaultAsync();
            return b is null ? (null, null) : (b.BannedHeaders, b.BannedSymbols);
        }
        if (problemId is int pid)
        {
            var row = await _db.Problems.Where(p => p.Id == pid)
                .Select(p => new { p.BoardId, p.BannedHeaders, p.BannedSymbols }).FirstOrDefaultAsync();
            if (row is null || (string.IsNullOrEmpty(row.BannedHeaders) && string.IsNullOrEmpty(row.BannedSymbols)))
                return (null, null);
            return await _boards.GetMembershipAsync(row.BoardId, UserId) is null
                ? (null, null) : (row.BannedHeaders, row.BannedSymbols);
        }
        return (null, null);
    }
}
