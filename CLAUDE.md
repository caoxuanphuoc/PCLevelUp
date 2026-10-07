# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project status

**PowerUp / Personal Growth Tracker** is a local-first desktop app for personal growth tracking. Sprints 1–4 of TDD §10 are done: foundation, domain calculators, Goals/Goal Detail/Settings (Areas) with the Goal → Milestone → Plan flow that schedules sessions, and daily logging (Today + Plan Result dialog: log, edit within 24h, skip, Score/XP/Level). Background jobs (rollover, daily risk scan, reminders) also run, with a tray icon, close-to-tray, start with the OS, and a single instance. Sprint 5a (Calendar: month/week/list, drag-and-drop rescheduling, schedule metrics) is done too. Dashboard (5b) and Weekly Review (6) are still placeholders. The daily backup job and the weekly review job are not built yet. The design docs in `docs/` (written in Vietnamese) are the spec:

- `docs/BRD — Personal Growth Tracker v1.0.md`: business rules (BR-xx), functional requirements (FR-xx), and the Score formula.
- `docs/Technical Design Document — Personal Growth Tracker Desktop v1.0.md` (TDD): stack, architecture, schema, build. **Authoritative for implementation.**
- `docs/UIDocument.md`: design tokens (Area/status/score colors), components, and the 7 MVP screens. It was written web-first (responsive, mobile tab bar). For the desktop app, the TDD's Avalonia mapping (TDD §6) takes precedence.
- `docs/stitch_personal_growth_tracker_ui/`: the visual reference. It has a `screen.png` and `code.html` per screen and the "Nexus Growth" design system (`nexus_growth/DESIGN.md`). Match its layout and look, but keep the TDD's 240px sidebar, and leave out elements that belong to later sprints (charts, AI Coach, streak/search header).

When code and docs disagree, or a doc lists an item as an open question (BRD §9.3, TDD §10.1), ask the user. Don't make the decision yourself.

## Stack

