using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using BeeLearn.Services.Judge;

namespace BeeLearn.Services;

/// <summary>Minimum spacing between a user's run/submit requests.</summary>
public class RateLimiter
{
    private readonly ConcurrentDictionary<int, long> _last = new();
    private readonly long _minTicks;

    public RateLimiter(IOptions<JudgeOptions> opt) =>
        _minTicks = TimeSpan.FromMilliseconds(opt.Value.RateLimitMs).Ticks;

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
