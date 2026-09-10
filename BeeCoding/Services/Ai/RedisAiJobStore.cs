using System.Text.Json;
using BeeCoding.Services.Realtime;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace BeeCoding.Services.Ai;

/// <summary>
/// Redis-backed <see cref="IAiJobStore"/> so a generate/regenerate job started on one web
/// pod can be polled from any pod. Job blobs and the per-user "running" set carry a TTL
/// (<see cref="IAiJobStore.Ttl"/>), refreshed on write.
/// </summary>
public sealed class RedisAiJobStore(IConnectionMultiplexer mux, IOptions<RealtimeStoreOptions> rt) : IAiJobStore
{
    private readonly IDatabase _db = mux.GetDatabase();
    private readonly string _prefix = rt.Value.KeyPrefix + "aijob:";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private string JobKey(string id) => _prefix + id;
    private string UserKey(int userId) => $"{_prefix}user:{userId}";

    public async Task<AiJob> CreateAsync(int userId)
    {
        var j = new AiJob { Id = Guid.NewGuid().ToString("N"), UserId = userId };
        await _db.StringSetAsync(JobKey(j.Id), JsonSerializer.Serialize(j, Json), IAiJobStore.Ttl);
        await _db.SetAddAsync(UserKey(userId), j.Id);
        await _db.KeyExpireAsync(UserKey(userId), IAiJobStore.Ttl);
        return j;
    }

    public async Task<AiJob?> GetAsync(string id)
    {
        var v = await _db.StringGetAsync(JobKey(id));
        return v.IsNullOrEmpty ? null : JsonSerializer.Deserialize<AiJob>((string)v!, Json);
    }

    public async Task<int> RunningForAsync(int userId)
    {
        var ids = await _db.SetMembersAsync(UserKey(userId));
        if (ids.Length == 0) return 0;

        var keys = ids.Select(id => (RedisKey)JobKey((string)id!)).ToArray();
        var blobs = await _db.StringGetAsync(keys);

        int running = 0;
        for (int i = 0; i < ids.Length; i++)
        {
            if (blobs[i].IsNullOrEmpty) { await _db.SetRemoveAsync(UserKey(userId), ids[i]); continue; }
            var job = JsonSerializer.Deserialize<AiJob>((string)blobs[i]!, Json);
            if (job?.Status == "running") running++;
            else await _db.SetRemoveAsync(UserKey(userId), ids[i]);   // finished — drop from the set
        }
        return running;
    }

    public Task CompleteAsync(string id, object result) =>
        MutateAsync(id, j => { j.Result = result; j.Status = "done"; j.Finished = DateTime.UtcNow; });

    public Task FailAsync(string id, string message, string? compilerOutput = null, string? stderr = null) =>
        MutateAsync(id, j =>
        {
            j.Message = message; j.CompilerOutput = compilerOutput; j.Stderr = stderr;
            j.Status = "error"; j.Finished = DateTime.UtcNow;
        });

    private async Task MutateAsync(string id, Action<AiJob> change)
    {
        var v = await _db.StringGetAsync(JobKey(id));
        if (v.IsNullOrEmpty) return;
        var job = JsonSerializer.Deserialize<AiJob>((string)v!, Json);
        if (job is null || job.Status != "running") return;

        change(job);
        await _db.StringSetAsync(JobKey(id), JsonSerializer.Serialize(job, Json), IAiJobStore.Ttl);
        await _db.SetRemoveAsync(UserKey(job.UserId), id);
    }
}
