using BeeCoding.Services.Judge;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;

// A low-privilege worker: pulls jobs from the Redis broker, compiles + runs untrusted code
// in the sandbox, publishes the verdict back. Holds NO database, SignalR, auth or API-key.
// The web tier persists verdicts + notifies (see BeeCoding/Services/GradeResultConsumer.cs,
// DEPLOY.md §2.6).

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<JudgeOptions>(builder.Configuration.GetSection("Judge"));
var judge = builder.Configuration.GetSection("Judge").Get<JudgeOptions>() ?? new JudgeOptions();

if (!judge.Queue.UseRedis)
    throw new InvalidOperationException("BeeCoding.Judge needs Judge:Queue:Backend=redis.");
var redisConn = judge.Queue.RedisConnectionString
    ?? throw new InvalidOperationException("Judge:Queue:RedisConnectionString is required.");

builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConn));
builder.Services.AddSingleton<RedisJudgeQueue>();
builder.Services.AddSingleton<IJudgeJobSource>(sp => sp.GetRequiredService<RedisJudgeQueue>());

builder.Services.AddSingleton<NativeToolchain>();
builder.Services.AddSingleton<NativeCompiler>();
builder.Services.AddSingleton<NativeSandbox>();
builder.Services.AddHostedService<JudgeWorker>();

var host = builder.Build();
host.Run();
