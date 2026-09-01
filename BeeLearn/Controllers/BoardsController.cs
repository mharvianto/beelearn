using BeeLearn.Data;
using BeeLearn.Models;
using BeeLearn.Services;
using BeeLearn.Services.Judge;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BeeLearn.Controllers;

[Authorize]
[Route("api/boards")]
public class BoardsController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly BoardService _boards;
    private readonly VisibilityService _vis;
    private readonly IBoardNotifier _notifier;

    public BoardsController(AppDbContext db, BoardService boards, VisibilityService vis, IBoardNotifier notifier)
    {
        _db = db;
        _boards = boards;
        _vis = vis;
        _notifier = notifier;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<BoardDto>>> Mine()
    {
        var rows = await _db.BoardMemberships
            .Where(m => m.UserId == UserId)
            .Include(m => m.Board!).ThenInclude(b => b.Members)
            .Include(m => m.Board!).ThenInclude(b => b.Problems)
            .ToListAsync();

        return rows
            .OrderByDescending(m => m.Board!.CreatedAt)
            .Select(m => ToDto(m.Board!, m.Role))
            .ToList();
    }

    [HttpPost]
    [Authorize(Roles = "Teacher")]
    public async Task<ActionResult<BoardDto>> Create(CreateBoardDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Title)) return BadRequest("Title is required.");

        var board = new Board
        {
            Title = dto.Title.Trim(),
            OwnerId = UserId,
            JoinCode = await _boards.GenerateJoinCodeAsync(),
        };
        _db.Boards.Add(board);
        _db.BoardMemberships.Add(new BoardMembership
        {
            Board = board,
            UserId = UserId,
            Role = MembershipRole.Owner,
        });
        await _db.SaveChangesAsync();

        return ToDto(board, MembershipRole.Owner);
    }

    [HttpPost("join")]
    public async Task<ActionResult<BoardDto>> Join(JoinBoardDto dto)
    {
        var code = (dto.Code ?? "").Trim().ToUpperInvariant();
        var board = await _db.Boards
            .Include(b => b.Members)
            .Include(b => b.Problems)
            .FirstOrDefaultAsync(b => b.JoinCode == code);
        if (board is null) return NotFound("No board with that code.");

        var existing = board.Members.FirstOrDefault(m => m.UserId == UserId);
        if (existing is not null) return ToDto(board, existing.Role);

        var role = CurrentRole == "Teacher" ? MembershipRole.Teacher : MembershipRole.Student;
        _db.BoardMemberships.Add(new BoardMembership { BoardId = board.Id, UserId = UserId, Role = role });
        await _db.SaveChangesAsync();

        return ToDto(board, role);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<BoardDto>> Get(int id)
    {
        var membership = await _boards.GetMembershipAsync(id, UserId);
        if (membership is null) return Forbid();

        var board = await _db.Boards
            .Include(b => b.Members)
            .Include(b => b.Problems)
            .FirstAsync(b => b.Id == id);
        return ToDto(board, membership.Role);
    }

    [HttpPatch("{id:int}")]
    public async Task<ActionResult<BoardDto>> Update(int id, UpdateBoardDto dto)
    {
        var board = await _db.Boards.Include(b => b.Members).Include(b => b.Problems)
            .FirstOrDefaultAsync(b => b.Id == id);
        if (board is null) return NotFound();
        if (board.OwnerId != UserId) return Forbid();

        board.ExamMode = dto.ExamMode;
        await _db.SaveChangesAsync();
        await _notifier.ExamModeChangedAsync(board.Id, board.ExamMode);

        return ToDto(board, MembershipRole.Owner);
    }

    [HttpGet("{id:int}/progress")]
    public async Task<ActionResult<ProgressBoardDto>> Progress(int id)
    {
        var progress = await _boards.BuildProgressAsync(id, UserId);
        return progress is null ? Forbid() : progress;
    }

    private BoardDto ToDto(Board b, MembershipRole role) => new(
        b.Id, b.Title, b.JoinCode, b.ExamMode,
        b.OwnerId == UserId, role.ToString(),
        b.Members?.Count(m => m.Role == MembershipRole.Student) ?? 0,
        b.Problems?.Count ?? 0);
}
