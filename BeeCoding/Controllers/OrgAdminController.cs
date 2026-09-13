using BeeCoding.Data;
using BeeCoding.Models;
using BeeCoding.Services;
using BeeCoding.Services.Ai;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BeeCoding.Controllers;

/// <summary>
/// Scoped admin surface for one organization at a time — a university's own Org Admin
/// manages exactly their own members/boards/AI settings, and never sees another
/// organization's data. Deliberately smaller than the platform AdminUiController: no
/// trash/purge, no audit log, no analytics export here — those stay platform-super-admin
/// only (AdminUiController) since they touch data across every organization at once.
/// Every action checks OrgAccess.CanManageAsync itself (a super admin passes too) rather
/// than a static [Authorize(Policy=...)], since "can manage" is parameterized by which
/// org the URL names.
/// </summary>
[ApiController]
[Authorize]
[Route("api/org-admin")]
public class OrgAdminController(AppDbContext db, OrgAccess access, AuditLog audit, AiRuntimeSettings aiRuntime) : ApiControllerBase
{
    private readonly AppDbContext _db = db;
    private readonly OrgAccess _access = access;
    private readonly AuditLog _audit = audit;
    private readonly AiRuntimeSettings _aiRuntime = aiRuntime;

    /// <summary>Organizations the caller administers — for the org picker. Empty for a
    /// user who administers none (most users, including most super admins' everyday use).</summary>
    [HttpGet("mine")]
    public async Task<ActionResult<List<OrganizationDto>>> Mine()
    {
        var orgs = await _access.ManagedOrgsAsync(UserId, ActorEmail);
        return orgs.Select(o => new OrganizationDto(o.Id, o.Name, o.Slug, o.CreatedAt)).ToList();
    }

    [HttpGet("{orgId:int}/summary")]
    public async Task<ActionResult<OrgSummaryDto>> Summary(int orgId)
    {
        if (!await _access.CanManageAsync(UserId, ActorEmail, orgId)) return Forbid();
        var memberCount = await _db.OrganizationMemberships.CountAsync(m => m.OrganizationId == orgId);
        var boardCount = await _db.Boards.CountAsync(b => b.OrganizationId == orgId);
        return new OrgSummaryDto(memberCount, boardCount);
    }

    [HttpGet("{orgId:int}/members")]
    public async Task<ActionResult<List<OrgMemberRow>>> Members(int orgId)
    {
        if (!await _access.CanManageAsync(UserId, ActorEmail, orgId)) return Forbid();
        return await _db.OrganizationMemberships.Where(m => m.OrganizationId == orgId)
            .OrderBy(m => m.JoinedAt)
            .Select(m => new OrgMemberRow(m.UserId, m.User!.Email, m.User.DisplayName, m.Role.ToString(), m.JoinedAt))
            .ToListAsync();
    }

    /// <summary>Adds an EXISTING BeeCoding user (by email) to the organization — this isn't
    /// an email invite that creates an account; have them register (or launch via this
    /// org's LTI platform, which auto-enrolls) first.</summary>
    [HttpPost("{orgId:int}/members")]
    public async Task<ActionResult<OrgMemberRow>> AddMember(int orgId, OrgAddMemberDto dto)
    {
        if (!await _access.CanManageAsync(UserId, ActorEmail, orgId)) return Forbid();
        if (!Enum.TryParse<OrgRole>(dto.OrgRole, ignoreCase: true, out var role))
            return BadRequest("OrgRole must be 'Member' or 'Admin'.");

        var email = (dto.Email ?? "").Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email && u.DeletedAt == null);
        if (user is null) return NotFound($"No BeeCoding account for '{email}' yet — they need to register (or launch via this org's LTI platform) first.");

        var existing = await _db.OrganizationMemberships.FirstOrDefaultAsync(m => m.OrganizationId == orgId && m.UserId == user.Id);
        if (existing is not null) return Conflict("Already a member.");

