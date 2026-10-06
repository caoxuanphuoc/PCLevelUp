# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project status

**PowerUp / Personal Growth Tracker** is a local-first desktop app for personal growth tracking. Sprint 1 (foundation) and Sprint 2 (domain) of TDD §10 are done: the solution, the DB schema, an app shell with placeholder pages, every domain calculator, and CI. Use cases and real screens start in Sprint 3. The design docs in `docs/` (written in Vietnamese) are the spec:

- `docs/BRD — Personal Growth Tracker v1.0.md`: business rules (BR-xx), functional requirements (FR-xx), and the Score formula.
- `docs/Technical Design Document — Personal Growth Tracker Desktop v1.0.md` (TDD): stack, architecture, schema, build. **Authoritative for implementation.**
- `docs/UIDocument.md`: design tokens (Area/status/score colors), components, and the 7 MVP screens. It was written web-first (responsive, mobile tab bar). For the desktop app, the TDD's Avalonia mapping (TDD §6) takes precedence.

When code and docs disagree, or a doc lists an item as an open question (BRD §9.3, TDD §10.1), ask the user. Don't make the decision yourself.

## Stack

.NET 10 (pinned in `global.json`) · Avalonia 12.1 (Fluent theme, Inter font) · CommunityToolkit.Mvvm 8.4 (partial-property `[ObservableProperty]`) · Microsoft.Extensions.Hosting · EF Core 10 + SQLite · Serilog · Velopack · xUnit **v3** + **AwesomeAssertions** (Apache-licensed fork of FluentAssertions; same API, `using AwesomeAssertions`) + Avalonia.Headless.XUnit · GitHub Actions (`.github/workflows/ci.yml`, windows-latest + ubuntu-latest). LiveCharts2 has not been added yet; it comes in Sprint 5.

Package versions live only in `Directory.Packages.props` (Central Package Management), so csproj files have `PackageReference` entries without versions. `Directory.Build.props` sets net10.0, nullable, and **TreatWarningsAsErrors**. `tests/Directory.Build.props` adds the xUnit, assertion and coverage packages to every test project, plus global usings for `Xunit` and `AwesomeAssertions`.

## Commands

```bash
dotnet build GrowthTracker.sln
dotnet test GrowthTracker.sln
dotnet test tests/GrowthTracker.Domain.Tests --filter "FullyQualifiedName~ScoreEngine"   # single test/class
dotnet test GrowthTracker.sln --collect:"XPlat Code Coverage" --results-directory TestResults
dotnet run --project src/GrowthTracker.Desktop

# dotnet-ef is a local tool (dotnet-tools.json); run `dotnet tool restore` once.
# A design-time factory in Infrastructure means no startup project is needed.
dotnet ef migrations add <Name> --project src/GrowthTracker.Infrastructure --output-dir Persistence/Migrations

# Release packaging (TDD §8.2)
dotnet publish src/GrowthTracker.Desktop -c Release -r win-x64 --self-contained -o publish/win-x64
vpk pack --packId GrowthTracker --packVersion <ver> --packDir publish/win-x64 --mainExe GrowthTracker.Desktop.exe
```

## Architecture

Clean Architecture. Dependencies point inward only: `Desktop → Application → Domain`, and `Infrastructure` implements interfaces that Application defines.

- **GrowthTracker.Domain** has no package references. It holds entities and the pure calculators: `ScoreEngine`, `XpCalculator`/`LevelTable`, `StreakCalculator`, `ProgressCalculator`, `RiskDetector`, `Recommender`, `ScheduleGenerator`. They are pure functions with no DB access and no `DateTime.Now`. Entity methods that need time take `today` (DateOnly) or `nowUtc` as parameters, and callers get them from `IClock` (Application). Entities enforce BR/FR rules by throwing `DomainException` with a code such as `plan.goal_required`. Thresholds that are still open questions live in `ScoreWeights`, `DifficultyTable`, `LevelTable.Default` and `DomainThresholds`.
- **GrowthTracker.Application** holds use cases. Each one is a Command/Query plus a Handler in the same feature folder. Handlers are registered directly in DI, with **no MediatR**. Reads return pre-built DTOs per screen (light CQRS). Domain events (`PlanLogged`, `PlanRescheduled`, `GoalStatusChanged`) are handled **in the same transaction** to update XP, Progress, Streak, and Risk.
- **GrowthTracker.Infrastructure** contains the EF Core `AppDbContext`, configurations, and migrations; OS notifiers (`WindowsToastNotifier`, `LinuxDbusNotifier` via Tmds.DBus, chosen by `OperatingSystem.IsWindows()/IsLinux()`); backup through the SQLite Online Backup API; the Velopack updater; and `BackgroundService` jobs. Jobs must **catch up on runs missed** while the app was closed.
- **GrowthTracker.Desktop** follows MVVM with one View (AXAML) and one ViewModel per screen. ViewModels call use cases only and never touch the DbContext. `ViewLocator` maps ViewModel to View by name (`ViewModels.Pages.XViewModel` → `Views.Pages.XView`). Pages derive from `PageViewModel`, are registered in Desktop's `DependencyInjection.AddDesktop`, and are shown through `INavigationService`. `Program.Main` runs in this order: `VelopackApp.Build().Run()` → Serilog → Generic Host → `DatabaseInitializer` (backup → migrate → WAL → seed 6 Areas) → set `App.Services` → start Avalonia. Inside the Desktop namespace, `Application` resolves to the `GrowthTracker.Application` namespace, so write `Avalonia.Application` in full. Colors are tokens in `Styles/Tokens.axaml` (Light/Dark ThemeDictionaries), and typography classes are in `Styles/AppStyles.axaml`.

