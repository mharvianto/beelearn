using BeeCoding.Data;
using BeeCoding.Models;

namespace BeeCoding.Services;

/// <summary>Records moderation actions (delete/restore/purge) for the admin panel's audit log.</summary>
public class AuditLog(AppDbContext db)
{
    private readonly AppDbContext _db = db;

    public async Task RecordAsync(int actorUserId, string actorEmail, string action, string targetType, int targetId, string targetLabel)
    {
        _db.AuditLogEntries.Add(new AuditLogEntry
        {
            ActorUserId = actorUserId,
            ActorEmail = actorEmail,
            Action = action,
            TargetType = targetType,
            TargetId = targetId,
            TargetLabel = targetLabel,
        });
        await _db.SaveChangesAsync();
    }
}
