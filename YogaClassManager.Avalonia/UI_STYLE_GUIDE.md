# Avalonia UI style guide

This documents the design system introduced for `YogaClassManager.Avalonia` (spacing/color/type
tokens, the collapsible nav rail, and shared control/dialog conventions). It exists so new UI work
looks like it belongs, instead of every view reinventing spacing, colors, and typography.

**Keep this file up to date.** Whenever you add a new token, a new shared style/`Classes` selector,
a new top-level page, a new reusable control, or change an existing convention documented here,
update this file in the same change. If you find this file out of sync with the code, fix the file
— treat drift here the same as a failing test.

`YogaClassManager.Avalonia.Tests/ViewLoadTests.cs` loads every View's XAML, which is what catches a
`{StaticResource}` key that doesn't exist — those throw at load time, not build time, so a mistyped
token name introduced while following this guide fails there rather than in front of a user.

## Where things live

- `Styles/Tokens.axaml` — spacing, corner-radius, semantic color, and typography resources. Merged
  into `Application.Styles` in `App.axaml`.
- `Styles/Controls.axaml` — global `Style` selectors (not per-view edits) for common controls:
  rounded corners, hover/pressed transitions, the `Border.Card` surface, `TextBlock.ValidationError`,
  `Button.Overflow`, and `FormField`'s template. Also merged into `Application.Styles`.
- `App.axaml` — style include order matters: `FluentTheme`, then `MaterialIconStyles`, then
  `Tokens.axaml`, then `Controls.axaml`, so app-level styles can override FluentTheme defaults.

Both files are plain Avalonia `Styles` (not `ResourceDictionary`) so they can hold both resources
(via `<Styles.Resources>`) and `<Style Selector="...">` rules in one file.

## Design tokens

**Spacing** (`Styles/Tokens.axaml`): `PagePadding` (Thickness, 24 — margin on every top-level page's
root panel), and `SpacingXs`/`Sm`/`Md`/`Lg`/`Xl` (`x:Double`: 4/8/16/24/32) for ad hoc gaps.

**Bind these rather than writing literals.** The double-typed tokens go straight onto any `double`
property — `Spacing="{StaticResource SpacingSm}"` on a `StackPanel`, `Grid.RowSpacing`/`ColumnSpacing`
— and that is now the convention. (An earlier version of this guide said spacing tokens couldn't be
resource-bound at all; that was only ever half true. `Margin`/`Padding` are `Thickness`-typed and
genuinely can't take an `x:Double` resource, which is what the original note was about.)

For the repeated margins, use the `Thickness` tokens instead of retyping the numbers:
| Token | Value | Use for |
|---|---|---|
| `PagePadding` | 24 | Every top-level page's root panel |
| `HeaderRowMargin` | `0,0,0,8` | A header/filter row sitting above the content it labels |
| `GroupGapMargin` | `0,8,0,0` | Extra separation above a control that starts a new group in a stacked form |
| `DialogButtonRowMargin` | `0,16,0,0` | The gap above a dialog's button row (owned by `DialogButtonRow`) |
| `TagPadding` | `6,2` | `Tag` only — the one sanctioned **off-scale** value; a badge sized to its 11px text |

Any other reusable `Thickness` should be declared as `<Thickness x:Key="...">` here rather than
repeated inline. Values off the 4/8/16/24/32 scale are drift unless they're documented (as `TagPadding`
is) with a reason.

**Corner radius**: `RadiusSmall` (4, buttons/inputs), `RadiusMedium` (8, cards), `RadiusLarge` (12,
reserved for larger surfaces — not yet used, available for future dialog/panel work).

**Tile size**: `TileSize` (136) and `TileIconSize` (36) size `Button.Tile`, the Dashboard's square
action tiles (see "Global control styles").

