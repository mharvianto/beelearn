using BeeCoding.Models;
using BeeCoding.Services;
using BeeCoding.Services.Judge;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace BeeCoding.Controllers;

[Authorize]
[Route("api/run")]
public class RunController : ApiControllerBase
{
    private readonly JudgeQueue _queue;
    private readonly RateLimiter _rate;
    private readonly JudgeOptions _opt;

    public RunController(JudgeQueue queue, RateLimiter rate, IOptions<JudgeOptions> opt)
    {
        _queue = queue;
        _rate = rate;
        _opt = opt.Value;
    }

    /// <summary>Ad-hoc compile + run with custom stdin. Not judged, not persisted.</summary>
    [HttpPost]
    public async Task<ActionResult<RunResultDto>> Run(RunDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Code)) return BadRequest("Code is empty.");
        if (dto.Code.Length > 200_000) return BadRequest("Code is too large.");
        if (!_rate.TryAcquire(UserId)) return StatusCode(429, "Slow down a moment and try again.");

        var job = new RunJob(
            dto.Language, dto.Code, dto.Stdin ?? "",
            _opt.RunTimeLimitMs, _opt.RunMemoryLimitKb,
            new TaskCompletionSource<RunResultDto>(TaskCreationOptions.RunContinuationsAsynchronously));

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            return await _queue.EnqueueRunAsync(job, timeout.Token);
        }
        catch (OperationCanceledException)
        {
            return StatusCode(504, "The judge is busy. Try again shortly.");
        }
    }
}
