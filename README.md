# BeeLearn

A Padlet-style board where a teacher posts C/C++ problems, students solve them in the
browser (Monaco), run/submit code against a sandboxed judge, and a **live board** shows
everyone's progress — with layered controls over who can see whose answers.

## Stack

| Layer      | Choice |
|------------|--------|
| API        | ASP.NET Core 10 (controllers) |
| DB         | SQLite + EF Core |
| Realtime   | SignalR (`/hubs/board`) |
| Auth       | cookie auth (self-register, PBKDF2) |
| Frontend   | Vue 3 + Vite + Pinia + Tailwind v4 + Monaco, built into `wwwroot/` |
| Judge      | native `gcc`/`g++` + a small `setrlimit` runner, optional bubblewrap |

## Features

1. **Teacher posts problems** — CRUD problems + test cases per board (`ProblemsController`).
2. **Live progress board** — `BoardHub` broadcasts `progressChanged`; clients refetch the
   visibility-filtered `/api/boards/{id}/progress` grid.
3. **Run code** — `POST /api/run`, default limits **1 s CPU / 32 MB** (per-problem limits on
   submit). Enforced with `RLIMIT_CPU`, `RLIMIT_AS`, `RLIMIT_STACK`, `RLIMIT_NPROC`,
   `RLIMIT_FSIZE` + a wall-clock backstop. Verdicts: AC / WA / TLE / MLE / RE / CE.
4. **Student hides own answer** — `PATCH /api/submissions/{id} { hiddenByStudent }`.
5. **Teacher hides answers from peers** — per student
   (`PATCH /api/boards/{id}/members/{userId} { hiddenByTeacher }`) **and** board-wide exam
   mode (`PATCH /api/boards/{id} { examMode }`).

All five visibility rules live in one place: `Services/VisibilityService.cs`.

## Running (dev)

```bash
# terminal 1 — API on http://localhost:5080
cd BeeLearn
dotnet run

# terminal 2 — Vite dev server on http://localhost:5173 (proxies /api and /hubs)
cd BeeLearn/ClientApp
npm install
npm run build     # first time: also populates ../wwwroot for the fallback route
npm run dev
```

Open http://localhost:5173. Demo teacher: `teacher@demo.test` / `password`, board join
code `DEMO01` (an "A + B" problem is seeded).

## Running (single process)

```bash
cd BeeLearn
dotnet publish -c Release -o out     # runs `npm ci && npm run build` into wwwroot
./out/BeeLearn                       # serves SPA + API on one port
```

## Build prerequisites on the host

`gcc` and `g++` must be on `PATH`. `bubblewrap` (`bwrap`) is used automatically **if the
environment allows unprivileged user namespaces**; otherwise the judge falls back to
rlimits only and logs which mode it picked at startup.

## Security note

The judge is **classroom-grade**, not hardened multi-tenant isolation:

- Time and memory limits are always enforced (`setrlimit` in `Services/Judge/RunnerSource.cs`).
- Filesystem isolation requires `bwrap`; this dev container blocks user namespaces, so runs
  currently share the host filesystem view (read-only for most paths, per the app user's
  permissions) with no network namespace.
- To harden: run the API as a dedicated low-privilege user, and/or rebuild the dev container
  with `"runArgs"` that permit user namespaces so `bwrap` engages, or move code execution to
  a disposable VM/container. A `seccomp` syscall filter in the runner is a further step.

## Layout

```
BeeLearn/
  Controllers/        Auth, Boards, Members, Problems, Submissions, Run
  Services/
    VisibilityService.cs      who sees whose answers
    BoardService.cs           join codes + progress-grid builder
    Judge/                    queue, BackgroundService worker, compiler, sandbox, verdicts
  Hubs/BoardHub.cs            groups: board-{id}, board-{id}-staff, user-{id}
  Data/                       AppDbContext, migrations, seeder
  ClientApp/                  Vue SPA (build output -> ../wwwroot)
```
