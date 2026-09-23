# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

YogaClassManager manages yoga class rosters, students, terms, passes, and people. The repo is
**mid-migration across three parallel stacks**, not one app — see "Solution structure" below before
touching anything, since which conventions apply depends entirely on which project a file is in.

## Solution structure

Six projects in `YogaClassManager.sln`:

| Project | What it is |
|---|---|
| `YogaClassManager/` | The original .NET MAUI desktop/mobile app. Itself contains **two** parallel data-access stacks (old `Database/`+`Models/`, newer `NewDatabase/`+`NewModels/`) — see the MAUI section below. |
| `YogaClassManager.Core/` | Framework-agnostic domain models + data-access abstractions (`IDbModel<TModel,TFilter>`, `Filters/`, `Repositories/`). No UI, no concrete database — see "Core architecture" below. |
| `YogaClassManager.Core.Dummy/` | The **only** current implementation of `Core`'s repository interfaces: in-memory (`InMemoryDataStore` + `InMemoryXxxRepository` classes + `DummyDataSeeder`). There is no real-database (e.g. SQLite) implementation of `Core` yet. |
| `YogaClassManager.Core.Tests/` | xUnit tests for `Core`/`Core.Dummy` (repository behavior, business rules like merge/archive). Plain `dotnet test` works. |
| `YogaClassManager.Avalonia/` | A newer, cross-platform Avalonia desktop UI head built on `Core` + `Core.Dummy`, **independent of the MAUI project** (does not use `NewDatabase`/`NewModels` at all). This is where active UI development is happening — see "Avalonia app" below. Has its own design-system doc, `YogaClassManager.Avalonia/UI_STYLE_GUIDE.md`. |
| `YogaClassManager.Avalonia.Tests/` | xUnit tests for `Avalonia`'s ViewModels/behaviors. Plain `dotnet test` works. |

