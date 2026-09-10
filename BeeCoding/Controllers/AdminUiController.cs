using System.Text.Json;
using BeeCoding.Data;
using BeeCoding.Models;
using BeeCoding.Services;
using BeeCoding.Services.Judge;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BeeCoding.Controllers;

/// <summary>
/// The admin panel. Cookie-authed; the "Admin" role is granted to emails in
/// <c>Admin:Emails</c> (see AuthController). Separate from the token-authed
/// <see cref="AdminController"/> which is for scripting.
/// </summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin-ui")]
public class AdminUiController(AppDbContext db, IConfiguration cfg) : ApiControllerBase
{
    private readonly AppDbContext _db = db;
    private readonly IConfiguration _cfg = cfg;

    private bool IsAdminEmail(string email) =>
        (_cfg["Admin:Emails"] ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(e => string.Equals(e, email, StringComparison.OrdinalIgnoreCase));

    // ---- users -------------------------------------------------------------
    [HttpGet("users")]
    public async Task<ActionResult<IEnumerable<AdminUserRow>>> Users([FromQuery] string? q)
    {
        var query = _db.Users.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var n = q.Trim();
            query = query.Where(u => EF.Functions.Like(u.Email, $"%{n}%") || EF.Functions.Like(u.DisplayName, $"%{n}%"));
        }

        var users = await query.OrderBy(u => u.Id).ToListAsync();
        var ownedByUser = await _db.Boards.GroupBy(b => b.OwnerId)
            .Select(g => new { g.Key, C = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.C);
        var subsByUser = await _db.Submissions.GroupBy(s => s.UserId)
            .Select(g => new { g.Key, C = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.C);

        return users.Select(u => new AdminUserRow(
            u.Id, u.Email, u.DisplayName, u.Role.ToString(), IsAdminEmail(u.Email),
            u.Xp, u.CreatedAt,
            ownedByUser.GetValueOrDefault(u.Id), subsByUser.GetValueOrDefault(u.Id))).ToList();
    }

    // ---- AI usage --------------------------------------------------------------
    [HttpGet("ai-usage")]
    public async Task<ActionResult<IEnumerable<AdminAiUsageRow>>> AiUsage()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        var rows = await _db.AiUsages.Include(x => x.User).ToListAsync();

        static AdminAiUsageBucket Sum(IEnumerable<AiUsage> xs)
        {
            int c = 0; long p = 0, k = 0;
            foreach (var x in xs) { c += x.Calls; p += x.PromptTokens; k += x.CompletionTokens; }
            return new AdminAiUsageBucket(c, p, k, p + k);
        }

        return rows.GroupBy(x => x.UserId)
            .Select(g => new AdminAiUsageRow(
                g.Key,
                g.First().User?.Email ?? "?",
                g.First().User?.DisplayName ?? "?",
                Sum(g.Where(x => x.Day == today)),
                Sum(g.Where(x => x.Day >= monthStart)),
                Sum(g)))
            .OrderByDescending(r => r.AllTime.TotalTokens)
            .ToList();
    }

    // ---- problems export / import -------------------------------------------
    [HttpGet("problems/export")]
    public async Task<IActionResult> Export()
    {
        var problems = await _db.BankProblems.Include(b => b.Owner).Include(b => b.TestCases)
            .OrderBy(b => b.Id).ToListAsync();

        var bundle = new AdminProblemBundle(1, DateTime.UtcNow, problems.Select(b => new AdminProblemItem(
            b.Owner?.Email ?? "", b.Title, b.StatementMarkdown, b.Language, b.Level.ToString(),
            b.Tags, b.StarterCode, b.TimeLimitMs, b.MemoryLimitKb, b.IsPublic,
            b.BannedHeaders, b.BannedSymbols,
            b.TestCases.OrderBy(t => t.Position).ThenBy(t => t.Id)
                .Select(t => new AdminProblemTest(t.Stdin, t.ExpectedStdout, t.IsSample, t.Points, t.Position))
                .ToList())).ToList());

        var json = JsonSerializer.Serialize(bundle, new JsonSerializerOptions { WriteIndented = true });
        return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json",
            $"beecoding-problems-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json");
    }

    [HttpPost("problems/import")]
    public async Task<ActionResult<AdminImportResult>> Import(AdminProblemBundle bundle, [FromQuery] bool replaceExisting = true)
    {
        int created = 0, updated = 0, skipped = 0;
        var errors = new List<string>();

        var firstTeacher = await _db.Users.Where(u => u.Role == UserRole.Teacher).OrderBy(u => u.Id).FirstOrDefaultAsync();

        foreach (var item in bundle.Problems ?? new())
        {
            var title = (item.Title ?? "").Trim();
            try
            {
                if (title.Length == 0) { errors.Add("(untitled): title required"); continue; }
                if (item.Tests is null || item.Tests.Count == 0) { errors.Add($"{title}: needs at least one test"); continue; }

                var owner = string.IsNullOrWhiteSpace(item.OwnerEmail)
                    ? firstTeacher
                    : await _db.Users.FirstOrDefaultAsync(u => u.Email == item.OwnerEmail.Trim());
                owner ??= firstTeacher;
                if (owner is null) { errors.Add($"{title}: no teacher account to own it"); continue; }

                var existing = await _db.BankProblems.Include(b => b.TestCases)
                    .FirstOrDefaultAsync(b => b.OwnerId == owner.Id && b.Title == title);
                if (existing is not null && !replaceExisting) { skipped++; continue; }

                var b = existing ?? new BankProblem { OwnerId = owner.Id, CreatedAt = DateTime.UtcNow };
                b.Title = title;
                b.StatementMarkdown = item.StatementMarkdown ?? "";
                b.Language = NativeCompiler.Normalize(item.Language);
                b.Level = Mapping.ParseLevel(item.Level);
                b.Tags = Mapping.NormalizeTags(item.Tags);
                b.StarterCode = item.StarterCode ?? "";
                b.BannedHeaders = SourcePolicy.Normalize(item.BannedHeaders);
                b.BannedSymbols = SourcePolicy.NormalizeSymbols(item.BannedSymbols);
                b.TimeLimitMs = Math.Clamp(item.TimeLimitMs <= 0 ? 1000 : item.TimeLimitMs, 100, 10_000);
                b.MemoryLimitKb = Math.Clamp(item.MemoryLimitKb <= 0 ? 32_768 : item.MemoryLimitKb, 4_096, 512_000);
                b.IsPublic = item.IsPublic;
                b.UpdatedAt = DateTime.UtcNow;

                if (existing is not null) _db.BankTestCases.RemoveRange(existing.TestCases);
                b.TestCases = item.Tests.Select((t, i) => new BankTestCase
                {
                    Stdin = t.Stdin ?? "", ExpectedStdout = t.ExpectedStdout ?? "",
                    IsSample = t.IsSample, Points = Math.Max(0, t.Points), Position = t.Position != 0 ? t.Position : i,
                }).ToList();

                if (existing is null) { _db.BankProblems.Add(b); created++; } else updated++;
                await _db.SaveChangesAsync();
            }
            catch (Exception ex) { errors.Add($"{(title.Length == 0 ? "(untitled)" : title)}: {ex.Message}"); }
        }

        return new AdminImportResult(created, updated, skipped, errors);
    }
}
