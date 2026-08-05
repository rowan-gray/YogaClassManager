# Avalonia UI review — 2026-07-26

A point-in-time review of `YogaClassManager.Avalonia` covering design consistency, architectural
layout, and usability. Scope: ~2,070 lines of XAML across 37 files, ~3,020 lines of ViewModel across
26 classes, plus the `Core`/`Core.Dummy` surfaces the UI consumes.

**This is a snapshot, not a living convention doc.** `UI_STYLE_GUIDE.md` remains the source of truth
for how the UI should be built; where this review and the style guide disagree, the style guide wins
(and this file records the case for changing it). Findings are not re-verified after the fact — treat
`file:line` references as accurate as of the date above.

---

## Executive summary

The app is **structurally sound and unusually well documented for its size**. The design system is
real, the shared controls are genuinely reusable, and several decisions (the `Skip(1)` debounce, the
`/template/ ContentPresenter` transition targeting, `FillWrapPanel`'s bounded/unbounded height
handling) show care and are recorded with their rationale. This is not a codebase in trouble.

The problems are **drift and omission**, concentrated in three areas:

| Area | Assessment |
|---|---|
| **Design consistency** | Good system, unevenly applied. The token set has real gaps (no muted-text, no weight, no small-caption ramp entry) that force ~190 raw literals and three different opacities for one semantic idea. Four structures are duplicated 5–14× each. |
| **Architectural layout** | Layering is clean — no ViewModel touches `Avalonia.Controls` except through one leaked interface method. The weaknesses are async-safety patterns that are latent today because every repository is synchronous, and a documented convention with no compiler-checkable surface. |
| **Usability** | The weakest area. No empty states, loading state computed on five pages and shown on one, nine destructive actions with no confirmation, all list-load errors silent, and **zero keyboard affordances app-wide**. |

Three findings are functional defects rather than polish, including one that makes a core entity
impossible to create in the running app.

---

## What works well

Worth stating explicitly, because the findings below are all negative by construction and the
balance matters:

- **`UI_STYLE_GUIDE.md` itself.** Conventions are documented *with their rationale*, including
  several "we tried X and reverted it because Y" notes (collapsed-mode icon centering, the pane
  header `Grid`-vs-`StackPanel` decision). That is rare and directly prevents re-litigating settled
  decisions.
- **The shared-control layer.** `MasterDetailView`, `LinkedRecordsPanel`, `FilterBar`/`FilterButton`/
  `SortByButton`, `FillWrapPanel`, and `Tag` are properly generic — no page-specific knowledge, no
  leaked bindings. `LinkedRecordsPanel` having native sort/filter support rather than making callers
  assemble it via `HeaderContent` is the right call.
- **`ScrollEdgeFade`.** Opt-in via one attached property, works on both `ScrollViewer` and `ListBox`,
  animates the fade band's *presence* rather than hard-cutting, and the pure math is extracted to
  `ScrollEdgeFadeMath` specifically so it can be unit-tested without a live control. Exemplary.
- **`DialogService`.** A stack rather than a single slot (so dialogs nest), a clean
  `TaskCompletionSource` bridge, correct `finally` cleanup, and **no Avalonia types** — genuinely
  view-agnostic.
- **The toast layer.** Built on `WindowNotificationManager`'s `AdornerLayer` rather than a hand-rolled
  stack, which gets correct z-ordering above the dialog overlay for free, and severity is mapped to
  the existing `Status*Brush` tokens rather than FluentTheme's stock colors.
- **`StudentsViewModel.AddStudentAsync`** (`:300-356`) — the three-way promote-existing-identity /
  confirm-name-match / create-new branching is thoughtful domain UX, not boilerplate.
- **`MarkRollViewModel`'s pass resolution** (`:181-208`) — 0 candidates warns then offers to add a
  pass, 1 auto-selects, >1 opens a ranked picker. Exactly the right shape.
- **Accessibility naming is largely done.** Nearly every icon-only control already carries both
  `AutomationProperties.Name` and `ToolTip.Tip`. The gap is keyboard operation, not naming.

---

## Findings — Design consistency

### D1. The token set has gaps that force literals · Medium

`Styles/Tokens.axaml` covers spacing, radii, status color, and a four-entry type ramp. Missing:
a muted/secondary-text token, a font-weight token, and a ramp entry for the 11px text used 5× and the
32px Dashboard stat value used 3×.

Consequences, measured:
- **Three different opacities** — 0.5, 0.6, 0.7 — across 16 sites for what is semantically one thing
  ("secondary metadata beside a record name"): `StudentsView.axaml:112,152,158,172`,
  `TermsView.axaml:31,61,73,74`, `ClassRollsView.axaml:90,92`, `PassDetailView.axaml:29,44`,
  `MarkRollWindow.axaml:66,68`, `SettingsView.axaml:16`.
- **`FontSize="11"` appears 5×** (`StudentsView.axaml:58`, `IdentitiesView.axaml:41`,
  `ClassSchedulesView.axaml:29`, `TermsView.axaml:31`, `Tag.axaml:9`) — an undocumented second small
  size alongside `Caption`'s 12.
- `FontWeight` is hand-written 20+ times with no token.

**Recommendation**: add the missing ramp entries and a muted-text class; collapse the three opacities
to one.

### D2. The style guide overstates the spacing-token limitation · Low

`UI_STYLE_GUIDE.md:27-33` says spacing tokens can't be resource-bound because "Avalonia doesn't
reliably convert an `x:Double` resource into a `Thickness`-typed property", and concludes most
in-view spacing should stay literal.

