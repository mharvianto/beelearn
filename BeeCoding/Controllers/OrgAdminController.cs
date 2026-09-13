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
public class OrgAdminController(AppDbContext db, OrgAccess access, AuditLog audit, AiRuntimeSettings aiRuntime, AiProviderRuntime aiProviderRuntime) : ApiControllerBase
{
    private readonly AppDbContext _db = db;
    private readonly OrgAccess _access = access;
    private readonly AuditLog _audit = audit;
    private readonly AiRuntimeSettings _aiRuntime = aiRuntime;
    private readonly AiProviderRuntime _aiProviderRuntime = aiProviderRuntime;

    /// <summary>Organizations the caller administers — for the org picker. Empty for a
    /// user who administers none (most users, including most super admins' everyday use).</summary>
    [HttpGet("mine")]
    public async Task<ActionResult<List<OrganizationDto>>> Mine()
    {
        var orgs = await _access.ManagedOrgsAsync(UserId, ActorEmail);
        return orgs.Select(o => new OrganizationDto(o.Id, o.Name, o.Slug, o.CreatedAt)).ToList();
    }

    // ---- Dashboard: at-a-glance overview, scoped to this org only ------------
    [HttpGet("{orgId:int}/dashboard")]
    public async Task<ActionResult<OrgDashboardDto>> Dashboard(int orgId)
    {
        if (!await _access.CanManageAsync(UserId, ActorEmail, orgId)) return Forbid();

        var members = await _db.OrganizationMemberships.Where(m => m.OrganizationId == orgId)
            .Select(m => new { m.UserId, m.Role, UserRole = m.User!.Role }).ToListAsync();
        var totalMembers = members.Count;
        var teacherCount = members.Count(m => m.UserRole == UserRole.Teacher);
        var studentCount = members.Count(m => m.UserRole == UserRole.Student);
        var adminCount = members.Count(m => m.Role == OrgRole.Admin);

        var totalBoards = await _db.Boards.CountAsync(b => b.OrganizationId == orgId);
        var totalProblems = await _db.Problems.CountAsync(p => p.Board!.OrganizationId == orgId);
        var totalSubmissions = await _db.Submissions.CountAsync(s => s.Problem!.Board!.OrganizationId == orgId);
        var acceptedSubmissions = await _db.Submissions.CountAsync(s =>
            s.Problem!.Board!.OrganizationId == orgId && s.Verdict == Verdict.Accepted && s.Score >= 1.0);

        // AI usage isn't tracked per-org directly (AiUsage is per-user) — sum it across this
        // org's members as the closest proxy. A user in several orgs shows up under each.
        var memberIds = members.Select(m => m.UserId).ToList();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var usage = await _db.AiUsages.Where(x => memberIds.Contains(x.UserId) && x.Day >= monthStart).ToListAsync();
        static AdminAiUsageBucket Sum(IEnumerable<AiUsage> xs)
        {
            int c = 0; long p = 0, k = 0;
            foreach (var x in xs) { c += x.Calls; p += x.PromptTokens; k += x.CompletionTokens; }
            return new AdminAiUsageBucket(c, p, k, p + k);
        }

        return new OrgDashboardDto(totalMembers, teacherCount, studentCount, adminCount,
            totalBoards, totalProblems, totalSubmissions, acceptedSubmissions,
            Sum(usage.Where(x => x.Day == today)), Sum(usage));
    }

    /// <summary>Weekly active-users + submissions, scoped to this org's boards only (bank/
    /// practice activity isn't org-scopable — a bank problem belongs to a user, not an org).</summary>
    [HttpGet("{orgId:int}/dashboard/weekly")]
    public async Task<ActionResult<List<AdminWeeklyStatDto>>> DashboardWeekly(int orgId, [FromQuery] int weeks = 12)
    {
        if (!await _access.CanManageAsync(UserId, ActorEmail, orgId)) return Forbid();
        weeks = Math.Clamp(weeks, 1, 52);

        var activity = await _db.Submissions.Where(s => s.Problem!.Board!.OrganizationId == orgId)
            .Select(s => new { s.UserId, s.CreatedAt }).ToListAsync();

        DateOnly WeekStart(DateTime dt)
        {
            var d = DateOnly.FromDateTime(dt);
            return d.AddDays(-(((int)d.DayOfWeek + 6) % 7));
        }

        return activity.GroupBy(x => WeekStart(x.CreatedAt))
            .OrderByDescending(g => g.Key).Take(weeks).OrderBy(g => g.Key)
            .Select(g => new AdminWeeklyStatDto(g.Key.ToString("yyyy-MM-dd"), g.Select(x => x.UserId).Distinct().Count(), g.Count()))
            .ToList();
    }

