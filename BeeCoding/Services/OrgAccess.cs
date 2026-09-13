using BeeCoding.Data;
using BeeCoding.Models;
using Microsoft.EntityFrameworkCore;

namespace BeeCoding.Services;

/// <summary>
/// Authorization for the Org Admin surface (OrgAdminController) — scoped strictly to one
/// organization's own data, unlike the platform-wide Admin:Emails/User.IsAdmin super-admin
/// (AdminAccess), who can act on every organization without an explicit membership row.
/// </summary>
public class OrgAccess(AppDbContext db, AdminAccess admin)
{
    private readonly AppDbContext _db = db;
    private readonly AdminAccess _admin = admin;

    /// <summary>True if the caller can manage organizationId's own admin surface — either a
    /// platform super admin, or an OrgRole.Admin member of that specific organization.</summary>
    public async Task<bool> CanManageAsync(int userId, string? actorEmail, int organizationId)
    {
        if (_admin.IsAdminEmail(actorEmail)) return true;
        return await _db.OrganizationMemberships.AnyAsync(m =>
            m.OrganizationId == organizationId && m.UserId == userId && m.Role == OrgRole.Admin);
    }

    /// <summary>Organizations this user administers — for the "which orgs can I manage"
    /// picker. A platform super admin gets every organization.</summary>
    public async Task<List<Organization>> ManagedOrgsAsync(int userId, string? actorEmail)
    {
        if (_admin.IsAdminEmail(actorEmail)) return await _db.Organizations.OrderBy(o => o.Name).ToListAsync();
        return await _db.OrganizationMemberships.Where(m => m.UserId == userId && m.Role == OrgRole.Admin)
            .Select(m => m.Organization!).OrderBy(o => o.Name).ToListAsync();
    }
}