.NET 10 (pinned in `global.json`) · Avalonia 12.1 (Fluent theme, Inter font) · CommunityToolkit.Mvvm 8.4 (partial-property `[ObservableProperty]`) · Material.Icons.Avalonia 3 · Microsoft.Extensions.Hosting · EF Core 10 + SQLite · Serilog · Velopack · xUnit **v3** + **AwesomeAssertions** (Apache-licensed fork of FluentAssertions; same API, `using AwesomeAssertions`) + Avalonia.Headless.XUnit · GitHub Actions (`.github/workflows/ci.yml`, windows-latest + ubuntu-latest). LiveCharts2 has not been added yet; it comes in Sprint 5.

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
- **GrowthTracker.Application** holds use cases. Each one is a `record XCommand : ICommand<T>` or `XQuery : IQuery<T>` plus an `internal` handler, kept in the same feature file (`Areas/`, `Goals/`, `Plans/`). Each handler is registered with one line in `DependencyInjection.AddApplication` (`AddCommand<,,>` or `AddQuery<,,>`), with **no MediatR**. Callers use `IUseCaseDispatcher`: `SendAsync` returns `Result<T>`, and `QueryAsync` returns a DTO. Each call runs in its own DI scope, so it gets a fresh DbContext. The dispatcher turns a thrown `DomainException` into a failed `Result` that carries its code, and handlers return `Errors.NotFound("goal")` and similar for missing rows. Reads return pre-built DTOs per screen (light CQRS). `OccurrencePlanner.TopUp` turns a Plan into `PlanOccurrence` rows (CreatePlan uses it, and the weekly rollover job will too). `PlanArchiver` archives plans and soft-deletes their upcoming sessions.
  - **Logging** (`Logging/`) covers the effects of a session result. `LogPlanResult`, `EditPlanLog` and `SkipOccurrence` call `LogEffects` explicitly, not through an event bus, inside one transaction (`IAppDbContext.BeginTransactionAsync`; save the log, then recompute, then commit). `LogEffects` does two things. First, it appends XP to the ledger per Area, split by `PlanGoalLink.Weight`; an edit appends only the difference. Second, it recomputes progress Plan → Milestone → Goal: a Goal is the weighted average of its Milestones, plus each plan outside a Milestone counted as weight 1. It also recomputes the Goal Score (the average of its logs) and the status, which is judged only after 7 days of sessions (BR-13). `ScoreInputs.BuildAsync` computes Consistency over the rolling 7-day window. Streak and levels are computed on read (`XpTotals`, `StreakCalculator`) and are not stored.
  - **Calendar** (`Calendar/`): `GetCalendar(From, To, AreaId)` serves the month, week and list views (the view only picks the range). It returns the sessions (Area = the Area of the plan's heaviest Goal), Goal and Milestone deadlines, peak days (planned minutes > 1.5× the average day; non-Time plans count 30 min) and stats. The stats are: Reschedule Rate (moved ÷ scheduled); adherence (Done, never moved, and logged on that local day ÷ due); and best weekday, which needs logs in 4 different weeks. `RescheduleOccurrence` has two scopes: `ThisOnly`, or `ThisAndFollowing`, which shifts every later Scheduled session of the plan by the same number of days. `AddSession` adds a one-off session.
  - **Jobs** (`Jobs/`): `JobScheduler.TickAsync` decides what is due, and the host ticks it every 30 seconds. On the first tick (start-up catch-up) and daily from 00:05 it runs `RunRollover`: unlogged past sessions become Missed, plans are topped up to 8 weeks, and a moved session still owns its `OriginalDate`. Daily from 06:00 it runs `RunRiskScan`, which recomputes every open Goal. On every tick it sends reminders: `GetDueReminders` over the window since the previous tick, preferred time minus 10 minutes, never replayed at start-up. Last run dates and user toggles live in `AppSetting` (`Settings/`: `GetAppSettings`, `UpdateBackgroundSettings`). `JobScheduler.DataChanged` makes Desktop call `INavigationService.RefreshAsync`.
- **GrowthTracker.Infrastructure** contains the EF Core `AppDbContext` (SQLite with split queries), configurations, and migrations; backup through the SQLite Online Backup API; `JobRunnerService` (a `BackgroundService` that ticks `JobScheduler`); `LinuxDbusNotifier` (Tmds.DBus.Protocol, `org.freedesktop.Notifications`), with `LogOnlyNotifier` elsewhere; and `StartupRegistration` (the HKCU Run key on Windows, `~/.config/autostart/growth-tracker.desktop` on Linux, launched with `--minimized`). The Velopack updater is not built yet. **On Windows the notifier lives in Desktop**, not in Infrastructure (the user chose this over the TDD's WinRT toast). `Services/Tray/WindowsTrayIcon` is one Win32 `Shell_NotifyIcon`: it is the tray icon, its context menu, and the notifications (balloons show as toasts on Windows 10/11, with no AUMID or shortcut needed). Desktop's `TrayNotifier` replaces `INotifier` there. Linux uses Avalonia's `TrayIcon` (`AvaloniaTrayService`). Tests register `NoTrayService` (`AddDesktop(withTray: false)`, the default).
- **GrowthTracker.Desktop** follows MVVM with one View (AXAML) and one ViewModel per screen. ViewModels call use cases only and never touch the DbContext. `ViewLocator` maps ViewModel to View by name (`ViewModels.Pages.XViewModel` → `Views.Pages.XView`). Pages derive from `PageViewModel`, are registered in Desktop's `DependencyInjection.AddDesktop`, and are shown through `INavigationService`. `Program.Main` runs in this order: `VelopackApp.Build().Run()` → Serilog → `SingleInstance` → Generic Host → `DatabaseInitializer` (backup → migrate → WAL → seed 6 Areas) → load `AppPreferences` → `host.Start()` (starts the job runner) → set `App.Services` → start Avalonia, where `AppShell.Start` takes over. Inside the Desktop namespace, `Application` resolves to the `GrowthTracker.Application` namespace, so write `Avalonia.Application` in full. Colors are tokens in `Styles/Tokens.axaml` (Light/Dark ThemeDictionaries), and typography and button classes (`primary`, `ghost`, `danger`, `link`, `card-button`, `filter`, `tab`) are in `Styles/AppStyles.axaml`.
  - **Navigation:** `INavigationService.NavigateAsync<TPage>(parameter)` calls `PageViewModel.OnNavigatedToAsync(parameter)` on every visit, including Back, so pages reload fresh data. Use `LoadSafelyAsync` for loads. Pages outside the sidebar (Goal Detail) clear the sidebar selection.
  - **Dialogs:** `IDialogService.ShowAsync(DialogViewModel<T>)` shows an overlay in MainWindow and returns a `Task<T?>` (null means cancelled; Escape cancels). Dialogs submit through `SubmitAsync`, which keeps the dialog open and shows `ErrorMessages.For(error)` (Vietnamese text per error code) when the use case fails. `ConfirmAsync` is for destructive actions. Dialog Views live in `Views/Dialogs/` and are found by the ViewLocator.
  - **Presentation:** `Presentation/Display` is the single place for the label, color and icon of each status, score tier and plan type, and for date formatting (`dd/MM/yyyy`). Use `Controls/Chip` (a tinted pill with icon or dot) and `Controls/ProgressTrack` (a bar with an expected-progress marker) instead of building these by hand, and likewise `Controls/ScoreRing` and `Controls/StarRating` (supports arrow keys and 1–5). Icons come from `Material.Icons.Avalonia` (`<mi:MaterialIcon Kind="..."/>`). List items take callbacks (`Func<Item, Task>`) and expose their own commands, so templates don't need `$parent` bindings.
  - **Time:** ViewModels must also take "today" from `IClock`, never `DateTime.Today`.
  - **Calendar view:** drag and drop, tap-to-select and the "T" shortcut live in `CalendarView.axaml.cs`, because pointer plumbing cannot be expressed in the ViewModel. A drag starts after 6 px of movement on a chip whose `CanReschedule` is true, and carries the occurrence id as text in a `DataTransfer`. A drop onto a day cell or week column calls `CalendarViewModel.MoveSessionAsync`, which opens `RescheduleDialogViewModel`. Avalonia 12 notes: tap events are `InputElement.TappedEvent` (`Gestures` is internal), and DnD uses `DataTransfer`/`DataTransferItem.CreateText` and `e.DataTransfer.TryGetText()`.
  - **Window lifecycle:** `AppShell` owns `MainWindow` with `ShutdownMode.OnExplicitShutdown`. When "close to tray" is on (the default, read from `AppPreferences`, which the Settings page updates), X only hides the window, and "Thoát" in the tray menu quits. `Program` takes a per-user named mutex (`SingleInstance`): a second launch signals the first over a named pipe to show its window, then exits. `--minimized` starts the app hidden in the tray.

Tests mirror these layers: `tests/GrowthTracker.{Domain,Application,Infrastructure,Desktop}.Tests`. Application tests (`TestApp`) and Desktop tests (`TestHost`) wire the real Application + Infrastructure (+ Desktop) stack on migrated SQLite in-memory, with a `FakeClock` fixed to Monday 2026-10-05, plus a `FakeStartupRegistration` (and a `FakeNotifier` in TestApp) so tests never touch the registry, the autostart folder or real notifications. Desktop tests use `[AvaloniaFact]` and drive flows through ViewModel commands. Use `TestHost.WaitForAsync`/`WaitForDialogAsync` to wait for async loads and dialogs. Headless tests use headless drawing; rendering with Skia in headless mode throws on Fluent's composite font for SemiBold text, which the real app does not do.

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
- A logged session becomes `Done` only if something was actually done. A Binary "Không", Actual 0, or no criteria ticked makes it `Missed`, so it does not count toward Streak or Consistency. "Already logged" is checked by whether a PlanLog exists, not by the session state. Each log stores `ActualValue` as follows: minutes/units for Time/Quantity, 1/0 for Binary/Frequency, and the number of criteria done for Achievement.
- The one-click tick on Binary sessions logs Quality 4 (`TodayViewModel.QuickTickQuality`); the user can edit it within 24h.
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