    /// <summary>Top tags by attempts, scoped to this org's board problems only.</summary>
    [HttpGet("{orgId:int}/dashboard/topics")]
    public async Task<ActionResult<List<AdminTopicStatDto>>> DashboardTopics(int orgId, [FromQuery] int take = 8)
    {
        if (!await _access.CanManageAsync(UserId, ActorEmail, orgId)) return Forbid();
        take = Math.Clamp(take, 1, 50);

        var problems = await _db.Problems.Where(p => p.Board!.OrganizationId == orgId)
            .Select(p => new { p.Id, p.Tags }).ToListAsync();
        var problemIds = problems.Select(p => p.Id).ToHashSet();
        var subs = await _db.Submissions.Where(s => problemIds.Contains(s.ProblemId))
            .Select(s => new { s.ProblemId, s.UserId, s.Verdict, s.Score }).ToListAsync();

        var byTag = new Dictionary<string, (int Attempts, int Accepted, HashSet<int> Solvers)>();
        static IEnumerable<string> TagsOf(string? t) =>
            (t ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                     .Select(x => x.ToLowerInvariant()).Distinct();

        foreach (var p in problems)
        {
            var pSubs = subs.Where(s => s.ProblemId == p.Id).ToList();
            foreach (var tag in TagsOf(p.Tags))
            {
                var (attempts, accepted, solvers) = byTag.TryGetValue(tag, out var v) ? v : (0, 0, new HashSet<int>());
                attempts += pSubs.Count;
                foreach (var s in pSubs.Where(s => s.Verdict == Verdict.Accepted && s.Score >= 1.0)) { accepted++; solvers.Add(s.UserId); }
                byTag[tag] = (attempts, accepted, solvers);
            }
        }

        return byTag.OrderByDescending(kv => kv.Value.Attempts).Take(take)
            .Select(kv => new AdminTopicStatDto(kv.Key, kv.Value.Attempts, kv.Value.Accepted,
                kv.Value.Attempts > 0 ? kv.Value.Accepted / (double)kv.Value.Attempts : 0))
            .ToList();
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

    // ---- AI provider/credential: this org's own override -----------------------
    [HttpGet("{orgId:int}/ai-provider")]
    public async Task<ActionResult<AiProviderConfigDto>> GetAiProvider(int orgId)
    {
        if (!await _access.CanManageAsync(UserId, ActorEmail, orgId)) return Forbid();
        var row = await _db.AiProviderConfigs.FirstOrDefaultAsync(x => x.OrganizationId == orgId);
        return new AiProviderConfigDto(!string.IsNullOrEmpty(row?.ApiKey), ApiKeyPreview(row?.ApiKey), row?.BaseUrl, row?.Model, row?.GenerateModel);
    }

    /// <summary>Bring-your-own AI credential for this organization — falls back to the
    /// platform default (and from there to appsettings.json) field-by-field when left blank.
    /// Leave ApiKey blank to keep whatever key is already saved; use DELETE
    /// ai-provider/api-key to actually clear it.</summary>
    [HttpPut("{orgId:int}/ai-provider")]
    public async Task<ActionResult<AiProviderConfigDto>> SetAiProvider(int orgId, AiSetProviderConfigDto dto)
    {
        if (!await _access.CanManageAsync(UserId, ActorEmail, orgId)) return Forbid();
        var row = await _db.AiProviderConfigs.FirstOrDefaultAsync(x => x.OrganizationId == orgId);
        if (row is null) { row = new AiProviderConfig { OrganizationId = orgId }; _db.AiProviderConfigs.Add(row); }
        if (!string.IsNullOrWhiteSpace(dto.ApiKey)) row.ApiKey = dto.ApiKey.Trim();
        row.BaseUrl = string.IsNullOrWhiteSpace(dto.BaseUrl) ? null : dto.BaseUrl.Trim();
        row.Model = string.IsNullOrWhiteSpace(dto.Model) ? null : dto.Model.Trim();
        row.GenerateModel = string.IsNullOrWhiteSpace(dto.GenerateModel) ? null : dto.GenerateModel.Trim();
        row.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _aiProviderRuntime.SetOrg(orgId, row.ApiKey, row.BaseUrl, row.Model, row.GenerateModel);
        await _audit.RecordAsync(UserId, ActorEmail, "org-ai-provider-set", "Organization", orgId, "AI provider override");
        return new AiProviderConfigDto(!string.IsNullOrEmpty(row.ApiKey), ApiKeyPreview(row.ApiKey), row.BaseUrl, row.Model, row.GenerateModel);
    }

    [HttpDelete("{orgId:int}/ai-provider/api-key")]
    public async Task<IActionResult> ClearAiProviderKey(int orgId)
    {
        if (!await _access.CanManageAsync(UserId, ActorEmail, orgId)) return Forbid();
        var row = await _db.AiProviderConfigs.FirstOrDefaultAsync(x => x.OrganizationId == orgId);
        if (row is null || row.ApiKey is null) return NoContent();
        row.ApiKey = null;
        row.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        _aiProviderRuntime.SetOrg(orgId, null, row.BaseUrl, row.Model, row.GenerateModel);
        await _audit.RecordAsync(UserId, ActorEmail, "org-ai-provider-clear-key", "Organization", orgId, "AI provider override");
        return NoContent();
    }

    private static string? ApiKeyPreview(string? key) =>
        string.IsNullOrEmpty(key) ? null : $"••••{key[Math.Max(0, key.Length - 4)..]}";
}