        var membership = new OrganizationMembership { OrganizationId = orgId, UserId = user.Id, Role = role };
        _db.OrganizationMemberships.Add(membership);
        await _db.SaveChangesAsync();
        await _audit.RecordAsync(UserId, ActorEmail, "org-member-add", "Organization", orgId, $"{user.Email} as {role}");
        return new OrgMemberRow(user.Id, user.Email, user.DisplayName, role.ToString(), membership.JoinedAt);
    }

    [HttpPut("{orgId:int}/members/{userId:int}")]
    public async Task<IActionResult> SetMemberRole(int orgId, int userId, OrgSetMemberRoleDto dto)
    {
        if (!await _access.CanManageAsync(UserId, ActorEmail, orgId)) return Forbid();
        if (!Enum.TryParse<OrgRole>(dto.OrgRole, ignoreCase: true, out var role))
            return BadRequest("OrgRole must be 'Member' or 'Admin'.");

        var membership = await _db.OrganizationMemberships.FirstOrDefaultAsync(m => m.OrganizationId == orgId && m.UserId == userId);
        if (membership is null) return NotFound();
        if (membership.Role == OrgRole.Admin && role == OrgRole.Member && await LastAdminAsync(orgId, userId))
            return Conflict("This is the last admin of this organization — promote someone else first.");

        membership.Role = role;
        await _db.SaveChangesAsync();
        await _audit.RecordAsync(UserId, ActorEmail, "org-member-role", "Organization", orgId, $"user #{userId} -> {role}");
        return NoContent();
    }

    [HttpDelete("{orgId:int}/members/{userId:int}")]
    public async Task<IActionResult> RemoveMember(int orgId, int userId)
    {
        if (!await _access.CanManageAsync(UserId, ActorEmail, orgId)) return Forbid();
        var membership = await _db.OrganizationMemberships.FirstOrDefaultAsync(m => m.OrganizationId == orgId && m.UserId == userId);
        if (membership is null) return NotFound();
        if (membership.Role == OrgRole.Admin && await LastAdminAsync(orgId, userId))
            return Conflict("This is the last admin of this organization — promote someone else first.");

        _db.OrganizationMemberships.Remove(membership);
        await _db.SaveChangesAsync();
        await _audit.RecordAsync(UserId, ActorEmail, "org-member-remove", "Organization", orgId, $"user #{userId}");
        return NoContent();
    }

    private async Task<bool> LastAdminAsync(int orgId, int excludingUserId) =>
        !await _db.OrganizationMemberships.AnyAsync(m => m.OrganizationId == orgId && m.Role == OrgRole.Admin && m.UserId != excludingUserId);

    [HttpGet("{orgId:int}/boards")]
    public async Task<ActionResult<List<OrgBoardRow>>> Boards(int orgId)
    {
        if (!await _access.CanManageAsync(UserId, ActorEmail, orgId)) return Forbid();
        return await _db.Boards.Where(b => b.OrganizationId == orgId)
            .Include(b => b.Owner).Include(b => b.Members).Include(b => b.Problems)
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new OrgBoardRow(b.Id, b.Slug, b.Title, b.Owner != null ? b.Owner.Email : "?",
                b.Members.Count(m => m.Role == MembershipRole.Student), b.Problems.Count, b.CreatedAt))
            .ToListAsync();
    }

    [HttpGet("{orgId:int}/ai-settings")]
    public async Task<ActionResult<OrgAiSettingsDto>> GetAiSettings(int orgId)
    {
        if (!await _access.CanManageAsync(UserId, ActorEmail, orgId)) return Forbid();
        var row = await _db.AiSettings.FirstOrDefaultAsync(x => x.OrganizationId == orgId);
        return row is null
            ? new OrgAiSettingsDto(false, null, _aiRuntime.DailyQuotaStudent, _aiRuntime.DailyQuotaTeacher)
            : new OrgAiSettingsDto(row.Paused, row.PausedReason, row.DailyQuotaStudent, row.DailyQuotaTeacher);
    }

    /// <summary>An org's pause only stops that org's own AI usage — see AiRuntimeSettings —
    /// it can never override the platform-wide kill switch.</summary>
    [HttpPut("{orgId:int}/ai-settings")]
    public async Task<ActionResult<OrgAiSettingsDto>> SetAiSettings(int orgId, OrgAiSettingsDto dto)
    {
        if (!await _access.CanManageAsync(UserId, ActorEmail, orgId)) return Forbid();
        var row = await _db.AiSettings.FirstOrDefaultAsync(x => x.OrganizationId == orgId);
        if (row is null)
        {
            row = new BeeCoding.Models.AiSettings { OrganizationId = orgId };
            _db.AiSettings.Add(row);
        }
        row.Paused = dto.Paused;
        row.PausedReason = dto.PausedReason;
        row.DailyQuotaStudent = Math.Max(0, dto.DailyQuotaStudent);
        row.DailyQuotaTeacher = Math.Max(0, dto.DailyQuotaTeacher);
        row.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _aiRuntime.SetOrg(orgId, row.Paused, row.PausedReason, row.DailyQuotaStudent, row.DailyQuotaTeacher);
        await _audit.RecordAsync(UserId, ActorEmail, "org-ai-settings", "Organization", orgId,
            dto.Paused ? $"paused: {dto.PausedReason}" : $"quota {dto.DailyQuotaStudent}/{dto.DailyQuotaTeacher}");
        return new OrgAiSettingsDto(row.Paused, row.PausedReason, row.DailyQuotaStudent, row.DailyQuotaTeacher);
    }
}
