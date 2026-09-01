using BeeLearn.Data;
using BeeLearn.Hubs;
using BeeLearn.Services;
using BeeLearn.Services.Judge;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSignalR();

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=beelearn.db"));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "beelearn.auth";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.ExpireTimeSpan = TimeSpan.FromDays(7);
        o.SlidingExpiration = true;
        // API/hub calls should get 401/403, never an HTML redirect.
        o.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
        o.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
    });
builder.Services.AddAuthorization();

builder.Services.Configure<JudgeOptions>(builder.Configuration.GetSection("Judge"));

builder.Services.AddSingleton<PasswordService>();
builder.Services.AddSingleton<NativeToolchain>();
builder.Services.AddSingleton<NativeCompiler>();
builder.Services.AddSingleton<NativeSandbox>();
builder.Services.AddSingleton<JudgeQueue>();
builder.Services.AddSingleton<PresenceTracker>();
builder.Services.AddSingleton<RateLimiter>();
builder.Services.AddSingleton<IBoardNotifier, BoardNotifier>();
builder.Services.AddHostedService<JudgeWorker>();

builder.Services.AddScoped<VisibilityService>();
builder.Services.AddScoped<BoardService>();
builder.Services.AddScoped<WallService>();

const string DevCors = "dev-spa";
builder.Services.AddCors(o => o.AddPolicy(DevCors, p => p
    .WithOrigins("http://localhost:5173", "https://localhost:5173")
    .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(db, scope.ServiceProvider.GetRequiredService<PasswordService>());

    // Backfill wall posts for submissions made before the wall existed.
    var missing = await db.Submissions
        .Select(s => new { s.UserId, s.ProblemId })
        .Distinct()
        .Where(k => !db.Posts.Any(p => p.ProblemId == k.ProblemId && p.UserId == k.UserId))
        .ToListAsync();
    foreach (var k in missing)
    {
        var boardId = await db.Problems.Where(p => p.Id == k.ProblemId).Select(p => p.BoardId).FirstAsync();
        db.Posts.Add(new BeeLearn.Models.Post { BoardId = boardId, ProblemId = k.ProblemId, UserId = k.UserId });
    }
    if (missing.Count > 0) await db.SaveChangesAsync();
}

// Build the sandbox runner + probe capabilities before serving traffic.
app.Services.GetRequiredService<NativeToolchain>().Initialize();

if (app.Environment.IsDevelopment())
    app.UseCors(DevCors);

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<BoardHub>("/hubs/board");
app.MapFallbackToFile("index.html");

app.Run();