**Control height**: `ControlHeight` (36) is the `MinHeight` every `Button`, `TextBox`, `ComboBox`,
`CalendarDatePicker` and `NumericUpDown` gets from `Styles/Controls.axaml` — don't set `Height`/
`MinHeight` on an individual instance of these. One token means a row mixing any of them (a search
box beside a `SortByButton`, an accent button beside a `Button.Overflow`) lines up without a
per-row alignment fix. Because 36 is taller than FluentTheme's content-height default, `Button` and
`TextBox` also set `VerticalContentAlignment="Center"` there (Fluent's defaults, Stretch/Top, leave the
text riding high in the extra space); `TextBox[AcceptsReturn=True]` goes back to `Top` so multi-line
text doesn't drift as it grows. Don't override either per instance. This used to be Overflow-only (FluentTheme's own default is 32 for all five);
`DetailHeader`'s primary-action-button-beside-Overflow pair was the visible mismatch that came from
that split. `Button.Overflow` still sets its own `Width` to `ControlHeight` to stay square — that's
the one place a call site should reference the token directly, since nothing else needs an explicit
`Height`/`Width` now that the base rules cover it.

Also set on `Button`: `VerticalContentAlignment="Center"`, needed *because* of the above.
FluentTheme's own default is `Stretch`, which barely showed at the old ~32px natural height, but a
button whose `MinHeight` now routinely exceeds its content's natural size will stretch its
`TextBlock` and draw the text from the top of the enlarged bounds instead of centering it — text
visibly rides high. `InteractiveStateTests.AButtonTallerThanItsContent_CentersTheContentVertically`
guards this.

**Font weight**: `FontWeightMedium`/`FontWeightSemiBold`/`FontWeightBold`. Bind these
(`FontWeight="{StaticResource FontWeightMedium}"`) rather than writing `FontWeight="Medium"` — those
three are the whole set the app uses, and a fourth usually means the type ramp is missing an entry
instead.

**Typography** — apply via `Classes`, not inline `FontSize`/`FontWeight`:
| Class | Size / weight | Use for |
|---|---|---|
| `PageTitle` | 28 / SemiBold | Top-level page headers ("Students", "Dashboard", ...) |
| `SectionTitle` | 20 / SemiBold | Detail-pane record titles, in-page section headers |
| `DialogTitle` | 18 / SemiBold, `Margin="0,0,0,8"` | The title line in every `Views/Shared/*` dialog window |
| `StatValue` | 32 / SemiBold | The single large number on a Dashboard stat tile |
| `Caption` | 12, muted | Secondary/small text: emails under names, attendee counts, sub-labels |
| `Micro` | 11 | The smallest step: inline flags and metadata on a dense list row (what `Tag` uses) |
| `Muted` | Opacity `MutedOpacity` (0.7) | Secondary text *at the surrounding size* — pair with `Caption` only if it should also be small |

Don't hand-write `FontSize="24" FontWeight="Bold"` etc. on new headers — add a `Classes="..."` that
matches one of these, or add a new class here if none fit (and document it in this table).

**One opacity for "secondary".** `MutedOpacity` (0.7) is the single value for "secondary metadata
beside a record name"; 0.5, 0.6 and 0.7 were previously all in use for that same idea across 16 sites.
Prefer `Classes="Muted"`; reach for the raw resource only where a `Classes` selector can't apply.

**Missing values show a Muted "n/a"**, not an empty line or a dropped row: a labelled detail field
(`Phone:`/`Email:` on the Identities and Students detail panes) keeps its label and shows
`<TextBlock Text="n/a" Classes="Muted" />` in place of the value, toggled with
`StringConverters.IsNullOrEmpty`/`IsNotNullOrEmpty`. So the row stays put whether or not the value
is filled in.

**Interactive state palette** — `InteractiveHoverBrush`/`InteractivePressedBrush` (overlay tints for
controls resting on a custom surface: nav items, overflow buttons, list rows),
`InteractiveSelected`/`SelectedHover`/`SelectedPressedBrush` (selection: **theme-scoped**: the
accent's palest `Light3` (hover/pressed `Light2`) under the light theme's dark text, its deep
`Dark2`/`Dark1` shades under the dark theme's white text; reusing the pale shades in dark mode left
white text at ~2:1 contrast),
`ButtonBackground` + `ButtonFillHover`/`Pressed`/`DisabledBrush` (the default filled `Button`, also
theme-scoped. The light theme overrides FluentTheme's own `ButtonBackground` key with a lighter,
ordered scale: resting 8% black, hover 12%, pressed 18%, disabled 4%. Fluent's was a 20% resting fill
with a *lighter* hover. Dark keeps Fluent's values), and
`AccentAction`/`Hover`/`Pressed`/`DisabledBrush` (primary actions). Every one is `Opacity=1` by
design — see "The interactive-state invariant" below before adding to or changing these.
`AccentSurfaceBrush` is the same light/dark split for a non-interactive accent-tinted surface that
carries ordinary text (the Dashboard's stat cards). Any surface that carries ordinary text must reach
WCAG AA (4.5:1) against the theme's text colour (`SystemBaseHighColor`) in **both** themes;
`InteractiveStateTests.TextOnSelectionAndAccentSurfaces_MeetsAAContrast` checks these, so add any new
one there. Never put a raw `SystemAccentColorLight*` behind text: it's only readable in the light
theme.

**Semantic status colors** — theme-aware via `ResourceDictionary.ThemeDictionaries` (Light/Dark),
chosen to clear WCAG AA (≥4.5:1) against each variant's default background:

| Resource | Meaning | Light | Dark |
|---|---|---|---|
| `StatusErrorBrush` | Validation errors, blocking problems | `#C42B1C` | `#FF8A80` |
| `StatusWarningBrush` | Depleted passes, non-blocking warnings, a student's health concerns on Mark Roll's attendee rows (always with the `MedicalBag` icon, never colour alone) | `#9D5D00` | `#FFB74D` |
| `SkeletonBrush` | Skeleton loading placeholders (`Border.Skeleton`, `SkeletonList`) - the theme's `SystemBaseLowColor`, never animated | theme | theme |
| `StatusArchivedBrush` | Archived records, expired passes | `#B34700` | `#FFA766` |
| `StatusSuccessBrush` | Reserved for future positive/success states | `#0F7B0F` | `#81C995` |
| `StatusInfoBrush` | Info toast message text | `#0B57D0` | `#90CAF9` |

Never use color literals (`Foreground="Red"`, `"OrangeRed"`, `"Goldenrod"`, etc.) for status/semantic
meaning — use `{DynamicResource StatusXBrush}` so it stays correct in both themes and stays
centralized. If you need a new semantic color, add it to both the Light and Dark
`ResourceDictionary` blocks and add a row to this table — never add just one variant.

**`OverlayScrimBrush`** (`#80000000`) is the dimmed backdrop behind a modal dialog, used by
`Controls/DialogHost`. Also fixed rather than theme-varying: a scrim's job is to darken whatever is
behind it, which light and dark pages need equally. It was previously a literal repeated in two
windows — the only color literals outside `Styles/`.

**Tag fill colors** — `TagArchivedBrush` (`#B34700`), `TagWarningBrush` (`#9D5D00`) and
`TagNeutralBrush` (`#5F6368`, for a state that's neither a problem nor a warning, just no longer
current — a completed term), used only by
`Controls/Tag.axaml` (see below). Unlike the `StatusXBrush` table above, these are **not**
`ThemeDictionaries`-scoped — a single fixed value is used in both Light and Dark. That's deliberate:
the `StatusXBrush` tokens are tuned as *foreground* text color against the page's own background, so
they invert to a paler tone in Dark mode to stay readable there; a `Tag`'s "background" is its own
solid fill (always paired with white text), not the page, so inverting it in Dark mode would swap it
for the Dark `StatusXBrush` variant's pale tone — which fails contrast against white text. If you add
a new tag color, pick one saturated value that keeps white text at WCAG AA in both themes, not a
Light/Dark pair.

## Tags (`Controls/Tag.axaml`)

A small rounded, colored-background label for one-word status flags — `Color` (`IBrush`) and `Label`
(`string`) properties, `RadiusSmall` corners, always-white text. Replaces the older convention of
bold, semantically-colored plain text for this (still fine for prose-style status lines like
"Archived" under a record's own details — `Tag` is specifically for a short flag sitting inline next
to other content, e.g. in a list row). Current usages: Expired (`TagArchivedBrush`) and Depleted
(`TagWarningBrush`) on `Pass` rows in `StudentsView.axaml`'s Passes panel and `PassDetailView.axaml`,
plus the ARCHIVED/COMPLETED flag every master-list row draws via `RecordListItem` (see below).

```xml
<controls:Tag Color="{DynamicResource TagArchivedBrush}" Label="EXPIRED" IsVisible="{Binding IsExpired}" />
```

**Status flags in a list row go through `Tag`, not hand-rolled small coloured text.** All five master
lists previously drew their own `FontSize="11"` + `StatusArchivedBrush` line (and Terms used a bare
`Opacity="0.6"` for the equivalent "Completed" flag) — that is what `RecordListItem`'s
`FlagLabel`/`IsFlagged`/`FlagColor` now standardise.

## Composed layout controls

Four structures were each copied 5–14× across the app before being extracted. Reach for these rather
than re-typing the shape; a page that hand-rolls one of them will drift, which is exactly what happened
to the dialog button row (two of its fourteen copies ended up with a 12px top margin against the
others' 16).

**`Controls/FormField.cs`** — one labelled input. Beyond the layout, it sets
`AutomationProperties.LabeledBy` from the input to its own label, which is the part markup can't
express: a loose `TextBlock` beside a `TextBox` is not a label as far as a screen reader is concerned.
Use it for **every** form input; ~25 sites previously had no label association at all.
```xml
<controls:FormField Label="First name">
    <TextBox Text="{Binding FirstName}" />
</controls:FormField>
```

**`Controls/DialogButtonRow`** — the right-aligned Cancel/primary pair that ends every dialog. It takes
only labels: both commands come from the inherited DataContext (always an `IDialogViewModel`), Cancel
from `CancelCommand` and the accent button from `DefaultCommand`. Binding the accent button to
`DefaultCommand` is deliberate — it's the same command `DialogHost` runs on Enter, so "the primary
button" and "what Enter does" can't drift apart.
```xml
<controls:DialogButtonRow ConfirmLabel="Save" />
<controls:DialogButtonRow ConfirmLabel="Use existing" CancelLabel="Create new instead" />
```

**`Controls/DetailHeader`** — a detail pane's heading plus its primary action and overflow menu on one
row (see "Page layout convention" below for why they share a row). `Title` trims rather than pushing
the buttons off the edge. `Actions` is a plain control slot — each page puts its own
`Button Classes="Overflow"` and `MenuFlyout` there, since the items differ per page.
Set `PrimaryActionIcon` (a `MaterialIconKind`, `Pencil` for every Edit) to make the primary action an
icon-only `Button.Icon`, with `PrimaryActionLabel` ("Edit", "Edit roll") as its tooltip and accessible
name. Leave it unset for an action with no clear icon ("Mark new roll"), which keeps a text label.

**`Controls/RecordListItem`** — one row in a `MasterDetailView` master list: `Title`, optional
`Subtitle`, optional status flag (`FlagLabel`/`IsFlagged`/`FlagColor`, rendered as a `Tag`). Titles and
subtitles trim; the master column is at most 320px (never wider than the details pane) and nothing in the app set `TextTrimming` before
(Avalonia's default is `None`, so long names and emails simply clipped).

**`Controls/PersonSearchBox`** + **`ViewModels/Shared/PersonSearchViewModel<TPerson>`**: the app's one
"find a person by name" component, used in three places:

- the Mark Roll window's "Add a student" panel (`StudentSearchViewModel`, active students only);
- `StudentPickerViewModel` (the Dashboard's Add pass / Search student, also students only);
- `ContactPickerViewModel` (adding an emergency contact: any active person, since a contact needn't
  be a student; the student and their existing contacts are left out).

Bind the control's `DataContext` to the search view model, which owns the 250ms-debounced search, the
"Type a name…" / "No … match…" empty text and the keyboard contract: **Enter in the box chooses the
match only when the search found exactly one person; Enter or a double-click on a row chooses that
row**. With several matches, Enter in the box does nothing: picking the first of two "Sarah"s was too
easy to get wrong. Its `onChosen` callback decides what
choosing means (add to roll, or close the picker). Each result shows the name with phone and email
underneath (each hidden when missing), so two people with the same name can be told apart.

The per-row action button is optional. Mark Roll shows it (`ActionLabel="Add to roll"
ActionIcon="Plus"`), and the picker dialogs turn it off (`ShowRowAction="False"`), since there the
dialog's Select button, Enter, or a double-click does the choosing.

**Whenever a flow needs the user to pick a student or a person, use this** (directly, or via one of the
pickers as a dialog), not `IdentityPickerViewModel`, so finding someone works the same everywhere. In a
dialog, the picker sets `DefaultCommand` to the search's own Enter command, so `DialogHost`'s Enter and
the box's KeyBinding are one action (`StudentPickerViewModelTests` presses a real Enter to prove it
fires once; `PersonSearchTests` covers the contact picker, the row-button rule and double-click).

## Global control styles (`Styles/Controls.axaml`)

`Button`, `TextBox`, `ComboBox`, `CalendarDatePicker`, `NumericUpDown` all get `RadiusSmall` corners
automatically — don't set `CornerRadius` on individual instances of these. `Button` and
`ListBoxItem` get a 120ms `PremultipliedBrushTransition` on `Background` (hover/pressed/selected
feedback) for free. It's a custom transition (`Styles/PremultipliedBrushTransition.cs`), **not**
Avalonia's `BrushTransition`. `BrushTransition` blends R/G/B/A independently (straight alpha), so going
from a translucent white/black hover or pressed tint (or from no background, i.e. transparent white) to
the opaque selection colour dragged the colour through white/black mid-way, and the selection flashed
grey each time it changed. Blending premultiplied colour is a true cross-fade of what's on screen, so no
frame can render outside its two ends (`PremultipliedBrushTransitionTests`). Never use a plain
`BrushTransition` for a `Background`.

**Gotcha this relies on**: FluentTheme's `:pointerover`/`:pressed`/`:selected` states repaint an
internal template part (`Button`/`ListBoxItem`'s own `/template/ ContentPresenter#PART_ContentPresenter`)
directly, not the control's own `Background` property — a `Transitions` setter on
`Button`/`ListBoxItem` alone does nothing for hover/selected, only for changes made directly to that
control's own `Background` (like our custom `Button.NavItem.Selected` class). That's why
`Styles/Controls.axaml` also has `Style Selector="Button /template/ ContentPresenter"` and
`"ListBoxItem /template/ ContentPresenter"` rules carrying their own matching `Transitions` — **do
this same `/template/`-part targeting for any new hover/selected-reactive style**, not just a
`Transitions` setter on the control itself, or the animation will silently no-op.

### The interactive-state invariant

> **A background transition may only be attached where the app owns an `Opacity=1` brush for every
> state at both ends of the change**, and it must be a `PremultipliedBrushTransition` (above), never a
> plain `BrushTransition`.

Exactly two control types carry a background transition — `Button` and `ListBoxItem` — and both have
a complete app-owned palette in `Styles/Tokens.axaml` ("Interactive state palette"). If you give a
third control type an animated `Background`, give it a full state set from that palette in the same
change. **You do not need to do anything for a new *button*** — the rules are on the bare `Button`
selector, so a new one is correct by default, including `Classes="accent"`.

`InteractiveStateTests` enforces this, so a missed state fails the build rather than being noticed as
a flicker months later.

**Why `Opacity=1` specifically** (measured via a probe against the real theme, not inferred): Avalonia's
`SolidColorBrushAnimator` interpolates a brush's `Color` and its `Opacity` as two *independent*
factors, and they multiply at render time. FluentTheme's `ButtonBackgroundPointerOver` is
`Color=Black, Opacity=0.1`, while `ButtonBackground` is `Color=#33000000, Opacity=1`. Animating
between them runs alpha `0x33→0xFF` while opacity runs `1.0→0.1`, so the midpoint renders at ~0.33
alpha — **darker than either endpoint** — then settles back. That overshoot is the "flashes grey then
corrects itself" symptom. Keep the opacity at 1 and put any translucency in the colour's alpha
channel, where interpolation is monotonic and cannot overshoot.

Two corollaries worth knowing:
- **The `Button /template/ ContentPresenter` selector reaches further than the app's own markup.** It
  matches any `ContentPresenter` whose templated parent is a `Button`, including framework-internal
  ones — `CalendarDatePicker`'s drop-down glyph, `TextBox`'s clear button, `TimePicker`'s flyout
  button, `Calendar`'s nav arrows. Those receive the transition whether or not we styled them, which
  is precisely why the state rules sit on the bare `Button` selector rather than per-class.
- **`System*Color` keys are `Color`s, not brushes, and they belong to FluentTheme.** Assigning one to
  a brush property works via implicit coercion (which yields `Opacity=1` — that is why the older
  per-class overrides happened to be flash-free), but the app's palette wraps them in explicit
  `SolidColorBrush` resources so the invariant is visible and testable. The accent-derived entries
  follow the **OS accent colour**, so selection colours legitimately differ per machine.

`Border.Card` (`Classes="Card"`) is the standard "grouped content" surface: `RadiusMedium` corners,
a subtle border, `SystemAltHighColor` background, 16px padding. Used for the master-list pane in
`MasterDetailView`, the list in `LinkedRecordsPanel`, and the Dashboard stat tiles (which override
`Background` locally to `AccentSurfaceBrush` while keeping the Card's corner/border/padding). Reach for
`Classes="Card"` before hand-rolling a new bordered container.

**`Controls/FillWrapPanel.cs`** packs children into rows by each child's own `MinWidth` (stretching
every child in a row to share the available width equally, unlike a plain `WrapPanel`, which shrinks
to content), stacking to full-width rows once there's no longer room for two at their `MinWidth`. It
adapts to whether its own available height is bounded or not: unbounded (e.g. inside a page's outer
`ScrollViewer` — the Student page's Emergency-contacts/Health-concerns panels) sizes each row to its
own content, since there's no meaningful height to "fill"; bounded (e.g. inside a fixed-size `Window`
row — `MarkRollWindow`'s search-results/attendees columns) stretches every row to share the full
available height equally, so a `Grid`'s `*` row (or similar stretch-to-fill content) inside it actually
fills the space instead of arranging at whatever smaller size it happened to report from `Measure`.

**`Button.Overflow` (`Classes="Overflow"`)** is the icon-only trigger for a `MenuFlyout` of
secondary/item actions — the `...` button pattern used for per-row item actions (e.g.
`LinkedRecordsPanel`'s per-row Edit/Remove menu) and for infrequently-used actions in a detail
toolbar (e.g. `TermsView`'s "Delete", `ClassSchedulesView`'s "Edit"/"Archive"), so only each screen's single
primary action (the `accent`-styled button — see "Dialogs" below) stays directly visible. It sets
chrome (36x36, centered content, default `Button` background so it still reads as clickable) **and**
supplies the dots icon, `AutomationProperties.Name` and `ToolTip.Tip` as defaults, so a call site is
just the flyout:
```xml
<Button Classes="Overflow">
    <Button.Flyout>
        <MenuFlyout Placement="BottomEdgeAlignedRight">
            <MenuItem Header="..." Command="{Binding ...}" />
        </MenuFlyout>
    </Button.Flyout>
</Button>
```
The icon comes from a `<Setter Property="Content"><Template>…</Template></Setter>` rather than a plain
value — a `Template` setter builds a *new* icon per button instead of sharing one control across the
whole app, which is what makes this expressible as a style at all. A site that needs a different icon
or a row-specific accessible name (`MarkRollWindow`'s Plus/Close buttons, `LinkedRecordsPanel`'s
per-row menu) sets its own locally, and a local value wins over a style setter.
(`xmlns:materialIcons="clr-namespace:Material.Icons.Avalonia;assembly=Material.Icons.Avalonia"` on
the view/control root — same URI `MainWindow.axaml` uses for nav icons.) Don't build a new
templated control for this — a shared `Classes="Overflow"` style plus a per-site `MenuFlyout` is
enough since there's no shared item data model across call sites.

**36 square** (`ControlHeight`, see "Design tokens" above) rather than a full 40: 40 was under
consideration for the comfortable-target guidance, but a 40px square next to an ordinary 36-tall
`Button` would read as an oversized, mismatched control.

**`Button.Icon` (`Classes="Icon"`)**: Add and Edit buttons are **icon-only**: `Plus` for Add (each
page's "Add student"/"Add class"/…, `LinkedRecordsPanel`'s Add) and `Pencil` for Edit (via
`DetailHeader.PrimaryActionIcon`, above). The style is chrome only: the same 36 square as `Overflow`,
padding 0, centred content, and **no background override**, so `Classes="accent Icon"` keeps the
accent palette (and the interactive-state invariant needs nothing new). The call site supplies the
icon plus the old text as both name and tooltip:
```xml
<Button Classes="accent Icon" Command="{Binding AddStudentCommand}"
        AutomationProperties.Name="Add student" ToolTip.Tip="Add student">
    <materialIcons:MaterialIcon Kind="Plus" Width="18" Height="18" />
</Button>
```
Tests should find these buttons by `AutomationProperties.GetName`, not by `Content` text (see
`ViewSmokeTests.ClassSchedulesView_HasAnAddClassButton_BoundToAddClassCommand`).

**The Dashboard header is the studio banner** (`Assets/tracygrayyoga_header.jpg`, from the MAUI
app), not a text title: an `Image` (`Stretch="Uniform"`, `MaxHeight="200"`) on a white rounded
`Border`, white in both themes because that's the background the artwork was made for. Clicking it is
the noot noot easter egg (`NootNootEasterEgg`, wired via `Tapped` in `DashboardView.axaml.cs`). It's a
plain `Border` with `Cursor="Hand"` rather than a `Button` on purpose: button chrome would paint
hover/pressed backgrounds over the artwork and give the surprise away. Its accessible name is the
studio's name. The window/app icon is `Assets/appicon.ico`, rendered from the MAUI app's
`appicon.svg`; don't reintroduce `avalonia-logo.ico`.

**Exception: the Dashboard's quick actions** ("Add student", "Add pass", "Search student") are
`Button.Tile`s (below) with a visible text label under the icon. Unlabelled icons on a landing page
would leave the instructor guessing which is which; the icon-only rule is for Add/Edit buttons that
sit next to the thing they act on (a page list, a detail header, a linked list), where context
already says what's being added or edited.

**`Button.Tile` (`Classes="Tile"`)**: a square (`TileSize`, 136) action tile: a large icon
(`TileIconSize`, 36) centred at the top, centred text below. Used for the Dashboard's quick actions
and its class-roll tiles: Today's classes (every active schedule for today's weekday: its roll if one
exists, else a new unsaved one, so a class shows before it's been marked) use `ClipboardCheckOutline` ("Mark roll"), the last 7 days'
use `ClipboardEditOutline` ("Edit roll") plus the date. Tiles sit in a `WrapPanel` (an
`ItemsControl`'s `ItemsPanel` for a list) with `Margin="0,0,12,12"` each, so they run left to right and
wrap. The content is a `StackPanel Classes="TileContent"`, an icon and then `TextBlock`s; the style
sizes and centres the icon and centres/wraps the text, so don't set those per tile. When the text
names the subject rather than the action (a class tile shows the class, not "Mark roll"), set the
action as `ToolTip.Tip` and as the start of `AutomationProperties.Name` ("Mark roll: Monday: 09:00").
Background and interactive states are the plain `Button`'s. A class tile's title is a
`TextBlock Classes="TileTitle"` (`TileTitleFontSize`, 11, Medium, no wrapping) inside a
`Viewbox StretchDirection="DownOnly"`: it must stay on one line, and 11px fits the worst case
`ClassSchedule.ToString()` can produce ("Wednesday: 12:00 pm"), while the Viewbox only ever shrinks it
if a font renders wider than expected. Class times read "9:00 am" everywhere: `ClassSchedule.ToString()`/
`FormatTime` use 12-hour time with lowercase am/pm, not the machine's culture.

`TextBlock.ValidationError` (`Classes="ValidationError"`) sets `Foreground` to `StatusErrorBrush`,
wraps, carries `GroupGapMargin`, and fades in via an `Opacity` transition — use this for dialog
validation-error text instead of `Foreground="Red"`, and don't re-set `TextWrapping`/`Margin` at the
call site (all five previously did, verbatim).

**`Button.RecordLink` (`Classes="RecordLink"`)** is how a detail pane links to a related record: the
record's name *is* the button, with a `LinkVariant` icon in front. Use it instead of a plain name
followed by a separate "View"/"View student" button (the old Identities-page shape). The call site
sets just the text, the command and a tooltip; the icon comes from the style's `ContentTemplate`:
```xml
<Button Classes="RecordLink" ToolTip.Tip="Open student"
        Content="{Binding FullName}"
        Command="{Binding DataContext.NavigateToStudentCommand, ElementName=Root}"
        CommandParameter="{Binding}" />
```
`Content` must be the display string: the template renders it with `{Binding}`, and a string
`Content` also gives the button its accessible name. It's left-aligned so it sizes to the name
rather than stretching across a card. All of its chrome and states come from the base `Button`
rules, so it adds nothing to the interactive-state invariant.

## Scroll edge shadow (`Behaviors/ScrollEdgeShadow.cs`)

**Layering:** the shadow bars draw into the nearest `AdornerLayer`. A window's own top-level layer
paints above *all* its regular content, including the `DialogHost` overlay, so on their own the shadows
showed through open dialogs. Every window that hosts a `DialogHost` (`MainWindow`, `MarkRollWindow`)
wraps its page content, but **not** the `DialogHost`, in a `<VisualLayerManager>`. That gives the
page's shadows (and its other adorners) a layer painted underneath the dialog overlay, while shadows
inside a dialog still use the window's top layer. A new window with a `DialogHost` needs the same
wrapper; `ScrollEdgeShadowLayeringTests` checks Mark Roll's.

Any scrollable area should hint that there's more content above/below with a short top/bottom
fade, and that hint disappears once there's nothing left to reveal in that direction. **The fade is the
colour of the surface the list sits on**, not black: the nearest fully opaque solid background above
the `ScrollViewer` (a Card, the page, the window), re-resolved when the theme changes, falling back to
`SystemAltHighColor` (`ScrollEdgeShadow.FadeColorFor`, `ScrollEdgeFadeColorTests`). It used to be
hard-coded black, a dark smudge over light-theme cards. Opt in with one attribute; don't hand-roll an
overlay per call site:

```xml
<ScrollViewer behaviors:ScrollEdgeShadow.IsEnabled="True"> ... </ScrollViewer>
<ListBox behaviors:ScrollEdgeShadow.IsEnabled="True" ItemsSource="..." />
```
(`xmlns:behaviors="using:YogaClassManager.Avalonia.Behaviors"` on the view/control root.)

Works on a plain `ScrollViewer` directly, or on a `ListBox` (it finds the Fluent-theme template's
`PART_ScrollViewer` itself). Each edge gets its own thin gradient `Border` (dark near the edge, fading
to transparent over its own height) mounted in the nearest `AdornerLayer` — every `TopLevel` already
carries one via its `VisualLayerManager`, so this needs no extra XAML scaffolding — rather than being
inserted into the `ScrollViewer`'s own visual tree. That's what makes it a drop-*shadow* (a dark tint
cast over the content) instead of the previous approach of fading the content's own alpha via
`OpacityMask`, which faded the content to whatever was behind it rather than shadowing it. The overlay
`Panel` is `IsHitTestVisible="False"` and lives in a separate layer above the page, so it never
intercepts scroll/click input — see `ShadowOverlay` in `ScrollEdgeShadow.cs` if you need to change how
it mounts or resizes.

**Growth curve**: each edge's shadow height is a pure function of how far the current scroll offset
has moved away from *that* edge specifically — 0px flush at the edge, easing up towards
`MaxShadowHeight` (default 16px) the further scrolling gets from it, via `height = maxHeight * (1 -
e^(-distance / growthDistance))` (`GrowthDistance`, default 60px, is roughly "how many px of scroll it
takes to reach ~63% of the max"). This is independent per edge — a shadow's height only depends on
distance from its own edge, not on whether the opposite edge still has content — and it naturally goes
to 0 with nothing left to reveal in that direction, and to 0 on both edges when the content fits
entirely (no special-casing needed, since both distances collapse to 0 in that case). Override either
value per instance (e.g. `MaxShadowHeight="12"` on a directly-authored `ScrollViewer`) if a shallower
or faster-growing shadow suits a specific panel better; the pure `(extent, viewport, offset, maxHeight,
growthDistance) → state` math lives in the public `ScrollEdgeShadowMath.Compute` so it's unit-tested in
`YogaClassManager.Avalonia.Tests` without a live control — extend those tests if you change the curve.

Don't add this to a scrollable area unless the content can actually overflow. (The Dashboard now
scrolls as one `ScrollViewer` around its stats, quick-action tiles and two sections of class-roll
tiles, with the shadow enabled; the tile sections themselves aren't separately scrollable.)

**Exception — lists that already live inside one of these shadowed `ScrollViewer`s**: give them no
fixed `Height` at all, don't nest a second scroll region. `StudentsView.axaml`'s student detail pane
is one big `ScrollViewer behaviors:ScrollEdgeShadow.IsEnabled="True"` around everything (record
details, Emergency contacts/Health concerns, Passes, attendance) — none of those inner
`LinkedRecordsPanel`s carry a `Height`, so each grows to fit its own content and the single outer
`ScrollViewer` handles all the scrolling. (`LinkedRecordsPanel` also centres each row's content
vertically against its 36px `...` button, so a one-line row doesn't sit high. An `ItemTemplate`
doesn't need to add its own vertical centring.) This is a deliberate exception to the general "fixed pixel
values rather than auto-measuring" convention used elsewhere for genuinely bounded small sets (e.g.
`FilterBar`'s advanced-filter panel, capped at 200 to fit an animated reveal) — those aren't sitting
inside another scroll region, so growing them unbounded isn't an option. If a list is already nested
inside a page-level shadowed `ScrollViewer`, prefer letting it grow over giving it its own fixed height
and a second, redundant inner scrollbar.

**Scrollbars reserve space rather than overlay content** — `Styles/Controls.axaml` sets
`ScrollViewer.AllowAutoHide="False"` globally (including inside a `ListBox`'s/`ComboBox`'s own
template, same as the Button rules above), so a visible scrollbar pushes the content over instead of
floating on top of it. This is layout, not visibility: a `ScrollViewer` with nothing to scroll still
collapses its bar to zero width/height and the content gets the full pane back, since that part is
`ScrollBarVisibility="Auto"`'s job, not `AllowAutoHide`'s — don't reach for `AllowAutoHide="True"`
locally to "restore" the collapse-when-empty behavior, it was never what provided it.

## Navigation shell

`Views/MainWindow.axaml` is an Avalonia `SplitView` (`DisplayMode="CompactInline"`,
`OpenPaneLength="220"`, `CompactPaneLength="56"`) driven by `MainWindowViewModel`:

- `NavItems` (`ObservableCollection<NavItemViewModel>`) holds the main nav entries (Dashboard,
  Identities, Students, Class Schedules, Class Rolls, Terms). `SettingsNavItem` is separate and docked to the bottom of the
  pane (`DockPanel.Dock="Bottom"`) so it stays pinned regardless of how many main items exist.
- Both the `ItemsControl` (main items) and the `ContentControl` (Settings) share one
  `DataTemplate` — `NavItemTemplate`, keyed in `MainWindow.axaml`'s `Window.Resources` — so there is
  exactly one place that defines what a nav row looks like.
- `IsPaneOpen` (bool, toggled by `ToggleNavCommand`) drives the `SplitView`. Nav-row labels bind
  their `IsVisible` to `{Binding $parent[SplitView].IsPaneOpen}` (reading the ancestor control
  directly, not round-tripping through the ViewModel) so icon-only mode collapses cleanly.
- Selection highlighting (`Button.NavItem.Selected`, defined in `Styles/Controls.axaml` with the rest
  of the interactive palette) is driven by `NavItemViewModel.IsSelected`, kept in sync by
  `MainWindowViewModel.UpdateSelection`, which subscribes to `Router.CurrentViewModel` and compares
  `UrlPathSegment`.
- Every nav row (including the collapse toggle) has both `ToolTip.Tip` and
  `AutomationProperties.Name` set to its label — required because icon-only collapsed mode removes
  the visible text.
- Collapsed-mode icon centering is deliberately **not** a state-dependent style switch. An earlier
  version toggled `HorizontalContentAlignment`/`Padding` via a `Classes.Collapsed` binding on
  `IsPaneOpen`, but that class flips *instantly* while `SplitView`'s pane-width animates over
  ~200ms, so the button's padding/alignment snapped to its collapsed values while the pane was still
  visually wide — a visible jump, not a smooth transition. Instead, `Button.NavItem`'s `Padding` is
  one constant value (`10`) chosen so it exactly centers the 20px icon at the *collapsed* width
  (`CompactPaneLength` 56 − the `ItemsControl`'s 8+8 margin = 40px available, `(40-20)/2 = 10`) with
  `HorizontalContentAlignment` staying `"Left"` throughout — at the open width there's plenty of
  slack so the same constant padding still reads correctly. **Don't reintroduce a
  collapsed/expanded style switch for anything that has to track the pane's width** — if a future
  change needs state-dependent visuals here, drive them from a property that actually animates in
  step with the pane (not a discrete bool), or accept a one-frame snap consciously.
- The pane header is just the hamburger toggle: a `Button.NavItem` with `HorizontalAlignment="Left"`
  and `Margin="8"`, so it sits at the same fixed left position as the nav items' icons below it and
  never moves as the pane animates. There's deliberately **no product-name title** beside it (the
  Dashboard banner and the window title carry the name). Don't centre the toggle: centring was tried
  and reverted, because it unpinned the button from the nav items' icon column.

**Cross-page navigation goes through `Services/IPageNavigator`**, not `Locator.Current.GetService<T>()`
in a command body. `ToStudent`/`ToIdentity`/`ToClassRoll` resolve the target page and carry a
"select this row when you get there" hand-off, which the arriving page consumes with the matching
`TakePending*` (reading it clears it, so it applies exactly once). The hand-off exists because page
ViewModels are transient — a fresh instance is built on every navigation — so the selection can't
simply be set on the target before navigating. Two things this buys, both of which were problems
before: pages no longer reference each other's ViewModel types (Students and Identities were mutually
coupled), and a page ViewModel can be constructed in a test against a plain `IScreen` with no Splat
container, where it previously threw `InvalidCastException` on an `(AppScreen)hostScreen` downcast.

**To add a new top-level page**, follow the existing pattern: register the ViewModel/View pair in
`App.axaml.cs` (`ConfigureServices`) as already done for the six existing pages, give the ViewModel
a `UrlPathSegment`, then add one `CreateNavItem(label, icon, urlPathSegment, resolver)` call in
`MainWindowViewModel`'s constructor (or, if it belongs at the bottom like Settings, expose it as its
own property and dock it in XAML). Pick a `MaterialIconKind` that reads clearly at 20x20 in
icon-only mode — check it renders recognizably before committing to it.

## Page layout convention

Every routed page (`Views/{Dashboard,Identities,Students,ClassSchedules,ClassRolls,Terms,Settings}/*View.axaml`) follows
the same skeleton: a root `Grid`/`StackPanel` with `Margin="{StaticResource PagePadding}"`, a
`Classes="PageTitle"` header, then page content. New top-level pages should match this shape rather
than introducing a new layout pattern.

**Detail-pane headings carry their own actions, not a separate row above them** — use
`Controls/DetailHeader` (see "Composed layout controls"), which is that shape: heading in a `*` column,
primary action button + `Button.Overflow` menu in the `Auto` column beside it, rather than a separate
`HorizontalAlignment="Right"` button row sitting above the heading. This reads as "the heading owns
these actions" instead of a free-floating toolbar, and keeps the same actions available
(Edit/Archive/Unarchive/etc. via the overflow menu, plus each page's one primary button). All five
detail panes use it; new ones should too, rather than reintroducing a standalone action row or
re-typing the grid.

**Master-list rows use `Controls/RecordListItem`**, likewise — all five previously hand-rolled the same
title/caption/flag stack.

## Sort and filter controls — the standard pattern (`Controls/SortByButton.axaml`, `Controls/FilterButton.axaml`, `Controls/FilterBar.axaml`)

**Whenever a repository-backed list has sortable and/or filterable fields, expose them with these
controls — not inline `ComboBox`/`CheckBox` rows sitting directly in the view.** This is the standard,
not a Students/Identities-page-specific convention: `SortByButton` and `FilterButton` are generic,
reusable `Controls/` components with no page-specific knowledge, used directly by any list that needs
sort and/or filter (e.g. the Student page's Passes list uses a standalone `FilterButton`, no search box
involved), and composed together by `FilterBar` for pages that also want a search box/basic filters
(Identities, Students' own top-level list). Reach for whichever fits: `FilterBar` when there's a search
box, `SortByButton`/`FilterButton` directly otherwise.

**Omit `SortByButton` if the filter's sort enum has no field beyond its internal fallback key** — e.g.
today's `PassSortOptions` is `Id`-only (no user-facing sortable field yet), so the Passes list's
`FilterButton` has no accompanying `SortByButton`. Add one back as soon as the repository model gains a
real sortable field.

Both buttons are dropdown-styled: a `Button` with a `Flyout` (plain `Flyout`, not `MenuFlyout` — its
`Content` can be arbitrary controls, unlike a `MenuFlyout`'s item list), placed
`Placement="BottomEdgeAlignedRight"` so the flyout's right edge aligns with the button's right edge
(same placement mode `LinkedRecordsPanel`'s `...` menu already uses) — Avalonia's popup placement
already flips it above the button when there's no room below, so neither control does any manual
"show above/below" positioning itself.

**`SortByButton`**: content is an up/down `MaterialIcon` reflecting `SelectedSortOrder`, then
the field name: no "Sort by:" prefix (the flyout's heading and `SortDescription`, the
tooltip/accessible name "Sort by Last Attendance, ascending", still say it in full). The field label is the sort enum member run through
`Converters/EnumDisplayTextConverter.cs` (Capital Case, a space before each capital: `LastAttendance` →
"Last Attendance"), applied to the button face, the dropdown items and `SortDescription` alike, so name
sort enum members as their display words and don't add a separate label mapping. The arrow icons reflect
`SelectedSortOrder` (`Converters/EnumEqualsConverter.cs`, a two-way "does this value equal this fixed
option" check — `ConverterParameter` is the already-boxed enum value, e.g. `{x:Static data:Order.Ascending}`,
not a string to parse, so it works for any enum without the converter needing to know its type up
front). Its `Flyout` holds the field `ComboBox` (`SortOptions`/`SelectedSort` — enum-backed, same
`Enum.GetValues<T>()` pattern as `ClassSchedulesViewModel.RollSortByOptions`, **excluding** whatever internal
fallback sort key the enum carries, e.g. `IdentitySortOptions.Id`/`StudentSortOptions.Id` — those exist
for the repository's default-sort fallback, not a user-facing choice, so ViewModels filter them out of
`SortByOptions` rather than removing them from the enum) plus an Ascending/Descending `RadioButton`
pair (also driven by `EnumEqualsConverter`). The two `RadioButton`s deliberately carry **no** explicit
`GroupName` — Avalonia groups same-parent `RadioButton`s by default, which already scopes the pair
correctly per `SortByButton` instance; a literal `GroupName` string would risk cross-grouping if two
instances were ever visible at once (a real possibility now that this is a standalone, multi-instance
control, unlike when this markup lived only inside the single page-level `FilterBar`). The whole button
is hidden when `SortOptions` is null; the radio pair inside the flyout is separately hidden when
`SortOrderOptions` is null, for a list that sorts by a fixed field with no direction — in practice every
current usage supplies both.

**`FilterButton`**: shows just a `FilterVariant` icon when `FilterCount` is 0, or
`"{icon} {FilterCount} filter(s)"` once it's positive (`Converters/FilterCountTextConverter.cs` — returns
null, not `"0 filters"`, when the count is 0, so the count `TextBlock`'s own `IsVisible` can key off its
own `Text` being null-or-empty via an `ElementName` self-reference + `StringConverters.IsNotNullOrEmpty`
rather than needing a second converter). Its `Flyout` holds a "Filters" title, the host's `Filters`
content, then a divider (`BorderThickness="0,1,0,0"`, `SystemBaseLowColor`) and a right-aligned "Clear
filters" button — **disabled** (via the same `FilterCountText`-is-empty check, not a separate bool) when
`FilterCount` is 0, so it doesn't sit there clickable with nothing to clear. **`FilterCount` and
`ClearFiltersCommand` are computed/implemented by the host, not `FilterButton` itself** — it has no way
to introspect an arbitrary supplied `Filters` control tree to count "how many filters are active" or
know how to reset them. The host exposes an `int XxxFilterCount` (summing whichever of its own filter
fields are non-default — mind the polarity: a checkbox that defaults to *checked* counts as "a filter
is active" when *un*checked, and one that defaults to *un*checked counts when checked — see
`StudentsViewModel.PassFilterCount`, where "Include expired" defaults on and "Include depleted" defaults
off (depleted passes are hidden by default), so the default view shows no badge. The same applies to
a non-null default value: `AttendanceFilterCount` treats "from" as active only when it differs from its
6-months-ago default, and `ClearAttendanceFiltersCommand` resets it to that default, not to null (Class
Schedules' `RollFilterCount`/`ClearRollFiltersCommand` do the same for its "Class rolls" list) — kept
current via `WhenAnyValue(...).Subscribe(_ => this.RaisePropertyChanged(nameof(XxxFilterCount)))` on
those same fields) and a `ClearXxxFiltersCommand` that resets them (which naturally re-triggers
whatever filter-reload `WhenAnyValue` chain already exists for those fields, same as any other
filter-field change). **A page's own list always calls it `ActiveFilterCount`/`ClearFiltersCommand`**;
a *child* list on the same page prefixes it (`PassFilterCount`/`ClearPassFiltersCommand` for Students'
Passes list, `RollFilterCount`/`ClearRollFiltersCommand` for Class Schedules' rolls list) — don't let
two unrelated filter groups collide on the same property name, and don't call a page's own count bare
`FilterCount`, which reads as the child-list form. Nothing enforces this at compile time: a missing or
misnamed implementation is a silently-empty badge, not a build error, so it's worth checking by eye
when adding a filterable list.

**Filter flyout content is one top-down column**: a `StackPanel Spacing="8" MinWidth="200"` holding
the fields in order. Each labelled input is a `StackPanel Spacing="4"` (label `TextBlock`, then the
control), and checkboxes sit directly in the column. Don't lay fields out side by side in a
`WrapPanel`/horizontal row (the Students page's filters once did and read left to right, unlike every
other page). See the Student page's Passes and Classes attended filters for the shape.

**Dates in a filter use `Controls/DateFilterField`**, not a bare `CalendarDatePicker`: it is the same
labelled-input shape (`Label`, then the picker) plus a clear (`Close` icon) button beside the picker,
because a `CalendarDatePicker` can't be emptied once a date is picked and a filter needs a way back to
"no date". The button is disabled, not hidden, while there's no date, so the picker's width doesn't
change; its accessible name is "Clear {Label}". `SelectedDate` binds two-way by default, through
`DateOnlyToDateTimeConverter`, exactly as the picker's own did:
`<controls:DateFilterField Label="From" SelectedDate="{Binding DateFrom, Converter={x:Static localConv:DateOnlyToDateTimeConverter.Instance}}" />`.
Clearing a date that has a non-null default (e.g. Classes attended's 6-months-ago "From") leaves it
empty, meaning "no limit", which counts as an active filter; "Clear filters" still restores the default.
Dates in dialogs (Term/Pass edit, Mark Roll) are required values and keep a plain picker.

`FilterBar` composes `SortByButton`/`FilterButton` plus a search box and an optional `BasicFilters` slot
(a `Control`-typed property, assigned via XAML property-element syntax, e.g.
`<controls:FilterBar.BasicFilters><CheckBox .../></controls:FilterBar.BasicFilters>`, not a
DataTemplate — each page supplies a small fixed set of already-instantiated controls, not per-item
templated content) — its own `SortOptions`/`SelectedSort`/etc. and `AdvancedFilters`/`FilterCount`/
`ClearFiltersCommand` properties just pass straight through to the two composed controls. Reserve
`BasicFilters` for controls a user reaches for often; anything used rarely (e.g. "Show archived")
belongs in `AdvancedFilters` instead — neither Identities nor Students currently populate `BasicFilters`
for this reason, so don't assume it's required.

**A page's main list keeps its search and action buttons inside the list's own card, above the
list**, not in a row above the card. Put them in `MasterDetailView.ListHeader`:

**A main list has one sort-and-filter button, not separate sort and filter buttons.** Sort lives
inside the filter popup: set `FilterButton`'s `SortOptions`/`SelectedSort`/`SortOrderOptions`/
`SelectedSortOrder` and its popup starts with a "Sort by" section (`Controls/SortOptionsPanel`) above
"Filters", while the button's tooltip and accessible name become "Sort and filter". That button sits
immediately left of the Add (`Plus`) button:

- **With a search box** (Students, Identities, Terms): a `FilterBar`, one row: search box, then the
  sort-and-filter button, then the list's Add button (pass it as `<controls:FilterBar.Action>`).
  `FilterBar`'s own `SortOptions`/… pass straight through to its `FilterButton`.
- **Without a search box** (Class Schedules, Class Rolls): a right-aligned horizontal `StackPanel`
  (`Spacing="{StaticResource SpacingSm}"`) of the sort-and-filter `FilterButton` and then Add (Class
  Rolls has no Add, so just the button).

`MasterDetailView` gives the header a `4,4,4,8` margin inside the card, so a page doesn't add its
own, and insets the list (rows, skeleton, empty text) by the same 4px on the left, right and bottom. The rows and the
selection highlight therefore line up exactly with the search box and buttons, both 8px in from the
card's edges, and the gap below the list matches the gap above the header (`MasterDetailLayoutTests`). Don't put a header row between the page title and the `MasterDetailView`. This applies to the
main (master) list only: the detail pane's sub-lists (`LinkedRecordsPanel`: Passes, Classes attended,
Emergency contacts, …) deliberately keep their title, a **separate** `SortByButton`, the
`FilterButton` and Add on one header line above their card (below). `SortByButton`'s popup is the same
`SortOptionsPanel`, so the two sort UIs can't drift apart.

In either popup, section headings ("Sort by", "Filters") are `TextBlock Classes="DialogTitle"
Margin="0"`, grouped with their content at `SpacingSm` (8px). Without `Margin="0"`, `DialogTitle`'s own
bottom margin adds to the spacing and leaves too big a gap under the heading.

`LinkedRecordsPanel` (a linked/child list, e.g. the Student page's Passes list, or the Class
Schedules page's "Class rolls" mini-list) is **not selectable**: its rows are an `ItemsControl` in a
`ScrollViewer`, not a `ListBox`, because every row action lives in the row's `...` menu and nothing acts
on a "selected" row, so a click-to-select highlight was meaningless. (Same for `MarkRollWindow`'s
Attendees list.) Rows reuse Fluent's `ListBoxItemPadding` so spacing matches the selectable lists. Use
a `ListBox` only where selection drives something: `MasterDetailView`'s master list, the picker dialogs
and Mark Roll's search results. It has native sort/filter support built in — the same
`SortOptions`/`SelectedSort`/`SortOrderOptions`/`SelectedSortOrder`/`Filters`/`FilterCount`/
`ClearFiltersCommand` properties `FilterBar` composes, rendered as a `SortByButton`+`FilterButton`
pair in its own header row (between the header text and the Add (`Plus`) button) rather than something a
caller has to assemble itself via `HeaderContent`. Set `SortOptions`/`SelectedSort` etc. directly on
the panel when the list has a real user-facing sort field (e.g. the Class Rolls mini-list's
`ClassRollSortOptions.Date/Time/DayOfWeek`); leave them unset to hide the `SortByButton` when it
doesn't (e.g. the Passes list, whose `PassSortOptions` is still `Id`-only per the carve-out above).
`HeaderContent` remains a general-purpose slot (rendered after the sort/filter buttons, before Add)
for anything that doesn't fit that shape. The row's "..." menu shows View/Edit/Remove for whichever of
`ViewCommand`/`EditCommand`/`RemoveCommand` are set, and **the "..." button is hidden entirely when none
are**: an empty menu used to open to nothing, which read as broken. Use `ViewLabel`/`EditLabel` when
"View"/"Edit" alone wouldn't say what happens (Emergency contacts' "View identity" opens the contact on
the People page; a class roll's attendees use "Change pass").

## Empty, loading, and error states

**A list that renders as a blank card is a bug, not a neutral default** — the user can't tell "nothing
here" from "still loading" from "the load failed". Both list controls take the text they should show
instead:

- **`MasterDetailView`** — `EmptyText` (shown when the list is empty *and* not busy), `ErrorText`
  (bind to the page's `LastError`; pins a `ValidationError`-styled line above the list), and `IsBusy`
  (bind to the page's `IsBusy`; shows an indeterminate `ProgressBar`). Also override `NoSelectionText`
  per page — the default "Select an item to view details." is a fallback, not a target.
- **`LinkedRecordsPanel`** — `EmptyText`. Opt-in: a panel without it renders nothing extra, so
  read-only history panels can stay bare if that genuinely reads better.

Both compute emptiness via `Controls/CollectionEmptinessWatcher.cs`, which follows
`INotifyCollectionChanged` — these lists are `ObservableCollection`s that get `Clear()`ed and refilled
in place on every refresh, so a one-shot count at bind time would go stale immediately. Reuse the
watcher rather than re-deriving emptiness if you add a third list control.

Write empty text that reflects *why* it's empty — "No students match the current search." beats "No
items", because on a filtered list the filter is usually the reason. Where "nothing yet" and "nothing
matched" are genuinely different situations, say which: the student search (`PersonSearchViewModel.SearchEmptyText`)
shows "Type a name to find a student." before anything is typed and "No students match the current
search." afterwards, because only one of those is a dead end.

**Empty-state copy follows one pattern**, so the lines read as a set:

- Start with "No {things}", in sentence case, with a full stop. Name the things as the screen does
  ("identities", the page's name; "class rolls"; "students").
- **The list can be narrowed** (search box and/or filters, including a filter with a non-empty default
  such as Classes attended's 6-months-ago "From"): "No {things} match the current search." / "…the
  current filters." / "…the current search and filters.", choosing the phrase by which controls the list
  has. Always "the current", never "this search" or "these filters".
- **Nothing has been recorded** (no search or filter): "No {things} {past participle}", without "are"/"were"
  ("No emergency contacts recorded.", "No students marked present for this roll."). Add "yet" only
  where more are expected to come ("No classes linked to this term yet.", "No students marked present
  yet." in Mark Roll); a student may never have health concerns, so that one has no "yet".
- No-selection prompts: "Select a {thing} to view its/their {details|rolls|attendees}."

| Where | Text |
|---|---|
| Students / Identities / Terms lists | No students / identities / terms match the current search and filters. |
| Class Schedules / Class Rolls lists | No class schedules / class rolls match the current filters. |
| Student: Passes, Classes attended | No passes match the current filters. / No classes match the current filters. |
| Student: Emergency contacts, Health concerns | No emergency contacts recorded. / No health concerns recorded. |
| Class schedule: Class rolls | No class rolls match the current filters. |
| Class roll: attendees | No students marked present for this roll. |
| Term: classes | No classes linked to this term yet. |
| Pass: usage | No classes attended with this pass yet. |
| Pass edit: alterations | No alterations recorded. |
| Mark Roll: attendees | No students marked present yet. |
| Student / identity / contact pickers | No students / identities / people match the current search. |
| Pass picker | No usable passes for this class. |
| Dashboard | No classes scheduled for today. / No classes in the last 7 days. |

Lists that aren't hosted by either control (the identity/pass pickers, Mark Roll's two columns) carry
their own empty line in the same shape — a `Caption`-styled `TextBlock` centred behind the list, so an
empty list stays the same size and the controls above it don't move.

## List page ViewModels (`ViewModels/Base/`)

`CollectionPageModelBase<T>` owns the pieces every list page would otherwise re-derive; use them rather
than hand-writing the equivalent chain:

- **`HasSelection`** — the `IObservable<bool>` to pass as `canExecute` for any command acting on the
  selected record.
- **`SortOrderOptions`** — the ascending/descending pair every `SortByButton` binds.
- **`RefreshWhenChanged(observable)`** — reload the list when a filter/sort property changes. It applies
  the mandatory `Skip(1)` (see `SearchableCollectionPageModelBase`'s own note): `WhenAnyValue` replays
  current values on subscription, and without it every page reloads redundantly right after
  construction, visibly resetting the selection.
- **`ReloadOn(trigger, load, whatFailed)`** — reload a selection-dependent *child* list (linked passes,
  emergency contacts, rolls). While any such load is in flight, `IsLoadingDetails` is true; bind it to
  each such `LinkedRecordsPanel`'s `IsLoading` (`IsLoading="{Binding DataContext.IsLoadingDetails,
  ElementName=Root}"`), which shows skeleton rows instead of the empty text while it loads.

**Loading content shows skeletons, not progress bars.** While data that has a known shape loads,
grey placeholders in that shape stand in for it, so a fast load turns "shape" into "content" rather
than flashing a bar:

- **Lists**: `Controls/SkeletonList` (`Rows`, `ShowSubtitle`): rows of grey bars padded like real
  list rows. `MasterDetailView` shows one in its master list while `IsBusy` and the list is empty;
  `LinkedRecordsPanel` shows one while `IsLoading` (`SkeletonRows`, default 3, set per panel to a
  typical record's count - the Student page uses 1 emergency contact, health concern and pass, 8 classes;
  `SkeletonShowsSubtitle="False"` for one-line lists like Health concerns) and hides its empty text
  meanwhile. **Only while the list is empty**: refreshing a list that already has rows (re-filtering,
  re-sorting) keeps the rows rather than swapping them for grey bars, which would bring the flicker back.
- **Anything else**: `Border Classes="Skeleton"` blocks sized like the real content. The Dashboard
  swaps each stat count and each class section's tiles for skeleton blocks while `IsBusy`, while the
  headings and quick actions show immediately; its "No classes…" lines wait until loading ends.

Skeletons are **static**: no shimmer or pulse, since a looping animation would break the ~200ms
ceiling under "Animation". `SkeletonBrush` (the theme's `SystemBaseLowColor`) fills them. A plain
`ProgressBar` remains only where there's no content shape to mimic: Mark Roll's pass lookup and the
database window.

**Loads are debounced** (`ViewModelBase.LoadDebounce`, 200ms). A page's first load waits it out with
its list skeleton showing, and is skipped if you've already moved to another page by then
(`DebounceLoadAsync`: the Dashboard does the same). A selection-triggered detail load
(`ReloadOn(..., debounce: true, clearNow: ...)` on Students, Class Schedules and People) clears its lists the
instant the selection changes, so the skeletons show at once, then waits. Each new selection cancels
the previous wait, so clicking quickly through records only fetches the one you stop on. Terms and
Class Rolls have nothing to fetch on selection (their details come with the list item), so only their
first load is debounced. A page's busy state is set synchronously inside the refresh, not from
`RefreshCommand.IsExecuting` (which arrives a UI-scheduler tick later and left the debounce with no
skeleton). While the main list loads with nothing selected yet, `MasterDetailView`'s details pane shows a
details skeleton (title, two lines, a card of rows) instead of its "Select a ..." prompt, since the first
record is about to be auto-selected. The prompt itself, once shown, is centred both ways in the pane.
The Identities page's Student / Emergency contact for cards get one placeholder card while the
links load, since which card will appear isn't known yet. **A skeleton must be exactly as tall as what
replaces it**, or the pane jumps when the data arrives, and fixed bar heights drift from the real
row heights (which come from font metrics and control padding). So each of that card's bars sits
centred over an invisible, inert copy of the control it stands in for (`Opacity="0"`, not focusable or
hit-testable, `AutomationProperties.AccessibilityView="Raw"`), which sizes the row
(`IdentityCardSpacingTests`). The bars themselves have **no `Height`**: they use
`VerticalAlignment="Stretch"` plus a vertical `Margin` to inset them, because a fixed-height bar taller
than its copy would size the row itself (a 28px bar over a 26px button made the card 2px too tall). Tests run with the debounce off
(`TestDefaults` module initializer); `LoadDebounceTests` turns it on in a non-parallel collection.

**Loading indicators need the UI thread free.** `IsBusy` (the main list's progress bar,
`MasterDetailView.IsBusy`) and `IsLoadingDetails` only help if the window can repaint while data loads.
`SqliteDataStore` runs every query, transaction and the startup migration on the thread pool (Microsoft.Data.Sqlite's
"async" calls are really synchronous; awaited directly from the UI thread they froze the window on every
page change). Keep it that way, and never block on a repository call from the UI thread (`.Result`,
`.Wait()`, `GetAwaiter().GetResult()`): the store resumes on the UI thread to release its gate, so that
deadlocks. `StoreThreadingTests` checks the store hands control back while a query is still running.

`ReloadOn` is worth reading before writing your own version, because two things about it are easy to
get wrong and both were wrong before:

1. It uses `Switch`, not `SelectMany`. `SelectMany` merges concurrent loads, so two quick selection
   changes each `Clear()` and refill the same collection and can interleave.
2. **`Switch` alone is not enough**, because these loads mutate their collection in place rather than
   returning a value — dropping a superseded load's *result* changes nothing, its writes still land.
   That's why `load` takes a `CancellationToken`: Rx cancels it when `Switch` drops the previous load,
   and each load is expected to pass it to its repository calls and re-check it before writing.
   Every `Core` repository/`IDbModel` method already accepts one.

Its error handling has the same shape of subtlety: the `Catch` sits *inside* the inner observable, not
on the outer `Subscribe`. An error reaching `Switch` terminates the whole outer sequence, so handling it
as the subscriber's `onError` would report the failure and still leave the pane permanently dead.

For a constructor-time load (a dialog's dropdown, the Dashboard's counts) use
`ViewModelBase.LoadOnCreate(load, onError)` rather than a bare `_ = LoadAsync()`, which discards the
failure as an unobserved `Task` exception and leaves an empty control with no explanation. A page
reports through a toast; a dialog puts the message on its own validation line, beside the empty control.

## Confirming destructive actions

**Anything that deletes, unlinks, archives, or discards data asks first**, via
`dialogService.ConfirmAsync(title, message, confirmLabel)` (`Services/DialogServiceExtensions.cs`,
wrapping the shared `ConfirmViewModel`). Keep the confirm label a verb that matches the action
("Delete", "Remove", "Unlink", "Archive"), not "OK".

Say what actually happens, especially when it differs from what the button implies:

- **Archive vs delete.** `IIdentityRepository.ArchiveOrDeleteAsync` hard-deletes an identity with no
  links and soft-archives one with links. Call `GetLinkageSummaryAsync` first and word the prompt for
  the branch that will actually run — don't make the user discover it from the toast afterwards. (A
  `Student` always counts as linked, so the Students page can promise "archive" outright.)
- **Side effects on other records.** Deleting a pass also clears it from every class roll entry that
  used it; the prompt says so.

Pair the confirmation with a toast on success, per the severity table below — a silent mutation and a
failed one look identical.

## Dialogs (`Views/Shared/*.axaml`)

Dialogs are **in-app overlays, not separate OS windows.** Each is a plain `UserControl` (no
`Title`/`SizeToContent` — those are `Window`-only) rendered by `Controls/DialogHost` — a shared
control (used by `MainWindow.axaml`, and by `MarkRollWindow.axaml` for its own local overlay) wrapping
a full-window `Border.DialogOverlay` (dimmed backdrop, fades in/out via the
`Classes.Open` + `DoubleTransition` pattern — same technique as
`TextBlock.ValidationError`, not a bool→double value converter) around a centered
`Border Classes="Card"` whose `ContentControl` shows the service's `ActiveDialog`. A host supplies
only the service:

```xml
<controls:DialogHost DialogService="{Binding Dialogs}" />
```

The dialog UserControl itself supplies no outer padding/border — the shared `Card` wrapper already does
(`Padding="16"`, `RadiusMedium`), so don't add `Margin="16"` to a dialog's root panel or it'll double
up. Convention for a new dialog:

```xml
<UserControl Width="...">
    <StackPanel Spacing="{StaticResource SpacingSm}">
        <TextBlock Text="{Binding Title}" Classes="DialogTitle" />

        <controls:FormField Label="Name">
            <TextBox Text="{Binding Name}" />
        </controls:FormField>

        <TextBlock Text="{Binding ValidationError}" Classes="ValidationError"
                   IsVisible="{Binding ValidationError, Converter={x:Static conv:ObjectConverters.IsNotNull}}" />

        <controls:DialogButtonRow ConfirmLabel="Save" />
    </StackPanel>
</UserControl>
```

Every input goes in a `FormField` and the button row is always `DialogButtonRow` — see "Composed
layout controls" above. A dialog taller than the Card's `MaxHeight` should wrap its own content
in a `ScrollViewer` (see `PassEditView.axaml`) rather than relying on the Card
to scroll — the Card sizes to its content, it doesn't scroll itself.

`FormField`'s label convention and a `DialogButtonRow`-styled button row are also reused outside actual
`DialogViewModelBase` dialogs where the same "labeled input(s) + a row of actions" shape applies — e.g.
`DatabaseGateWindow`'s file-selection buttons and the Settings page's database-file section (see the
Dialogs section's `DatabaseGateWindow` callout below, and `Views/Settings/SettingsView.axaml`). Their
reuse isn't strictly tied to the dialog-overlay pattern, just to the visual/accessibility convention
those two controls encode.

**Every dialog ViewModel derives from `DialogViewModelBase<TResult>`, which owns `CancelCommand`**
(previously re-declared byte-identically in all twelve) and exposes `DefaultCommand`. Set
`DefaultCommand` in the constructor to whatever the accent button does — `SaveCommand`,
`SelectCommand`, `ConfirmCommand`. Both are surfaced through the non-generic `IDialogViewModel`, which
is how `DialogHost` drives them without knowing each dialog's `TResult`.

### Keyboard behaviour (all of it lives in `DialogHost`)

Don't re-implement any of this per dialog:

- **Closing fades the whole card out, content intact.** The card renders `DialogHost.DisplayedDialog`,
  which holds the closing dialog through the overlay's 150ms fade-out. Rendering `ActiveDialog`
  directly emptied the card instantly, leaving a small empty square (just its padding and border)
  behind every Cancel/OK. A newly opened dialog replaces it immediately.
- **Escape** runs `CancelCommand`. **Clicking the dimmed backdrop does not close the dialog**: only
  choosing one of its actions does (or Escape, the keyboard Cancel), so a stray click outside can't
  throw away a half-filled form. The backdrop still swallows the click, so the page behind doesn't
  react. Don't reintroduce click-outside-to-close.
- **Enter** runs `DefaultCommand`, but only when its own `CanExecute` allows, so a half-filled form
  doesn't submit. A multi-line `TextBox` keeps Enter for itself; anything else with a claim on the key
  (`NumericUpDown` committing typed text, an open `ComboBox`/`Flyout`, a focused `Button`) has already
  marked the event handled, which is why the handler bubbles rather than tunnels.
- **Initial focus** goes to the dialog's first focusable control, **preferring a real input over a
  Button**. That's what removes the "click before you can type" problem, and it matters for Enter: a
  focused `Button` consumes Enter itself. A prompt with no fields therefore starts on Cancel — the safe
  action — so a single Enter can't confirm "delete this permanently". Confirming is deliberately
  Tab-then-Enter, or a click. **Don't "fix" this by focusing the accent button.**
- **Tab stays inside** the dialog: the card is `KeyboardNavigation.TabNavigation="Cycle"`, and the host
  additionally blanks tab navigation on its sibling content while a dialog is open. Focus is restored
  to wherever it was when the dialog closes.

`Services/IDialogService.cs`/`DialogService.cs` hold a small stack (not a single slot) of open
dialog ViewModels, so a dialog can open another dialog on top of itself (e.g. `PassEditView`'s Add
Alteration flow) — the nested one becomes `ActiveDialog`, and closing it reveals whatever was
underneath (focus follows it). A dialog ViewModel still just calls `Close(result)` to signal
completion; nothing in the dialog Views/`.axaml.cs` files handles closing directly.

**Exception: `MarkRollWindow` is a real, separate OS `Window`, not an overlay.** Marking a roll can run
for a while and the instructor often needs to check other pages without losing their place, so
`Services/IRollWindowService.cs`/`RollWindowService.cs` opens it via `Window.Show(owner)` (non-modal —
the rest of the app stays interactive) instead of `IDialogService.ShowDialogAsync`, and dedupes so the
same roll never has two windows open at once (`.Activate()`s the existing one instead). The dedup key
is `ClassRoll.Id` for a saved roll, falling back to `ClassSchedule.Id` + `Date` for one that hasn't
been saved yet — a brand-new roll is only INSERTed when the instructor saves it (see
`MarkRollViewModel`'s remarks), so every unsaved roll has `Id == 0` and keying on `Id` alone would
make them all collide with each other. Because the shared `Border.DialogOverlay` only exists inside `MainWindow.axaml`,
`MarkRollWindow.axaml` carries its own **local copy** of that overlay block, bound to
`MarkRollViewModel.Dialogs` — a private `IDialogService` instance that ViewModel constructs itself
(`new DialogService()`, no dependencies), not the shared app-wide singleton — so its own popups (the
pass picker, add-pass) render on `MarkRollWindow`, not on `MainWindow`. `MarkRollViewModel` is a plain
`ViewModelBase`, not a `DialogViewModelBase<TResult>` — a `Window` closes itself (a code-behind `Click`
handler calling `Close()`), it doesn't need the `RequestClose`/`TaskCompletionSource` machinery a
shared-overlay dialog needs to signal an awaiting caller. This is the one exception to "dialogs are
in-app overlays with a single shared host" in this app — don't use it as precedent for turning another
dialog into a `Window` without the same "runs long, user needs to do other things meanwhile" justification.

**Second exception: `Views/Bootstrap/DatabaseGateWindow` is also a real, separate OS `Window`, for the
opposite reason.** It's the startup gate `App.axaml.cs` shows before `MainWindow`/`DialogHost` exist at
all — no repository can be constructed until a database file is resolved (see
`Services/Database/IAppDataStoreProvider.cs`), so it structurally *cannot* be a `DialogHost` overlay
(there's no host to render into yet). Where `MarkRollWindow` is deliberately **non-modal** (the rest of
the app stays usable while it's open), `DatabaseGateWindow` is the **only** window until a database is
bound: there's nothing behind it to cancel back to, so it has no `DialogViewModelBase<TResult>`/
`CancelCommand`, and **closing it quits the app** (it's `desktop.MainWindow` under
`ShutdownMode.OnMainWindowClose`). Never cancel its `Closing` to "force" a choice — that's what an earlier
version did, and it left no way to quit (title-bar close, Cmd+Q and Dock > Quit all silently refused).
Quitting is what keeps the app from running without a database; `App.axaml.cs`'s hand-off checks
`IsClosed` so a file that finishes opening after the user quit doesn't open `MainWindow`. Don't treat either of these two exceptions as precedent for a third —
they cover opposite, narrowly-justified structural needs, not a general "some dialogs can be windows"
allowance.

## Toasts (`Services/IToastService.cs`)

Transient info/warning/error feedback ("Added John Smith.", "Can't delete X - it still has linked
classes.") — this replaced the old per-page `StatusMessage` text line under the search box.
ViewModels call `toastService.ShowInfo/ShowWarning/ShowError(message)`; don't add a new
`StatusMessage`-style bound `TextBlock` for this again. Severity choice:
- **Info** — the action fully succeeded (`Added X.`, `Saved X.`, archive confirmations).
- **Warning** — the action was blocked but nothing broke (`Can't delete X - it still has linked
  classes.`).
- **Error** — a caught exception surfaced a specific problem, or `RxApp.DefaultExceptionHandler`
  (`Services/ToastExceptionHandler.cs`, wired in `App.axaml.cs`) caught an unhandled one.

Implemented on top of Avalonia's built-in `WindowNotificationManager` (`Services/ToastService.cs`),
not a hand-rolled toast stack — it renders inside the host `TopLevel`'s `AdornerLayer`, which paints
above regular window content by construction, so toasts stay visible above the dialog overlay above
without any extra z-order work. `Styles/Toasts.axaml` maps `NotificationType` to the existing
`Status*Brush` tokens (`Tokens.axaml`) via `NotificationCard`'s `:information`/`:warning`/`:error`
pseudoclasses, rather than FluentTheme's stock notification colors.

## Accessibility

- **Label every input via `FormField`**, not a loose `TextBlock` above it. `FormField` sets
  `AutomationProperties.LabeledBy`, which is what actually makes the caption a label; a `TextBlock`
  that merely sits above a `TextBox` is not one.
- Any icon-only control (no visible text) must have `AutomationProperties.Name`, and should also
  have `ToolTip.Tip` for sighted mouse users — see every nav row/toggle in `MainWindow.axaml` and
  the Add/Edit/Remove buttons in `Controls/LinkedRecordsPanel.axaml` for the pattern.
  `Button.Overflow` supplies both by default (see above).
- **A search box's `Watermark` is not an accessible name** — it disappears as soon as there's text in
  the box. Set `AutomationProperties.Name` alongside it (`FilterBar` does this from its own
  `SearchWatermark`).
- **Never convey state by icon or colour alone.** `SortByButton` shows direction with an up/down arrow,
  so it also exposes a `SortDescription` ("Sort by Name, ascending") as both its accessible name and
  tooltip. Toasts carry severity as a coloured icon badge, so `ToastContent` prefixes its accessible
  name with the severity word.
- Don't set `IsTabStop="False"` or `Focusable="False"` on interactive controls without a specific
  reason — the app relies on FluentTheme's default focus-visible/tab-order behavior. The one
  deliberate exception is `DialogHost` blanking tab navigation on the content behind an open modal,
  which is restored on close.
- Keyboard operation for dialogs is `DialogHost`'s job, not each dialog's — see the Dialogs section.
  `MarkRollWindow`, being a real `Window`, wires its own equivalents in code-behind (Escape closes via
  the discard-changes prompt, Ctrl+S saves) with an explicit `e.Handled` check so its own dialog
  overlay gets Escape first.
- New semantic colors must be checked against WCAG AA (≥4.5:1 contrast for normal text) in **both**
  theme variants before being added to the tables above.

## Animation

Motion is intentionally minimal: a ~120ms background-color transition on buttons/list rows (hover
feedback — see the `/template/ ContentPresenter` gotcha under "Global control styles" above if you're
adding a new one of these), a ~150ms opacity fade on validation errors, and a 150ms cross-fade
between routed pages (`RoutedViewHost.PageTransition` in `MainWindow.axaml`), and a 120ms fade for
drop-downs. **Every flyout is a `controls:FadeMenuFlyout` / `controls:FadeFlyout`** (`Controls/FadeFlyouts.cs`),
not a plain `MenuFlyout`/`Flyout`: they fade in on open and fade out before closing (a close request,
whether a menu item, Escape or a click outside, is held until the fade has played). A `ComboBox`
drop-down fades in only (a style animation on its `PopupBorder` in `Styles/Controls.axaml`), since its
popup closes instantly with no hook to delay it. `SplitView`'s pane
open/close animation is Avalonia's built-in default — don't add a custom one. Keep any new motion in
this same short, subtle range; nothing in this app should have a animation longer than ~200ms.
