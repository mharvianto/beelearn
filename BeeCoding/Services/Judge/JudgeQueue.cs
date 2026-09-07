using System.Threading.Channels;
using BeeCoding.Models;
using Microsoft.Extensions.Options;

namespace BeeCoding.Services.Judge;

public abstract record JudgeJob;

public sealed record SubmissionJob(int SubmissionId) : JudgeJob;

public sealed record BankSubmissionJob(int BankSubmissionId) : JudgeJob;

public sealed record RunJob(
    string Language,
    string Code,
    string Stdin,
    int TimeLimitMs,
    int MemoryLimitKb,
    TaskCompletionSource<RunResultDto> Completion) : JudgeJob;

public class JudgeQueue
{
    private readonly Channel<JudgeJob> _channel;

    public JudgeQueue(IOptions<JudgeOptions> options)
    {
        _channel = Channel.CreateBounded<JudgeJob>(new BoundedChannelOptions(options.Value.QueueCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
        });
    }

    public ChannelReader<JudgeJob> Reader => _channel.Reader;

    public ValueTask EnqueueAsync(JudgeJob job, CancellationToken ct = default) =>
        _channel.Writer.WriteAsync(job, ct);

    public async Task<RunResultDto> EnqueueRunAsync(RunJob job, CancellationToken ct = default)
    {
        await _channel.Writer.WriteAsync(job, ct);
        return await job.Completion.Task.WaitAsync(ct);
    }
}
