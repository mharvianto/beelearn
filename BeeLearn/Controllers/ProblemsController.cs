using BeeLearn.Data;
using BeeLearn.Models;
using BeeLearn.Services;
using BeeLearn.Services.Judge;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BeeLearn.Controllers;

[Authorize]
[Route("api/boards/{slug}/problems")]
public class ProblemsController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly BoardService _boards;
    private readonly VisibilityService _vis;
    private readonly IBoardNotifier _notifier;

    public ProblemsController(AppDbContext db, BoardService boards, VisibilityService vis, IBoardNotifier notifier)
    {
        _db = db;
        _boards = boards;
        _vis = vis;
        _notifier = notifier;
    }

    [HttpGet]
    public async Task<ActionResult<object>> List(string slug)
    {
        var boardId = await _boards.ResolveBoardIdAsync(slug);
        if (boardId is null) return NotFound();
        var me = await _boards.GetMembershipAsync(boardId.Value, UserId);
        if (me is null) return Forbid();

        var problems = await _db.Problems
            .Where(p => p.BoardId == boardId.Value)
            .Include(p => p.TestCases)
            .OrderBy(p => p.Position).ThenBy(p => p.Id)
            .ToListAsync();

        return _vis.IsStaff(me.Role)
            ? problems.Select(Mapping.ToOwnerDto).ToList()
            : problems.Select(Mapping.ToStudentDto).ToList();
    }

    [HttpGet("{problemId:int}")]
    public async Task<ActionResult<object>> Get(string slug, int problemId)
    {
        var boardId = await _boards.ResolveBoardIdAsync(slug);
        if (boardId is null) return NotFound();
        var me = await _boards.GetMembershipAsync(boardId.Value, UserId);
        if (me is null) return Forbid();

        var p = await _db.Problems
            .Include(x => x.TestCases)
            .FirstOrDefaultAsync(x => x.Id == problemId && x.BoardId == boardId.Value);
        if (p is null) return NotFound();

        return _vis.IsStaff(me.Role) ? Mapping.ToOwnerDto(p) : Mapping.ToStudentDto(p);
    }

    [HttpPost]
    public async Task<ActionResult<ProblemDto>> Create(string slug, UpsertProblemDto dto)
    {
        var (boardId, err) = await RequireOwnerAsync(slug);
        if (err is not null) return err;

        var p = new Problem { BoardId = boardId!.Value };
        Apply(p, dto);
        _db.Problems.Add(p);
        await _db.SaveChangesAsync();
        await _notifier.ProblemChangedAsync(boardId.Value);

        return Mapping.ToOwnerDto(p);
    }

    [HttpPut("{problemId:int}")]
    public async Task<ActionResult<ProblemDto>> Update(string slug, int problemId, UpsertProblemDto dto)
    {
        var (boardId, err) = await RequireOwnerAsync(slug);
        if (err is not null) return err;

        var p = await _db.Problems
            .Include(x => x.TestCases)
            .FirstOrDefaultAsync(x => x.Id == problemId && x.BoardId == boardId!.Value);
        if (p is null) return NotFound();

        Apply(p, dto);

        if (dto.TestCases is not null)
        {
            var keepIds = dto.TestCases.Where(t => t.Id is > 0).Select(t => t.Id!.Value).ToHashSet();
            _db.TestCases.RemoveRange(p.TestCases.Where(t => !keepIds.Contains(t.Id)));
        }

        await _db.SaveChangesAsync();
        await _notifier.ProblemChangedAsync(boardId!.Value);

        var fresh = await _db.Problems.Include(x => x.TestCases).FirstAsync(x => x.Id == problemId);
        return Mapping.ToOwnerDto(fresh);
    }

    [HttpDelete("{problemId:int}")]
    public async Task<IActionResult> Delete(string slug, int problemId)
    {
        var (boardId, err) = await RequireOwnerAsync(slug);
        if (err is not null) return err;

        var p = await _db.Problems.FirstOrDefaultAsync(x => x.Id == problemId && x.BoardId == boardId!.Value);
        if (p is null) return NotFound();

        _db.Problems.Remove(p);
        await _db.SaveChangesAsync();
        await _notifier.ProblemChangedAsync(boardId!.Value);
        return NoContent();
    }

    private void Apply(Problem p, UpsertProblemDto dto)
    {
        p.Title = (dto.Title ?? "").Trim();
        p.StatementMarkdown = dto.StatementMarkdown ?? "";
        p.Language = NativeCompiler.Normalize(dto.Language);
        p.StarterCode = dto.StarterCode ?? "";
        p.TimeLimitMs = Math.Clamp(dto.TimeLimitMs <= 0 ? 1000 : dto.TimeLimitMs, 100, 10_000);
        p.MemoryLimitKb = Math.Clamp(dto.MemoryLimitKb <= 0 ? 32_768 : dto.MemoryLimitKb, 4_096, 512_000);
        p.Position = dto.Position;

        foreach (var t in dto.TestCases ?? Enumerable.Empty<UpsertTestCaseDto>())
        {
            var tc = t.Id is > 0 ? p.TestCases.FirstOrDefault(x => x.Id == t.Id) : null;
            if (tc is null)
            {
                tc = new TestCase { Problem = p };
                p.TestCases.Add(tc);
            }
            tc.Stdin = t.Stdin ?? "";
            tc.ExpectedStdout = t.ExpectedStdout ?? "";
            tc.IsSample = t.IsSample;
            tc.Points = Math.Max(0, t.Points);
            tc.Position = t.Position;
        }
    }

    private async Task<(int? boardId, ActionResult? error)> RequireOwnerAsync(string slug)
    {
        var board = await _db.Boards.FirstOrDefaultAsync(b => b.Slug == slug);
        if (board is null) return (null, NotFound());
        if (board.OwnerId != UserId) return (null, Forbid());
        return (board.Id, null);
    }
}