That is **half right**. `StackPanel.Spacing` and `Grid.RowSpacing`/`ColumnSpacing` are `double`
properties, so `Spacing="{StaticResource SpacingMd}"` binds fine today. Only `Margin`/`Padding`
genuinely need `Thickness`-typed resources — and the guide already says how to fix that ("declare it
as `<Thickness x:Key="...">`"), it just never followed through.

Current state: ~190 raw spacing literals against exactly 8 `PagePadding` usages. `RadiusLarge` and
`StatusSuccessBrush` are declared and never used.

**Recommendation**: add `Thickness` tokens for the common margins, bind `Spacing` to the existing
doubles, and correct the guide's claim.

### D3. Values off the documented scale · Low

The guide sets a 4/8/16/24/32 scale. Off-scale: `Spacing="6"` (`PassEditView.axaml:63`,
`StudentsView.axaml:110`), `Spacing="2"` (`StudentsView.axaml:148`, `ClassSchedulesView.axaml:97`),
`Padding="6,2"` (`Tag.axaml:8`), `Spacing="12"` (5 sites), `Margin="0,4"` (`IdentitiesView.axaml:105`).

Most notable as a drift signal: `IdentityPickerView.axaml:28` and `PassPickerView.axaml:31` use
`Margin="0,12,0,0"` on the dialog button row where **all 11 other dialogs use `0,16,0,0`** — a
copy-paste divergence, which is exactly what D4 predicts.

### D4. Four structures duplicated 5–14× · High

| Structure | Copies | Representative sites |
|---|---|---|
| Dialog Cancel/Save button row | **14** | `ConfirmView.axaml:13-16`, `StudentEditView.axaml:34-37`, `TermEditView.axaml:45-48` |
| Detail-pane heading + action row (~20 lines each) | **5** | `StudentsView.axaml:72-93`, `ClassRollsView.axaml:65-82`, `TermsView.axaml:41-58` |
| Form field (label above input) | **~25** | `IdentityEditView.axaml:13-23`, `PassEditView.axaml:19-53` |
| Master-list item template | **5** | `StudentsView.axaml:55-60`, `IdentitiesView.axaml:38-43` |

Also: the `Button.Overflow` + `DotsHorizontal 18×18` + automation-name block repeats **7×**, and the
validation-error `TextBlock`'s `TextWrapping`/`IsVisible`/`Margin` repeat verbatim **5×** when they
could live in the existing `TextBlock.ValidationError` style (`Controls.axaml:128`).

This is ~250 lines of pure duplication, and D3 shows it is already drifting.

**Recommendation**: extract `DialogButtonRow`, `DetailHeader`, `FormField`, `RecordListItem` into
`Controls/`. `FormField` should set `AutomationProperties.LabeledBy`, which closes U7 for free.

### D5. Status flags bypass the `Tag` control · Low

`UI_STYLE_GUIDE.md:75-86` says `Tag` is for "a short flag sitting inline next to other content, e.g.
in a list row" — yet all five master-list templates hand-roll `FontSize="11"` +
`StatusArchivedBrush` instead, and `TermsView.axaml:31` uses bare `Opacity="0.6"` for the equivalent
"Completed" flag.

### D6. Two pages ignore the mandated sort/filter controls · Medium

`UI_STYLE_GUIDE.md:286-293` and CLAUDE.md both state this is the standard, not a per-page convention:

- **`ClassSchedulesView.axaml:20-22`** — a bare `CheckBox` is the page's entire filter UI. It has no
  search, no sort, and no `FilterCount`/`ClearFiltersCommand`. `ClassScheduleSortOptions`
  (`Core/Filters/ClassScheduleFilter.cs:15-20`) has `Day` and `Time`, so the "omit `SortByButton`"
  carve-out at guide L295-298 does **not** apply. Note the same file uses the pattern correctly for
  its nested rolls list (L69-94).
- **`TermsView.axaml:19-24`** — a hand-rolled `TextBox` + `CheckBox` row, which is precisely what
  `FilterBar` exists to replace. Sort is hardcoded to `StartDate desc` (`TermsViewModel.cs:72`)
  despite `TermSortOptions` offering `Name`/`StartDate`/`EndDate`.

### D7. Fixed sizes that clip · Medium

- `TermsView.axaml:64` (`Height="200"`) and `PassEditView.axaml:58` (`Height="120"`) put fixed-height
  lists **inside** page/dialog-level fading `ScrollViewer`s, producing a nested second scrollbar —
  the exact anti-pattern `UI_STYLE_GUIDE.md:199-209` forbids by name.
- `DashboardView.axaml:19-38` — three `Width="180"` tiles in a **non-wrapping** horizontal
  `StackPanel`; they clip off-screen on a narrow window. `FillWrapPanel` already exists for this and
  is used at `StudentsView.axaml:103`.
- **Nothing in the app sets `TextTrimming`** (Avalonia's default is `None`), so long names and emails
  in the fixed 320px master column (`MasterDetailView.axaml:8`) clip with no ellipsis.
- `Background="#80000000"` is duplicated as a literal at `MainWindow.axaml:138` and
  `MarkRollWindow.axaml:114` — the only two color literals outside `Styles/`.

---

## Findings — Usability

### U1. A ClassSchedule cannot be created · **Critical**

`ClassSchedulesViewModel.cs:48` constructs `AddClassCommand` and `AddClassAsync` (`:181-198`) is
fully implemented — but **no button in `ClassSchedulesView.axaml` is bound to it** (verified by
reading the file in full). Every other list page has an Add button (`IdentitiesView.axaml:31`,
`StudentsView.axaml:48`, `TermsView.axaml:23`).

Downstream: no new class schedules means no new roll targets and no new term-link targets, so the app
cannot grow beyond its seeded data.

### U2. All list-load failures are silent · **Critical**

`CollectionPageModelBase.cs:27` subscribes `RefreshCommand.ThrownExceptions` into `LastError`
(`:38-42`) — a property **no view binds** (verified by grep across the project).

The subtlety that makes this worse than a dead property: because a `ThrownExceptions` subscriber
exists, ReactiveUI does **not** route the exception to `RxApp.DefaultExceptionHandler`
(`App.axaml.cs:90`). So registering it actively *suppresses* the app's only working error surface.
The base class's own doc comment (`:8-10`) advertises "error surfacing via `ThrownExceptions`" — that
surfacing reaches nothing. A failed page load leaves an empty list and says nothing.

### U3. "Mark new roll" persists an orphan on cancel · High

`ClassSchedulesViewModel.cs:255-256` calls `AddAsync` on a `new ClassRoll(0, today, …)` *before*
opening the window. Cancelling leaves a persisted 0-attendee roll with no undo.

### U4. No empty states anywhere · High

- **`LinkedRecordsPanel`** (`Controls/LinkedRecordsPanel.axaml:31-66`) renders a blank bordered card
  when its list is empty — across ~10 usages (emergency contacts, health concerns, passes, classes
  attended, class rolls, attendees, linked classes, alterations, pass usage history). Single highest-
  leverage fix in the review: one `EmptyText` property fixes every site.
- **`MasterDetailView`** (`:10-14`) has no empty-list state, so a search returning zero results is
  indistinguishable from a loading list. Its `NoSelectionText` default ("Select an item to view
  details.") is overridden by **no page**, so all five show the same generic sentence.
- No "No matches" in `IdentityPickerView.axaml:19`, `PassPickerView.axaml:16`,
  `MarkRollWindow.axaml:33`.

### U5. Loading state computed on five pages, shown on one · Medium

`ViewModelBase`'s reentrant busy counter and `CollectionPageModelBase.cs:21-25` maintain `IsBusy` on
all five collection pages. **Only `DashboardView.axaml:20,41,64` binds it.** `MarkRollViewModel`
calls `StartBusy` at `:179,230` to no visible effect. The app contains no `ProgressBar`/`ProgressRing`
at all — only three plain `"Loading..."` `TextBlock`s.

### U6. Nine destructive actions with no confirmation · High

`ConfirmViewModel`/`ConfirmView` already exist, are generic, and are used **once**.

| Action | Site |
|---|---|
| Archive-or-hard-delete identity | `IdentitiesViewModel.cs:216` |
| Archive-or-hard-delete student | `StudentsViewModel.cs:384` |
| Remove emergency contact | `StudentsViewModel.cs:428` |
| Remove health concern | `StudentsViewModel.cs:451` |
| Delete pass | `StudentsViewModel.cs:487` |
| Delete term | `TermsViewModel.cs:112` |
| Unlink class from term | `TermsViewModel.cs:146` |
| Delete class roll | `ClassSchedulesViewModel.cs:268`, `ClassRollsViewModel.cs:152` |
| Reset all dummy data | `SettingsViewModel.cs:33-34` |

Two are worse than the generic case and need specific copy:
- **Archive can silently hard-delete.** `InMemoryIdentityRepository.cs:60-74` deletes the row outright
  when it has no links, and the user only learns which happened from the toast *afterwards*.
  `GetLinkageSummaryAsync` (`IIdentityRepository.cs:15`) already computes exactly what's needed to say
  so up front — it's currently used only for the merge preview.
- **Deleting a pass rewrites history.** `InMemoryPassRepository.cs:79-81` nulls `entry.Pass` on every
  roll that used it, silently.

Several mutations are also fully silent — no toast, no acknowledgement — against the severity
convention at `UI_STYLE_GUIDE.md:436-440`: `StudentsViewModel.cs:423,432,446,485`,
`ClassSchedulesViewModel.cs:222,266`.

### U7. Zero keyboard affordances · High

A grep for `KeyBinding|KeyDown|KeyGesture|IsDefault|IsCancel|HotKey|Focus(` across the project
returns **no matches**. Concretely:

- **Enter submits no dialog** and **Escape cancels none** of the 14. Clicking the dimmed backdrop
  does nothing either (`MainWindow.axaml:137-143` is a plain `Border`). The Cancel button is the only
  exit from every dialog.
- **No dialog sets initial focus**, so every one requires a mouse click before typing.
- **Modality is pointer-only.** `Border.DialogOverlay.Open` (`Styles/Controls.axaml:156-159`) flips
  `IsHitTestVisible`, which blocks clicks — but the `SplitView` beneath stays focusable, so Tab walks
  focus *behind* an open modal into the nav rail, where Space/Enter activate it.
- **The roll search is mouse-only** (`MarkRollWindow.axaml:40-47`): type, then click `+`. This is the
  most-repeated action in the app.
- ~25 form inputs are a loose `TextBlock` above a `TextBox` with no `AutomationProperties.LabeledBy`.

Smaller gaps: `SortByButton.axaml:15` lacks the `AutomationProperties.Name`/`ToolTip.Tip` its sibling
`FilterButton.axaml:14` has, and conveys ascending/descending by icon alone. `Button.Overflow` is
32×32 (`Controls.axaml:100-101`), below the ~40px minimum target size, and is the sole affordance for
per-record actions. Toast severity (`ToastContent.axaml:8`) is icon+color only.

### U8. Validation gaps · Medium

- **`ClassScheduleEditViewModel.cs:51-54` closes unconditionally with no validation**, and
  `ClassScheduleEditView.axaml` has no validation line. The day/time collision rule
  (`IClassScheduleRepository.cs:11`) is enforced by the repository *after* the dialog closed, so the
  error toast arrives with all input already discarded (`ClassSchedulesViewModel.cs:194-197`).
- **The merge picker doesn't apply the domain's own guard.** `MergeAsync` rejects Student+Student
  (`IIdentityRepository.cs:29-30`), and `IdentityPickerViewModel` already has an `excludeStudents`
  parameter (`:26`) — but `IdentitiesViewModel.cs:239-240` doesn't pass it. A user can pick an invalid
  target, read a full merge preview, confirm, and only then get an error.
- Validation everywhere is on-submit only, surfaced as one line at the dialog's bottom with **no
  field-level highlighting** — the message names the rule, not the offending field. `IdentityEditViewModel.cs:79`
  says "at least one valid phone number or email is required" without stating the actual rule (exactly
  8 or 10 digits, `Identity.cs:80`).

### U9. Navigation accumulates state the user cannot reach · Medium

`MainWindowViewModel.cs:75` uses `Router.Navigate.Execute(...)`, never `NavigateAndReset`, so every
nav-rail click pushes a new page — including re-clicking the current page. Back navigation exists in
exactly one place (`PassDetailView.axaml:17`), so the stack grows unboundedly and is never unwound.

Page VMs are transient (`App.axaml.cs:99-148`), so selection resets on every navigation. The
`AppScreen.Pending*SelectionId` channel patches this for three cross-page links only;
`PendingClassScheduleSelectionId` (`AppScreen.cs:38`) is dead by its own doc comment.

Selection also moves on its own: `CollectionPageModelBase.cs:52-60` falls back to
`Items.FirstOrDefault()` whenever the previous selection isn't in the refreshed list, so typing in a
debounced search box repeatedly swaps the detail pane to whatever now sorts first.

### U10. Filter and sort capability that exists but isn't exposed · Low

| Unexposed | Declared |
|---|---|
| Identity search by email / phone / first / last individually | `IdentityFilter.cs:15-18` |
| Class schedule `Day`/`TimeFrom`/`TimeTo`/sort | `ClassScheduleFilter.cs:8-12` |
| Roll `TimeFrom`/`TimeTo`; `ClassScheduleId` on the Class Rolls page | `ClassRollFilter.cs:8,10-11` |
| Term sort by `Name`/`StartDate`/`EndDate` | `TermFilter.cs:13-19` |
| Emergency-contact sort | `EmergencyContactFilter.cs:12-17` |

Class Rolls is the sharpest: **no search at all**, and no class filter, so finding a specific roll
means scrolling. Unused repository methods: `IClassScheduleRepository.TryDeleteAsync` (`:19`),
`IPassRepository.AddAlterationAsync`/`RemoveAlterationAsync` (`:16-17`), `GetByIdAsync` (`:19`).

`EmergencyContact.Relationship` is writable (`EmergencyContact.cs:9`) but only ever set at link time —
changing it requires remove-and-re-add.

### U11. Half-finished surfaces · Low

- `AlterationEditViewModel`'s doc comment says "Add/edit a single `PassAlteration`" (`:7`) but the
  title is hardcoded `"Add alteration"` (`AlterationEditView.axaml:11`), the constructor takes no
  existing alteration, and `PassEditView.axaml:58-60` wires only Add/Remove. Alterations cannot be
  edited.
- `SettingsViewModel.cs:31-36` wipes and re-seeds all data with no confirmation, then tells the user
  to "Navigate to another page to see the fresh data". Any open `MarkRollWindow` keeps references to
  discarded objects.
- `IdentityPickerViewModel.SearchAsync` (`:78-84`) filters client-side with `Contains`, while the
  Identities page passes `NameFilter` to the repository, which also does `StartsWith` on first/last
  name (`InMemoryIdentityRepository.cs:239-242`). The same search box behaves differently in two
  places.

No `TODO`/`FIXME`/`NotImplementedException` exists anywhere in the Avalonia project.

---

## Findings — Architecture

### A1. `SelectMany` over async where `Switch` is required · High

Six sites use `WhenAnyValue(...).SelectMany(async _ => await LoadX())`:
`StudentsViewModel.cs:77-79, 87-90`, `IdentitiesViewModel.cs:54-56`,
`ClassSchedulesViewModel.cs:62-64, 66-70`, `MarkRollViewModel.cs:66-76`.

`SelectMany` **merges** concurrent inner tasks. Two rapid selection changes start two overlapping
loads that each `Clear()` then `Add()` into the same `ObservableCollection` — last writer wins, and an
interleaved `Clear()` mid-`foreach` can leave a mixed list showing one record's linked data under
another's header. The correct operator is `Select(...).Switch()`.

**This is latent, not live.** Every `InMemory*Repository` returns `Task.FromResult` synchronously
(e.g. `InMemoryStudentRepository.cs:38,57,93,117,129,154,166`), so nothing interleaves today. It
becomes a real race the moment a database implementation of `Core` lands — which is the argument for
fixing it now, while it is cheap and verifiable by inspection.

### A2. Async chains with no `onError` · High

The same six sites call `.Subscribe()` with no error handler. If the load throws, Rx rethrows on the
UI thread **and the subscription is dead permanently** — selection changes stop loading linked data
for the remaining life of that ViewModel instance.

### A3. Five fire-and-forget constructor loads · Medium

`DashboardViewModel.cs:36`, `PassDetailViewModel.cs:34`, `PassEditViewModel.cs:97`,
`ClassLinkViewModel.cs:30`, `IdentityPickerViewModel.cs:43` all do `_ = LoadAsync()` with no error
path. A failure is an unobserved `Task` exception, silently discarded, leaving a permanently empty
list or dropdown.

`PassEditViewModel:97` is the worst instance: a failed `LoadTermsAsync` leaves `Terms` empty, and
`Save()` (`:220-223`) then reports "Select a term and a class" — blaming the user for a load failure.

### A4. The filter-state convention has no enforceable surface · Medium

**Narrower than it first appears.** `UI_STYLE_GUIDE.md:338-344` explicitly sanctions per-list naming
when a page has more than one filterable list, so `PassFilterCount`/`RollFilterCount` for *child*
lists are correct and intentional, not drift.

The genuine inconsistency is at the *page's own* list: `ClassRollsViewModel.cs:111` calls it
`FilterCount`, while `StudentsViewModel.cs:125` and `IdentitiesViewModel.cs:85` call it
`ActiveFilterCount` — and `ClassSchedulesViewModel`'s main list has neither, nor a clear command
(tying back to D6).

The underlying weakness is real regardless: the contract is enforced only by whatever XAML happens to
bind it. A missing or misnamed implementation is a silently-empty badge, not a build error.

### A5. Two testability blockers · Medium

- **`(AppScreen)hostScreen` unchecked downcast in 5 ViewModels** (`StudentsViewModel.cs:46-47`,
  `IdentitiesViewModel.cs:36-37`, `ClassSchedulesViewModel.cs:37-38`, `ClassRollsViewModel.cs:30-31`,
  `PassDetailViewModel.cs:27-28`). A mock `IScreen` throws `InvalidCastException`, so these cannot be
  unit-tested without constructing the real `AppScreen`.
- **`Locator.Current.GetService<T>()` inside command bodies** (`StudentsViewModel.cs:291,297`,
  `IdentitiesViewModel.cs:174`, `PassDetailViewModel.cs:56`) makes `StudentsViewModel` and
  `IdentitiesViewModel` **mutually type-coupled** and requires a globally-populated Splat container in
  any test.

### A6. Collection-page boilerplate repeated across 5 ViewModels · Medium

Identical in all five: the `hasSelection` observable, the "refresh on filter/sort change"
`Skip(1)` chain (hand-repeating the exact footgun `SearchableCollectionPageModelBase` exists to
encapsulate), the constructor initial-load kick, and `SortOrderOptions`.

The `if (Selection is null) return;` guard appears **17×** despite every one of those commands
already being gated by `hasSelection`'s `canExecute`. `CancelCommand` is byte-identical across 12
dialog ViewModels while `DialogViewModelBase` (15 lines) owns nothing.

Two ViewModels also re-implement the search debounce by hand rather than extending the searchable
base — `MarkRollViewModel.cs:66-76` (250ms) and `IdentityPickerViewModel.cs:37-41` (200ms), **both
without the `Skip(1)`** the base documents as mandatory. `IdentityPickerViewModel` additionally kicks
an eager `_ = SearchAsync()` at `:43`, so its initial load runs twice.

### A7. Interface shapes that force bad call sites · Medium

- **`IToastService.AttachHost(TopLevel)`** (`IToastService.cs:1,13`) puts an Avalonia view type on an
  interface **9 ViewModels** depend on. It has exactly one legitimate caller
  (`Views/MainWindow.axaml.cs:22`) and belongs on a separate `IToastHost`.
- **`IRollWindowService.OpenOrActivate(ClassRoll, Action)`** (`:15`) — `Action`, not `Func<Task>` —
  is what *forces* the fire-and-forget `_ = SomeAsync()` at all five call sites
  (`ClassRollsViewModel.cs:143`, `ClassSchedulesViewModel.cs:251,258,263`, `DashboardViewModel.cs:96`).
  `RollWindowService` is also a hidden second composition root: it constructs both `MarkRollViewModel`
  and `MarkRollWindow` itself (`:38-39`), which is why it takes four repositories purely to forward
  them, and it reaches into `Application.Current.ApplicationLifetime` (`:49`) as an ambient global.
- **`SettingsViewModel.cs:17` depends on the concrete `InMemoryDataStore`**, so the ViewModel layer
  has a hard dependency on the dummy provider and will not compile once `Core.Dummy` is dropped.

### A8. Zero cancellation · Low (today)

`grep -r "CancellationToken" YogaClassManager.Avalonia` returns **no matches**, while every `Core`
`IDbModel`/repository method accepts one (`Core/Data/IDbModel.cs:15,22`). Every call site passes
`default`. Harmless against synchronous in-memory repositories; relevant alongside A1 when a real
database lands.

### A9. Core's paging infrastructure is unused · Low

`GrowableCollection`/`IGrowableCollection` are never used by any ViewModel, and
`Controls/GrowableItemsControl` — which wraps them — is referenced by **no view**. Every list eagerly
loads its entire result set via `LoadMultiple()` with the default `count = uint.MaxValue`.

Not a defect at current data volumes; noted because it is dead code carrying maintenance cost, and
because the decision to page or not should be explicit.

### A10. Subscription disposal has no mechanism · Low

The only disposed subscription in the app is `MarkRollWindow.axaml.cs:10`. `WhenActivated`,
`CompositeDisposable`, and `IActivatableViewModel` are used **zero** times.

Nothing leaks today: page ViewModels only subscribe to `this`, so a VM and its subscriptions become
collectable together. But page VMs are transient and services are singletons, so **the first
subscription anyone writes against `dialogService`, `appScreen`, or a repository event will leak one
page VM per navigation**, with nothing in the codebase positioned to catch it.

### A11. Test coverage · Medium

221 lines across three files: five tests of `SearchableCollectionPageModelBase`'s debounce and six of
`ScrollEdgeFadeMath.Compute`. **All 26 production ViewModels have zero coverage.** No mocking library
and no `Avalonia.Headless` package, so nothing requiring a repository or `TopLevel` is testable as
configured.

Notable gaps: `CollectionPageModelBase`'s own selection-restoration and error paths are never
asserted; `DialogService`'s nesting stack is pure and testable *today* and untested; and
`MarkRollViewModel.IsRecentlyExpired` (`:288-294`) was deliberately made `public static` and pure —
i.e. someone intended to test it — and still has none. `MarkRollViewModel.SaveAsync` (`:140-168`),
the most data-destructive logic in the app, is untested and non-transactional: a mid-way throw leaves
the store half-updated *and* `savedEntryState` stale (`:166` never reached), so the next save diffs
against the wrong baseline.

---

## Flagged domain gaps — outside UI scope

Two findings are real and user-visible but are **`Core` behaviour changes, not UI work**. Recorded
here for a separate decision rather than folded into UI remediation:

1. **Attendance never consumes a pass.** No code path anywhere increments `Pass.ClassesUsed`; the only
   writer is the pass-edit dialog (`InMemoryPassRepository.cs:47`). Every "N remaining of M total"
   shown in Students, `PassDetailView`, and the pass picker is therefore manually-maintained fiction
   that drifts from actual attendance.
2. **`TermClassSchedule.Uses` is never incremented.** Seeded at 0 (`DummyDataSeeder.cs:30-37`) and
   never written, so `UnlinkClassAsync`'s guard (`ITermRepository.cs:19`) never fires and
   `TermsViewModel.cs:151`'s "it has already been used this term" warning is unreachable.

Both need a decision about where attendance-to-pass consumption belongs before either can be fixed.

---

## Prioritised remediation

| Phase | Findings | Rationale |
|---|---|---|
| **1 — Functional defects** ✅ **done** | U1, U2, U3, D6 | A core entity cannot be created; all load errors are silent. Everything else is polish by comparison. |
| **2 — Feedback & confirmation** ✅ **done** | U4, U5, U6, U8 | Highest user-visible value per unit of work. `LinkedRecordsPanel.EmptyText` alone fixes ~10 sites; `ConfirmViewModel` already exists. Depends on Phase 1's U2 for the error channel. |
| **3 — Keyboard & accessibility** ✅ **done** | U7 | Entirely new behaviour. Best done at the dialog *host* — one fix, not 14. |
| **4 — Design consistency** ✅ **done** | D1, D2, D3, D4, D5, D7 | Mechanical and low-risk, but touches the most files. `FormField` (D4) should land before U7's label association. |
| **5 — Architecture** ✅ **done** | A1–A6 | Invisible to users; deferrable without blocking anything. But A1/A2 must precede any real-database work on `Core`, since that is what turns the latent races into live ones. |
| **Recorded, not scheduled** | A7–A11, U9, U10, U11 | Real, lower-leverage, or dependent on decisions not yet made (paging strategy, navigation model, test infrastructure). |

### Phase 1 outcome

- **U1** — `ClassSchedulesView.axaml` now has an "Add class" button bound to `AddClassCommand`.
- **U2** — `CollectionPageModelBase` now raises an error toast alongside `LastError`, so a failed load
  is no longer silent. (`LastError` still needs an inline surface — that is Phase 2's `MasterDetailView`
  work.)
- **U3** — a brand-new roll is no longer INSERTed up front; `MarkRollViewModel` creates it on save, and
  `RollWindowService`'s dedup key falls back to schedule+date so unsaved rolls (all `Id == 0`) no
  longer collide with each other.
- **D6** — Class Schedules now uses `SortByButton` + `FilterButton` (no search box: `ClassScheduleFilter`
  has no text field, so this is the guide's "directly, not `FilterBar`" case) and exposes the `Day`
  filter; Terms now uses `FilterBar` with real `Name`/`StartDate`/`EndDate` sorting instead of a
  hardcoded order.

**A11 partially addressed as a side effect**: the test project gained `Avalonia.Headless.XUnit`, a
`TestAppBuilder`, and `FakeToastService`/`FakeRollWindowService` doubles. Tests went from 11 to 38,
now covering `CollectionPageModelBase`'s own contract (including the error path), the Class Schedules
filter/sort/roll-creation flows, `MarkRollViewModel`'s save-vs-create paths, and headless render +
binding-resolution smoke tests for three views. Rendering views headlessly is what catches the failure
mode the build cannot: XAML that compiles but throws on load, or a binding path that silently
resolves to nothing.

### Phase 2 outcome

- **U4** — `MasterDetailView` gained `EmptyText`/`ErrorText`/`IsBusy` and `LinkedRecordsPanel` gained
  `EmptyText`, both backed by a shared `CollectionEmptinessWatcher` that follows
  `INotifyCollectionChanged` (these lists are `Clear()`ed and refilled in place, so a one-shot count
  would go stale). All five pages now pass page-specific empty text and their own `NoSelectionText`,
  and eight `LinkedRecordsPanel` call sites have empty text.
- **U2 completed** — `LastError` now has the inline surface Phase 1 left outstanding.
- **U5** — `IsBusy` is bound on all five list pages and in `MarkRollWindow`; the app has real
  `ProgressBar`s instead of three plain `"Loading..."` `TextBlock`s.
- **U6** — all nine destructive actions now confirm, via a new `IDialogService.ConfirmAsync`
  extension so each call site stays one line. The identity prompt calls `GetLinkageSummaryAsync`
  first and says "delete permanently" or "archive" accordingly; the pass prompt states that deleting
  a pass unlinks it from past attendance. The silent mutations at `StudentsViewModel` and
  `ClassSchedulesViewModel` gained success toasts.
- **U8** — `ClassScheduleEditViewModel` now checks the day/time uniqueness rule *inside* the dialog,
  so a collision shows a validation line with the input intact instead of a post-close toast with the
  input discarded. The merge picker now passes `excludeStudents` when the survivor is a Student, so an
  invalid Student+Student merge can no longer be selected and confirmed before being rejected.

**New finding while implementing U6**: `StudentsViewModel.ToggleArchiveAsync`'s
`ArchiveResult.Deleted` branch (`"had no other links, so the record was deleted."`) is **unreachable**.
`IdentityLinkageSummary.HasAnyLinks` counts `IsStudent`, so a Student always has links and
`ArchiveOrDeleteAsync` always archives. The branch is left in place as defensive handling of the
repository contract, which a real database implementation could satisfy differently.

Test count is now 46 (Avalonia) + 42 (Core), including tests that assert a *declined* confirmation
leaves the store untouched — the half of the fix that's easy to regress by wiring a prompt whose
result nothing checks.

### Phase 3 outcome

The whole keyboard contract landed in one place, `Controls/DialogHost` — the shared overlay both
MainWindow and MarkRollWindow now render, replacing the two copies of the overlay markup. Every dialog
inherits it: **Escape** (and a backdrop click) cancels, **Enter** runs the dialog's `DefaultCommand`
when that command's own `CanExecute` allows, **initial focus** lands inside the dialog, **Tab** cycles
within the card, and focus is **restored** to wherever it was when the dialog closes. Supporting that,
`DialogViewModelBase<TResult>` now owns `CancelCommand` (previously byte-identical in all twelve
dialogs — A6 in passing) and exposes `DefaultCommand` through a new non-generic `IDialogViewModel`,
which is how the host drives a dialog without knowing its `TResult`.

- **Two decisions worth not re-litigating.** Initial focus prefers a real input *over* a Button,
  because a focused Button consumes Enter itself — focusing one would make Enter mean "activate that
  button" rather than "submit". The consequence is that a prompt with no fields (every confirmation)
  starts on **Cancel**, so a single Enter cannot confirm a destructive action. That is deliberate and
  has a test. Second, the key handler *bubbles* rather than tunnels, so a multi-line `TextBox`,
  `NumericUpDown` committing typed text, and an open `ComboBox` dropdown all keep their claim on the key.
- **`FormField`** (pulled forward from D4, as the plan required) now wraps all ~25 form inputs and sets
  `AutomationProperties.LabeledBy` — the part markup cannot express. A `TextBlock` sitting above a
  `TextBox` was never a label as far as a screen reader is concerned.
- **Mark Roll is keyboard-operable.** Enter in the search box adds the top match; Enter on a
  highlighted result row adds that row (one `AddSelectedStudentCommand` serves both, since "whatever is
  selected, or the first result if nothing is" is the same intent either way). Escape closes via the
  existing discard-changes prompt, Ctrl+S saves. This is the most-repeated action in the app and was
  previously type-then-reach-for-the-mouse.
- **Non-text state got text.** `SortByButton` conveyed direction by arrow glyph alone; it now exposes
  `SortDescription` ("Sort by Name, ascending") as its accessible name and tooltip. Toast severity was
  icon + colour only; `ToastContent` prefixes its accessible name with the severity word. `FilterBar`'s
  search box takes its `Watermark` as an `AutomationProperties.Name`, since a watermark disappears the
  moment there is text in the box.
- **`Button.Overflow` is 36x36**, up from 32 — not the ~40 this review asked for. These sit in a row
  beside ordinary Buttons whose FluentTheme height is 32, and a 40px square there reads as a
  mismatched, oversized control. 36 is a deliberate compromise, not an oversight.

Also closed while in the same files: the "No matches" gap U4 left open in `IdentityPickerView`,
`PassPickerView` and both of `MarkRollWindow`'s columns. Mark Roll's distinguishes "type a name to
search" from "nothing matched" — only one of those is a dead end.

### Phase 4 outcome

- **D1/D2** — the token set gained `FontWeightMedium/SemiBold/Bold`, `MutedOpacity`, ramp entries
  `Micro` (11), `StatValue` (32), `AppTitle` (16) and `Muted`, plus `TagNeutralBrush` and
  `OverlayScrimBrush` (which removes the last two colour literals outside `Styles/`). The three
  repeated margins are now `Thickness` tokens (`HeaderRowMargin`, `GroupGapMargin`,
  `DialogButtonRowMargin`). The style guide's claim that spacing tokens cannot be resource-bound was
  corrected — `Spacing` is a `double` property and binds fine — and views now bind it. After this pass
  there are **no** `Opacity="0.x"` or `FontSize=` literals left in any view.
- **D3** — the off-scale values are resolved: the two `Margin="0,12,0,0"` dialog rows went with the
  button row itself, `Spacing="6"`/`"2"`/`"12"` are on the scale, and `Tag`'s `6,2` survives as the one
  **documented** exception (`TagPadding`), because a badge sized to 11px text cannot use a 4px step.
- **D4** — all four duplicated structures are controls now: `DialogButtonRow` (was 14 copies),
  `DetailHeader` (5), `FormField` (~25), `RecordListItem` (5). The `Button.Overflow` block (7 copies)
  collapsed into the style itself via a `<Template>` `Content` setter — which is what gives each button
  its own icon instance rather than sharing one control app-wide — and `TextBlock.ValidationError`
  absorbed the `TextWrapping`/`Margin` its five call sites each repeated verbatim.
- **D5** — status flags in list rows go through `Tag` via `RecordListItem`, including Terms'
  "Completed", which was bare `Opacity="0.6"` text.
- **D7** — the two fixed-height lists inside fading `ScrollViewer`s (`TermsView` 200, `PassEditView`
  120) lost their heights and grow to fit; the Dashboard's three fixed-width tiles moved to
  `FillWrapPanel` so they wrap instead of clipping off a narrow window; `TextTrimming` is set on the
  text that can overflow the fixed 320px master column and on detail headings.

`DetailHeader.Actions` is a plain `Control` slot rather than a `MenuFlyout` property. That is the shape
`LinkedRecordsPanel.Filters` already proves keeps a caller's own `ElementName=Root` bindings working
inside a hosted control; a `MenuFlyout` property would have put the caller's menu items outside the
view's namescope, which is a different and much less certain proposition.

### Phase 5 outcome

- **A1/A2, including a correction to this review's own diagnosis.** The six `SelectMany` sites are now
  `Select(...).Switch()` in one shared helper, `CollectionPageModelBase.ReloadOn`. Writing a test for it
  showed that **`Switch` alone does not fix the described failure mode**: these loads mutate their
  `ObservableCollection` in place rather than returning a value, so discarding a superseded load's
  *result* changes nothing — its writes still land. `ReloadOn` therefore takes a `CancellationToken`
  (Rx cancels it when `Switch` drops the previous load) and each load passes it to its repository calls
  and re-checks it before writing. That also delivers the part of A8 that actually matters. A second
  test showed the same shape of problem with `onError`: an error reaching `Switch` terminates the
  *outer* sequence, so handling it at the subscriber would have reported the failure and still left the
  pane permanently dead. The `Catch` sits inside the inner observable instead.
- **A3** — `ViewModelBase.LoadOnCreate(load, onError)` replaces all five bare `_ = LoadAsync()` calls.
  Pages report through a toast; dialogs put the message on their own validation line, beside the
  control that would otherwise just be mysteriously empty. `PassEditViewModel` — the worst case, where
  a failed term load made Save blame the user with "Select a term and a class" — now says what happened.
- **A4** — `ClassRollsViewModel.FilterCount` is `ActiveFilterCount`, matching the other pages, and the
  style guide now states the rule (`ActiveFilterCount` for a page's own list, a prefix for child lists)
  rather than leaving it to be inferred from examples.
- **A5** — new `Services/IPageNavigator`. Both halves are gone: no ViewModel casts
  `(AppScreen)hostScreen`, and no command body calls `Locator.Current.GetService<TOtherPage>()`.
  Students and Identities no longer reference each other's types, and `FakeNavigator` in the test
  project is a double that previously *could not exist*.
- **A6** — `CollectionPageModelBase` gained `HasSelection`, `SortOrderOptions` and
  `RefreshWhenChanged` (which owns the mandatory `Skip(1)`), removing that boilerplate from all five
  pages. `MarkRollViewModel`'s and `IdentityPickerViewModel`'s hand-rolled debounces gained the
  `Skip(1)` they were missing — which also fixes `IdentityPickerViewModel` running its initial load
  twice.

**Deliberately not done**: the 17 `if (Selection is null) return;` guards stay. They are redundant
against `HasSelection`'s `canExecute` for a *button* press, but these commands are also invoked
directly — by other commands, and by tests — where the guard is the only thing between a mis-sequenced
call and a `NullReferenceException`. Removing them trades a real safety net for a cosmetic saving.

`AppScreen.PendingClassScheduleSelectionId` is still producer-less, now reachable only through
`IPageNavigator.TakePendingClassScheduleSelection`. It was left in place rather than deleted because
whether it should exist is a navigation-model decision (U9), which is recorded and not scheduled.

### Test coverage after Phases 3-5

94 (Avalonia) + 42 (Core), up from 46 + 42. The new Avalonia tests cover the dialog keyboard contract
driven through *real key input* on the headless platform (the routing is what is at risk of regressing,
not the commands), `FormField`'s label association, `DialogButtonRow`'s
accent-button-equals-`DefaultCommand` invariant, Mark Roll's Enter-to-add paths and its empty-state
wording, `RecordListItem`'s rendering including the archived `Tag`, `ReloadOn`'s supersede and
survive-a-failure behaviour, `RefreshWhenChanged`'s `Skip(1)`, and page construction against a plain
`IScreen` with no service locator.

`ViewLoadTests` is the cheapest of them and worth keeping: it loads the XAML of all 24 Views. Now that
design tokens are bound rather than written as literals, a `{StaticResource}` typo is the realistic way
to break a view, and that throws at *load* time, not build time. Before it, most views were only proved
to load by launching the app and navigating to them by hand.