Tests mirror these layers: `tests/GrowthTracker.{Domain,Application,Infrastructure,Desktop}.Tests`. Infrastructure tests run the real migrations against SQLite in-memory through `TestDatabase` and use a `FakeClock`. Desktop tests use `[AvaloniaFact]`: they build services with `AddDesktop()`, show `MainWindow`, and call `Dispatcher.UIThread.RunJobs()`.

### Core data model ideas

- **PlanOccurrence** (a scheduled session) is separate from **PlanLog** (the result of that session), with a 1:0..1 relationship. Calendar, rescheduling, Consistency, and Missed state all derive from occurrences.
- Plan ↔ Goal is many-to-many through `PlanGoalLink` (with Weight). Every Plan needs at least one link (BR-01).
- `XpLedgerEntry` is append-only. XP is never removed, not even when a Plan is archived (BR-07).
- Every table (except PlanGoalLink and AppSetting) has a GUID v7 Id stored as TEXT, plus `CreatedAt`, `UpdatedAt`, and `IsDeleted` (soft delete, for future sync).
- Goal/Milestone `Progress` and `Score` are **caches** that can be recomputed. PlanLog stores each score component and `ScoreVersion` so history can be recalculated when the formula changes.
- SQLite runs with `journal_mode=WAL` and `foreign_keys=ON`. Migrations run automatically at startup, **after** a backup is taken.
- `decimal` is stored as REAL (a convention in `AppDbContext`) so SUM and ORDER BY run in SQL. Every `Entity` gets a global `!IsDeleted` query filter, and `AuditInterceptor` stamps CreatedAt/UpdatedAt from `IClock`.

## Domain rules that are easy to get wrong

- **Score** = weighted sum of Completion, Quality (stars ÷ 5 × 100), Consistency (sessions done ÷ target over the last 7 days, max 100), and Difficulty. Weights are versioned: `V1` = 0.40/0.25/0.20/0.15 (original BRD), `V2` = 0.45/0.30/0.25/0. **The user chose V2 as the default** (`ScoreWeights.Default`). V1 is kept and tested. Round with `MidpointRounding.AwayFromZero`, and cap the Score at 100 (BR-05).
- **XP** = Score × difficulty multiplier (Hard ×1.15). XP may exceed 100 (BR-06).
- Completion per session is capped at 100%. Going over target is recorded but earns nothing extra (BR-09).
- A skipped session has no Score but still counts in the Consistency denominator (BR-10).
- A PlanLog can be edited only within 24h of creation, and editing recomputes its Score (BR-11).
- Rescheduling never changes Score. It only feeds Reschedule Rate (BR-12).
- Sessions can't be scheduled in the past, and results can't be logged for future dates (FR-24).
- Business "days" are `DateOnly` in the user profile's time zone. Instants are stored in UTC.
- Recommendations apply only after the user confirms, and only to future sessions (BR-14).
- Required test: with V1 weights, C=90, Q=80, S=100, D=90 must give Score **90** (89.5 rounded up) and **103.5 XP** for Hard. V2 also gives 90. Name tests after the BRD examples, for example `Score_BrdExample_V1_Returns90` or `Gym_48PercentIn4Weeks_SuggestsDecreaseTo3`.
- Assumptions for open BRD §9.3 items, chosen to be easy to change: difficulty points/XP multipliers Easy 60/×1.00, Normal 75/×1.05, Hard 90/×1.15, Extreme 100/×1.30; Consistency uses a rolling 7-day window; level threshold = 75·L·(L−1), which fits the BRD examples; a Plan linked to several Goals splits XP and progress by `PlanGoalLink.Weight`; Goal priority 1 is the highest.

## Conventions

- Logs record only IDs, never the contents of Note or Goal names.
- Domain coverage target is ≥ 90%, and Application ≥ 80%.
- Data paths: Windows `%LOCALAPPDATA%\GrowthTracker\growth.db`; Linux `$XDG_DATA_HOME/growth-tracker/growth.db` (see TDD §7.3 for backup, log, and config paths).
- The UI font is Inter, embedded in Assets. Colors are tokens in Light and Dark ResourceDictionaries, and the values come from UIDocument §2.
