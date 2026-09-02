using BeeLearn.Data;
using BeeLearn.Models;
using BeeLearn.Services;
using BeeLearn.Services.Judge;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BeeLearn.Controllers;

/// <summary>
/// A teacher's private problem bank. Problems are private by default; marking one public
/// lets other teachers browse and copy it (hidden test cases are never sent to them — the
/// copy happens server-side). Adding to a board COPIES: the board's problem is independent.
/// </summary>
[ApiController]
[Authorize(Roles = "Teacher")]
public class BankController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly IBoardNotifier _notifier;

    public BankController(AppDbContext db, IBoardNotifier notifier)
    {
        _db = db;
        _notifier = notifier;
    }

    private IQueryable<BankProblem> Readable() =>
        _db.BankProblems.Where(b => b.OwnerId == UserId || b.IsPublic);

    [HttpGet("api/bank")]
    public async Task<ActionResult<IEnumerable<BankSummaryDto>>> List(
        [FromQuery] string? q, [FromQuery] string? tag, [FromQuery] string? scope)
    {
        var query = (scope ?? "all").ToLowerInvariant() switch
        {
            "mine" => _db.BankProblems.Where(b => b.OwnerId == UserId),
            "public" => _db.BankProblems.Where(b => b.IsPublic && b.OwnerId != UserId),
            _ => Readable(),
        };

        if (!string.IsNullOrWhiteSpace(q))
        {
            var needle = q.Trim();
            query = query.Where(b => EF.Functions.Like(b.Title, $"%{needle}%")
                                  || EF.Functions.Like(b.Tags, $"%{needle}%"));
        }
        if (!string.IsNullOrWhiteSpace(tag))
        {
            var t = tag.Trim().ToLowerInvariant();
            query = query.Where(b => EF.Functions.Like(b.Tags, $"%{t}%"));
        }

        var rows = await query
            .Include(b => b.Owner)
            .Include(b => b.TestCases)
            .OrderByDescending(b => b.UpdatedAt)
            .Take(200)
            .ToListAsync();

        return rows.Select(b => Mapping.ToSummary(b, UserId)).ToList();
    }

    [HttpGet("api/bank/{id:int}")]
    public async Task<ActionResult<BankProblemDto>> Get(int id)
    {
        var b = await Readable()
            .Include(x => x.Owner)
            .Include(x => x.TestCases)
            .FirstOrDefaultAsync(x => x.Id == id);
        return b is null ? NotFound() : Mapping.ToDto(b, UserId);
    }

    [HttpPost("api/bank")]
    public async Task<ActionResult<BankProblemDto>> Create(UpsertBankProblemDto dto)
    {
        var b = new BankProblem { OwnerId = UserId };
        Apply(b, dto);
        _db.BankProblems.Add(b);
        await _db.SaveChangesAsync();

        await _db.Entry(b).Reference(x => x.Owner).LoadAsync();
        return Mapping.ToDto(b, UserId);
    }

    [HttpPut("api/bank/{id:int}")]
    public async Task<ActionResult<BankProblemDto>> Update(int id, UpsertBankProblemDto dto)
    {
        var b = await _db.BankProblems
            .Include(x => x.Owner)
            .Include(x => x.TestCases)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (b is null) return NotFound();
        if (b.OwnerId != UserId) return Forbid();

        Apply(b, dto);
        if (dto.TestCases is not null)
        {
            var keep = dto.TestCases.Where(t => t.Id is > 0).Select(t => t.Id!.Value).ToHashSet();
            _db.BankTestCases.RemoveRange(b.TestCases.Where(t => !keep.Contains(t.Id)));
        }
        await _db.SaveChangesAsync();

        var fresh = await _db.BankProblems.Include(x => x.Owner).Include(x => x.TestCases)
            .FirstAsync(x => x.Id == id);
        return Mapping.ToDto(fresh, UserId);
    }

    [HttpDelete("api/bank/{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var b = await _db.BankProblems.FirstOrDefaultAsync(x => x.Id == id);
        if (b is null) return NotFound();
        if (b.OwnerId != UserId) return Forbid();

        _db.BankProblems.Remove(b);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Copy a bank problem (with all its tests) onto a board the caller owns.</summary>
    [HttpPost("api/bank/{id:int}/copy-to/{slug}")]
    public async Task<ActionResult<ProblemDto>> CopyToBoard(int id, string slug)
    {
        var bank = await Readable().Include(x => x.TestCases).FirstOrDefaultAsync(x => x.Id == id);
        if (bank is null) return NotFound();

        var board = await _db.Boards.FirstOrDefaultAsync(x => x.Slug == slug);
        if (board is null) return NotFound();
        if (board.OwnerId != UserId) return Forbid();

        int nextPos = await _db.Problems.Where(p => p.BoardId == board.Id)
            .Select(p => (int?)p.Position).MaxAsync() is int max ? max + 1 : 0;

        var p = new Problem
        {
            BoardId = board.Id,
            Title = bank.Title,
            StatementMarkdown = bank.StatementMarkdown,
            Language = bank.Language,
            StarterCode = bank.StarterCode,
            TimeLimitMs = bank.TimeLimitMs,
            MemoryLimitKb = bank.MemoryLimitKb,
            Position = nextPos,
            SourceBankProblemId = bank.Id,
        };
        foreach (var t in bank.TestCases.OrderBy(t => t.Position).ThenBy(t => t.Id))
            p.TestCases.Add(new TestCase
            {
                Stdin = t.Stdin,
                ExpectedStdout = t.ExpectedStdout,
                IsSample = t.IsSample,
                Points = t.Points,
                Position = t.Position,
            });

        _db.Problems.Add(p);
        await _db.SaveChangesAsync();
        await _notifier.ProblemChangedAsync(board.Id);

        return Mapping.ToOwnerDto(p);
    }

    /// <summary>Save an existing board problem into the caller's bank.</summary>
    [HttpPost("api/boards/{slug}/problems/{problemId:int}/to-bank")]
    public async Task<ActionResult<BankProblemDto>> SaveToBank(string slug, int problemId)
    {
        var board = await _db.Boards.FirstOrDefaultAsync(x => x.Slug == slug);
        if (board is null) return NotFound();
        if (board.OwnerId != UserId) return Forbid();

        var p = await _db.Problems.Include(x => x.TestCases)
            .FirstOrDefaultAsync(x => x.Id == problemId && x.BoardId == board.Id);
        if (p is null) return NotFound();

        var b = new BankProblem
        {
            OwnerId = UserId,
            Title = p.Title,
            StatementMarkdown = p.StatementMarkdown,
            Language = p.Language,
            StarterCode = p.StarterCode,
            TimeLimitMs = p.TimeLimitMs,
            MemoryLimitKb = p.MemoryLimitKb,
        };
        foreach (var t in p.TestCases.OrderBy(t => t.Position).ThenBy(t => t.Id))
            b.TestCases.Add(new BankTestCase
            {
                Stdin = t.Stdin,
                ExpectedStdout = t.ExpectedStdout,
                IsSample = t.IsSample,
                Points = t.Points,
                Position = t.Position,
            });

        _db.BankProblems.Add(b);
        await _db.SaveChangesAsync();

        await _db.Entry(b).Reference(x => x.Owner).LoadAsync();
        return Mapping.ToDto(b, UserId);
    }

    private static void Apply(BankProblem b, UpsertBankProblemDto dto)
    {
        b.Title = (dto.Title ?? "").Trim();
        b.StatementMarkdown = dto.StatementMarkdown ?? "";
        b.Language = NativeCompiler.Normalize(dto.Language);
        b.StarterCode = dto.StarterCode ?? "";
        b.TimeLimitMs = Math.Clamp(dto.TimeLimitMs <= 0 ? 1000 : dto.TimeLimitMs, 100, 10_000);
        b.MemoryLimitKb = Math.Clamp(dto.MemoryLimitKb <= 0 ? 32_768 : dto.MemoryLimitKb, 4_096, 512_000);
        b.Tags = NormalizeTags(dto.Tags);
        b.IsPublic = dto.IsPublic;
        b.UpdatedAt = DateTime.UtcNow;

        foreach (var t in dto.TestCases ?? Enumerable.Empty<UpsertTestCaseDto>())
        {
            var tc = t.Id is > 0 ? b.TestCases.FirstOrDefault(x => x.Id == t.Id) : null;
            if (tc is null)
            {
                tc = new BankTestCase { BankProblem = b };
                b.TestCases.Add(tc);
            }
            tc.Stdin = t.Stdin ?? "";
            tc.ExpectedStdout = t.ExpectedStdout ?? "";
            tc.IsSample = t.IsSample;
            tc.Points = Math.Max(0, t.Points);
            tc.Position = t.Position;
        }
    }

    private static string NormalizeTags(string? tags) =>
        string.Join(',', (tags ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.ToLowerInvariant())
            .Distinct()
            .Take(12));
}