**Practical implication**: `YogaClassManager.Core`/`Core.Dummy`/`Avalonia` is the actively-developed
stack going forward; the MAUI project's two stacks are effectively legacy (still present, still
buildable, but new feature work should default to `Core`+`Avalonia` unless you're explicitly asked
to modify the MAUI app in place). Don't assume a pattern from one stack applies to another — e.g.
`Core`'s `IDbModel<TModel,TFilter>` looks similar to the MAUI project's `NewDatabase`'s
`IDbModel<TModel,TFilter>` but they're unrelated, separately-defined interfaces in different
namespaces with slightly different shapes (`Core`'s is fully async including `Delete`; the MAUI
one's `Save`/`Refresh` are sync and its `PersonDbModel` implementation is an unfinished stub).

## Build & run

**MAUI (`YogaClassManager/`)**: open `YogaClassManager.sln` (or `YogaClassManager/YogaClassManager.App.sln`)
in an IDE with the MAUI workload installed, or from the CLI:
```
dotnet build YogaClassManager.sln
dotnet build YogaClassManager/YogaClassManager.csproj -f net8.0-windows10.0.19041.0   # Windows
dotnet build YogaClassManager/YogaClassManager.csproj -f net8.0-maccatalyst           # Mac
```
There is no `dotnet run` target for the MAUI head in the usual sense — deploy/run via the IDE
(Visual Studio / Rider run configuration) or `dotnet build -t:Run -f <tfm>`. The MAUI project has no
test project — don't assume `dotnet test` works for it specifically.

**Core / Core.Dummy / Core.Tests / Avalonia / Avalonia.Tests**: ordinary .NET projects, no special
tooling needed:
```
dotnet build YogaClassManager.Core.Tests/YogaClassManager.Core.Tests.csproj   # also builds Core, Core.Dummy
dotnet test  YogaClassManager.Core.Tests/YogaClassManager.Core.Tests.csproj
dotnet build YogaClassManager.Avalonia/YogaClassManager.Avalonia.csproj
dotnet test  YogaClassManager.Avalonia.Tests/YogaClassManager.Avalonia.Tests.csproj
dotnet run   --project YogaClassManager.Avalonia/YogaClassManager.Avalonia.csproj   # launches the desktop app
```

## MAUI app (`YogaClassManager/`): two parallel database/model stacks (mid-migration)

Both stacks coexist within the MAUI project — know which one a file belongs to before editing it.

**Old stack** (`Database/`, `Models/`):
- `Database/DatabaseManager.cs` wraps a single `SQLiteAsyncConnection` (sqlite-net-pcl) and owns one `*Service` per entity (`PeopleService`, `StudentsService`, `ClassesService`, `PassesService`, `TermService`), each subclassing `Database/DatabaseService.cs`.
- Services hand-write SQL strings (including string-interpolated `WHERE` clauses — not parameterized) against tables listed in `Models/Tables.cs`, and map rows to hand-written `Models/**` classes (`ObservableObject` + `IIdentifiable`/`IUpdateable<T>`) via `*Mapping` types.
- `DatabaseManager` also implements manual nested transactions (`BeginTransactionAsync`/`CommitAsync`/`AbortAsync` with a `transactionDepth` counter).
- ViewModels for this stack extend `ViewModels/Base/CollectionPageModel.cs` → `SearchableCollectionPageModel.cs` / `LazySearchableCollectionPageModel.cs`, which handle busy-state, search-query debouncing/racing, and paged retrieval via abstract `Retrieve*`/`Get*UpdatedItems` methods services must implement.

**New stack** (`NewDatabase/`, `NewModels/`):
- `NewDatabase/DatabaseService.cs` wraps `Microsoft.Data.Sqlite` directly (via Dapper for query mapping) with reference-counted open/close (`ExecuteDbAction`/`ExecuteDbFunction`, plus transactional variants) instead of a long-lived connection.
- Data access follows an `IDbModel<TModel, TFilter>` interface (`NewDatabase/IDbModel.cs`): one implementation per entity (e.g. `NewDatabase/People/PersonDbModel.cs`) built from a `struct TFilter` (e.g. `PersonFilter`) that composes a SQL `WHERE`/`ORDER BY` string from optional filter fields. `SortBy` uses a `KeyValuePair<TSortOptionsEnum, Order>` where enum values carry a `[StringValue]` attribute (`NewModels/People/Order.cs`) resolved via `StringValueAttribute.GetStringValue(...)` — add new sortable columns by adding an enum member with `[StringValue("ColumnName")]`, not by string-matching property names.
- New models live in `NewModels/**`, extend `ObservableValidatableObject` (`Models/ObservableValidatableObject.cs`, wraps `CommunityToolkit.Mvvm`'s `ObservableValidator`) and use `System.ComponentModel.DataAnnotations` attributes (`[Required]`, `[RegularExpression]`) on `[ObservableProperty]` fields for validation, rather than the old stack's hand-rolled `Validate()` methods.
- UI binds to `NewModels/GrowableDbCollection.cs`, an `ObservableCollection<TModel>` that lazily pages in more rows from its backing `IDbModel` via `GrowCollection(amount)`. It implements `IGrowableCollection` so the `Components/GrowableScrollView` control (a `CollectionView` wrapper) can auto-grow when scrolling nears the end (`RemainingItemsThreshold`/`GrowAmount` bindable properties).

Both `DatabaseManager` and the new `NewDatabase.DatabaseService` are registered as separate DI singletons in `MauiProgram.cs` (note the `DatabaseService` alias resolves to `YogaClassManager.NewDatabase.DatabaseService`, not the old one) — a page/viewmodel should only depend on the one matching the stack it uses.

## MAUI app structure

- **Navigation**: Shell-based (`AppShell.xaml` defines `FlyoutItem`/`ShellContent` routes). Programmatic navigation goes through `Services/NavigationService.cs`, which wraps `Shell.Current.GoToAsync` and passes parameters boxed in `Models/Message.cs` (a workaround for Shell query-parameter type constraints) rather than as raw query strings.
- **DI**: All pages and page-models are registered `AddTransient` in `MauiProgram.cs`; add new pages/viewmodels there in matching pairs.
- **MVVM**: ViewModels use `CommunityToolkit.Mvvm` source generators (`[ObservableProperty]`, `[RelayCommand]`) plus a hand-rolled `BasePageModel.IsBusy`/`StartBusy()`/`EndBusy()` reentrant-busy-counter pattern — always pair `StartBusy()`/`EndBusy()` in a `try`/`finally`.
- **Custom layout controls** live in `Components/`: `OptionsLayout` (a `VerticalStackLayout` with a custom `ILayoutManager` that shows/hides children based on an attached `VisibleOn` property matching the layout's `Option`, for building option-dependent forms) and `GrowableScrollView` (see above).
- **Converters** (`Converters/`) are registered as needed per-XAML and used for things like `DateOnly`/`TimeOnly` ↔ `DateTime`/`TimeSpan` bridging (MAUI pickers don't support `DateOnly`/`TimeOnly` natively) — note `MauiProgram.cs` sets `SetShouldSuppressExceptionsInConverters(true)`, so converter exceptions fail silently rather than crashing; a recent fix (see git log) had to account for a CommunityToolkit.Maui update changing converter exception behavior.

## Core architecture (`YogaClassManager.Core`)

- **Models** (`Models/People/`, `Models/Passes/`, `Models/Classes/`) — plain `ObservableObject` (CommunityToolkit.Mvvm) classes implementing `IIdentifiable`/`IUpdateable<T>`, e.g. `Person`, `Student` (extends `Person`), `EmergencyContact`, `Pass`/`CasualPass`/`DatedPass`/`TermPass`, `Term`, `ClassSchedule`, `ClassRoll`.
- **Data access**: `Data/IDbModel.cs` defines `IDbModel<TModel, TFilter>` — `Task<TModel?> LoadSingle(...)`, `Task<IReadOnlyList<TModel>> LoadMultiple(count, skip, ...)`, `Task<bool> Refresh(...)`, `Task Save(model, SaveOptions, ...)`, `Task Delete(...)`, plus a `TFilter? Filter { get; init; }`. `Data/GrowableCollection.cs` (`GrowableCollection<TModel,TFilter> : ObservableCollection<TModel>, IGrowableCollection`) pages a query in via repeated `GrowCollection(amount)` calls against an `IDbModel`.
- **Filters** (`Filters/*.cs`) — one `struct` per entity (`PersonFilter`, `StudentFilter`, `PassFilter`, `EmergencyContactFilter`, `ClassScheduleFilter`, `ClassRollFilter`, `TermFilter`), each with optional filter fields plus a `SortBy` of `KeyValuePair<TSortOptionsEnum, Order>?` (sort enum members carry `[StringValue("ColumnName")]`, same pattern as the MAUI new stack's `Order.cs`/`StringValueAttribute`). `PassFilter`/`EmergencyContactFilter` key off a parent `StudentId` — the pattern for a child/relationship query.
- **Repositories** (`Repositories/*.cs`, interfaces only) — one per entity: `IPersonRepository`, `IStudentRepository`, `IPassRepository`, `IEmergencyContactRepository`, `IClassScheduleRepository`, `IClassRollRepository`, `ITermRepository`. Each exposes `IDbModel<TModel, TFilter> Query(TFilter filter)` for reads plus imperative async CRUD/relationship methods (e.g. `IStudentRepository.LinkEmergencyContactAsync`/`GetPassesAsync`). `Student.Passes`/`Student.EmergencyContacts` on the model itself are live `IDbModel<...>` values (attached by the repository layer when it hands out a `Student`, via `Student.AttachChildRepositories(...)`), not eagerly-populated collections — don't reintroduce eager loading there.
- No real database implementation exists yet (`IDbModel.cs`'s own doc comment says implementations may be in-memory today "or, in the future, a real database" — that future implementation doesn't exist). Everything currently runs against `YogaClassManager.Core.Dummy`'s `InMemoryDataStore`.

## Avalonia app (`YogaClassManager.Avalonia`)

- **DI / composition root**: `App.axaml.cs`'s `ConfigureServices()` uses Splat (`Locator.CurrentMutable`), not a `Microsoft.Extensions.DependencyInjection` container. Repositories and `InMemoryDataStore` are `RegisterLazySingleton`; page ViewModels are plain `Register` (a **fresh instance is constructed on every navigation** — don't assume a page ViewModel's state survives navigating away and back); `IViewFor<TViewModel>` registrations are what `RoutedViewHost` uses to resolve a View for a routed ViewModel (naming convention `XyzViewModel` → `XyzView`, also used by `Services/DialogService.cs` for dialog windows).
- **Navigation**: ReactiveUI's `RoutingState`/`IScreen`/`RoutedViewHost` (not Avalonia's own routing). `AppScreen` is the single app-wide `IScreen`. `MainWindowViewModel` drives the nav rail and holds the `Router`; see `UI_STYLE_GUIDE.md`'s "Navigation shell" section for the collapsible `SplitView` nav-rail structure and how to add a new top-level page.
- **List pages**: `ViewModels/Base/CollectionPageModelBase.cs` (plain list + `RefreshCommand`) and `ViewModels/Base/SearchableCollectionPageModelBase.cs` (adds a 300ms-debounced `SearchQuery`, `.Skip(1)`'d past `WhenAnyValue`'s immediate initial emission — **don't remove that `Skip(1)`**, without it every page extending this base redundantly reloads ~300ms after every navigation and visibly glitches the list's selection). `PeopleViewModel`/`StudentsViewModel`/`TermsViewModel` extend the searchable base; `ClassSchedulesViewModel`/`ClassRollsViewModel` extend the plain one.
- **Dialogs**: in-app overlays (not separate OS windows), each a plain `UserControl` shown via `Services/IDialogService.cs`/`DialogService.cs` inside a shared `Controls/DialogHost`; see `UI_STYLE_GUIDE.md`'s "Dialogs" section for the standard dialog XAML shape (`FormField` per input, `DialogButtonRow` to close) and for the keyboard contract `DialogHost` provides to every dialog (Escape/Enter, initial focus, tab containment) — don't re-implement any of it per dialog. `Views/Shared/MarkRollWindow.axaml` is the one deliberate exception (a real `Window`, via `Services/IRollWindowService.cs`, with its own private `IDialogService` instance/overlay) — don't use it as precedent for turning another dialog into a `Window` without the same "runs long, user needs to do other things meanwhile" justification.
- **Sort/filter controls**: whenever a repository-backed list has sortable and/or filterable fields, expose them via `Controls/SortByButton.axaml`/`Controls/FilterButton.axaml` (used directly for a list with no search box, e.g. the Student page's Passes list) or `Controls/FilterBar.axaml` (composes both plus a search box, for a full page list) — **not** inline `ComboBox`/`CheckBox` rows in the view. Omit `SortByButton` if the filter's sort enum has no user-facing field beyond its internal fallback key (e.g. today's `PassSortOptions`, `Id`-only). See `UI_STYLE_GUIDE.md`'s "Sort and filter controls" section for the full pattern (including the `FilterCount`/`ClearFiltersCommand` contract each host must implement).
- **Folder layout**: `Views/` (routed pages, one folder per page, plus `Views/Shared/` for dialogs) + `ViewModels/` (mirrors `Views/`, plus `ViewModels/Base/`) + `Controls/` (reusable controls: `MasterDetailView`, `LinkedRecordsPanel`, `RecordDetailsPanel`, `DetailHeader`, `RecordListItem`, `FormField`, `DialogHost`, `DialogButtonRow`, `GrowableItemsControl`, `OptionSwitchPanel`, `FillWrapPanel`, `Tag`, `SortByButton`, `FilterButton`, `FilterBar`) + `Behaviors/` (attached-property behaviors, e.g. `ScrollEdgeFade`) + `Styles/` (`Tokens.axaml`, `Controls.axaml` — the design-token/global-style dictionaries) + `Services/` + `Converters/`.
- **Cross-page navigation** goes through `Services/IPageNavigator` (`ToStudent`/`ToIdentity`/`ToClassRoll` plus `TakePending*` for the "select this row on arrival" hand-off), *not* `Locator.Current.GetService<TOtherPageViewModel>()` in a command body — page ViewModels don't reference each other's types, and can be constructed in tests against a plain `IScreen` with no Splat container.
- **List page ViewModels** get `HasSelection`, `SortOrderOptions`, `RefreshWhenChanged(...)` and `ReloadOn(...)` from `CollectionPageModelBase`. `ReloadOn` uses `Switch` **and** a `CancellationToken` — the loads mutate their collections in place, so discarding a superseded load's result isn't enough on its own; see `UI_STYLE_GUIDE.md`'s "List page ViewModels" section before writing a new one.
- **Interactive states (hover/pressed/selected/disabled) are owned centrally**, not per control: the palette is in `Styles/Tokens.axaml` and every rule is on a base selector in `Styles/Controls.axaml`, so a **new button needs no styling work at all**. The invariant — *a `BrushTransition` may only be attached where the app owns an `Opacity=1` brush for every state at both ends* — exists because Avalonia's `SolidColorBrushAnimator` interpolates `Color` and `Opacity` as independent multiplying factors, so a brush with `Opacity < 1` at one end overshoots mid-animation and reads as a grey flash. `InteractiveStateTests` enforces it; see `UI_STYLE_GUIDE.md`'s "interactive-state invariant".
- **Visual/design-system conventions** (spacing/color/typography tokens, the collapsible nav rail, shared control and dialog styling, the scroll-edge-fade behavior, accessibility and animation rules) are documented separately in `YogaClassManager.Avalonia/UI_STYLE_GUIDE.md`. **Read that file before touching any Avalonia XAML, and update it in the same change whenever you add or change a token, shared style, top-level page, or UI convention it documents** — it is the source of truth for how the Avalonia UI should look and is expected to be kept current, not left to drift from the code.
