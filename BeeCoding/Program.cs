using BeeCoding.Data;
using BeeCoding.Hubs;
using BeeCoding.Services;
using BeeCoding.Services.Judge;
using BeeCoding.Services.Lsp;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// journald integration + Type=notify readiness when run under systemd; no-op otherwise.
builder.Host.UseSystemd();

// Trust X-Forwarded-* from a reverse proxy (nginx) so Request.Scheme is "https"
// behind TLS termination. Only the proxy should be able to reach the app port.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
});

builder.Services.AddControllers();
builder.Services.AddSignalR();

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=beecoding.db"));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "beecoding.auth";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;   // Secure when served over HTTPS
        o.ExpireTimeSpan = TimeSpan.FromDays(7);
        o.SlidingExpiration = true;
        // API/hub calls should get 401/403, never an HTML redirect.
        o.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
        o.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
    });
builder.Services.AddAuthorization();

builder.Services.Configure<JudgeOptions>(builder.Configuration.GetSection("Judge"));
builder.Services.Configure<LspOptions>(builder.Configuration.GetSection("Lsp"));
builder.Services.AddSingleton<LspEndpoint>();

builder.Services.AddSingleton<PasswordService>();
builder.Services.AddSingleton<NativeToolchain>();
builder.Services.AddSingleton<NativeCompiler>();
builder.Services.AddSingleton<NativeSandbox>();
builder.Services.AddSingleton<JudgeQueue>();
builder.Services.AddSingleton<PresenceTracker>();
builder.Services.AddSingleton<DraftStore>();
builder.Services.AddSingleton<StatementImageService>();
builder.Services.AddSingleton<RateLimiter>();
builder.Services.AddSingleton<IBoardNotifier, BoardNotifier>();
builder.Services.AddHostedService<JudgeWorker>();

builder.Services.AddScoped<VisibilityService>();
builder.Services.AddScoped<BoardService>();
builder.Services.AddScoped<WallService>();
builder.Services.AddScoped<ProgressService>();

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
    await DbSeeder.SeedBankAsync(db);

    // Backfill public slugs for boards created before slugs existed.
    var boardSvc = scope.ServiceProvider.GetRequiredService<BeeCoding.Services.BoardService>();
    var slugless = await db.Boards.Where(b => b.Slug == null || b.Slug == "").ToListAsync();
    foreach (var b in slugless) b.Slug = await boardSvc.GenerateSlugAsync();
    if (slugless.Count > 0) await db.SaveChangesAsync();

    // Backfill wall posts for submissions made before the wall existed.
    var missing = await db.Submissions
        .Select(s => new { s.UserId, s.ProblemId })
        .Distinct()
        .Where(k => !db.Posts.Any(p => p.ProblemId == k.ProblemId && p.UserId == k.UserId))
        .ToListAsync();
    foreach (var k in missing)
    {
        var boardId = await db.Problems.Where(p => p.Id == k.ProblemId).Select(p => p.BoardId).FirstAsync();
        db.Posts.Add(new BeeCoding.Models.Post { BoardId = boardId, ProblemId = k.ProblemId, UserId = k.UserId });
    }
    if (missing.Count > 0) await db.SaveChangesAsync();
}

// Build the sandbox runner + probe capabilities before serving traffic.
app.Services.GetRequiredService<NativeToolchain>().Initialize();

app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
    app.UseCors(DevCors);

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseWebSockets();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<BoardHub>("/hubs/board");

// C/C++ language server bridge (clangd). No-op unless Lsp:Enabled + clangd on PATH.
app.MapGet("/lsp/cpp", (HttpContext c, LspEndpoint ep) => ep.HandleAsync(c)).RequireAuthorization();

app.MapFallbackToFile("index.html");

app.Run();
