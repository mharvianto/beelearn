using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using BeeCoding.Services.Judge;

namespace BeeCoding.Services;

/// <summary>Minimum spacing between a user's run/submit requests.</summary>
public class RateLimiter(IOptions<JudgeOptions> opt)
{
    private readonly ConcurrentDictionary<int, long> _last = new();
    private readonly long _minTicks = TimeSpan.FromMilliseconds(opt.Value.RateLimitMs).Ticks;

    /// <summary>Returns true if allowed; records the timestamp when allowed.</summary>
    public bool TryAcquire(int userId)
    {
        var now = DateTime.UtcNow.Ticks;
        var prev = _last.GetOrAdd(userId, 0);
        if (now - prev < _minTicks) return false;
        _last[userId] = now;
        return true;
    }
}
