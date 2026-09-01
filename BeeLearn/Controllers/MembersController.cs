using BeeLearn.Data;
using BeeLearn.Models;
using BeeLearn.Services;
using BeeLearn.Services.Judge;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BeeLearn.Controllers;

[Authorize]
[Route("api/boards/{boardId:int}/members")]
public class MembersController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly BoardService _boards;
    private readonly VisibilityService _vis;
    private readonly IBoardNotifier _notifier;

    public MembersController(AppDbContext db, BoardService boards, VisibilityService vis, IBoardNotifier notifier)
    {
        _db = db;
        _boards = boards;
        _vis = vis;
        _notifier = notifier;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<MemberDto>>> List(int boardId)
    {
        var me = await _boards.GetMembershipAsync(boardId, UserId);
        if (me is null) return Forbid();
        bool staff = _vis.IsStaff(me.Role);

        var members = await _db.BoardMemberships
            .Where(m => m.BoardId == boardId)
            .Include(m => m.User)
            .OrderBy(m => m.Role)
            .ThenBy(m => m.User!.DisplayName)
            .ToListAsync();

        return members
            .Select(m => new MemberDto(m.UserId, m.User!.DisplayName, m.Role.ToString(),
                staff && m.HiddenByTeacher))
            .ToList();
    }

    /// <summary>Feature 5 (per student): owner hides/shows one student's cells from other students.</summary>
    [HttpPatch("{userId:int}")]
    public async Task<ActionResult<MemberDto>> Update(int boardId, int userId, UpdateMemberDto dto)
    {
        var board = await _db.Boards.FindAsync(boardId);
        if (board is null) return NotFound();
        if (board.OwnerId != UserId) return Forbid();

        var target = await _db.BoardMemberships
            .Include(m => m.User)
            .FirstOrDefaultAsync(m => m.BoardId == boardId && m.UserId == userId);
        if (target is null) return NotFound();
        if (target.Role != MembershipRole.Student) return BadRequest("Only students can be hidden.");

        target.HiddenByTeacher = dto.HiddenByTeacher;
        await _db.SaveChangesAsync();
        await _notifier.MemberVisibilityChangedAsync(boardId, userId, dto.HiddenByTeacher);

        return new MemberDto(target.UserId, target.User!.DisplayName, target.Role.ToString(), target.HiddenByTeacher);
    }
}
