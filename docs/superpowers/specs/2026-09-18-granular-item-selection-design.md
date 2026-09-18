# Granular Item Selection — Design

**Date:** 2026-09-18
**Status:** Approved by user (chat, 2026-09-18)

## Problem

B4X_Context currently lets the user include each `.bas` module as either
**Full** (whole file) or **Skeleton** (all headers, no bodies) plus the
globals block. For big modules that is still too much context. The user
wants to select **individual items** — specific Subs, individual global
variables, Types, Regions — so a module can be narrowed down to only the
pieces relevant to the AI task, while the AI still sees a compact index of
everything the module contains.

## Goal

Add a granular selection mode per `.bas` module:

1. Select any subset of Subs, variables, Types, and Regions within a module.
2. Generated bundle for a module in granular mode = compact index of ALL
   items (name + signature) + full code of the SELECTED items only.
3. Keep Full and Skeleton modes untouched.

## Reference Projects (parsers to reuse)

- `b4x_intellisense` — `B4XChatbot/Parser/B4xAst.cs` (item model: Sub/Variable/
  Type/Region with signature) and `B4xParser.cs` (regexes `ReDim`/
  `RePrivateDim`/`ReTypeField`, `SplitParams`).
- `b4x-mcp` — `schemas/source.py` (SubDeclaration.signature, VariableDeclaration,
  TypeDeclaration, Region) confirms the same item model.

Only the variable-extraction regexes and the signature-format convention are
borrowed; the structural node parsing stays in the existing `B4xParser`.

## Design

### 1. Model (`Models/`)

- `FileMode` becomes `{ Full, Skeleton, Custom }` (enum — no 3rd state label
  change elsewhere needed; `ToString()` gives "Custom").
- New `ModuleItem`:
  - `Kind` — enum `ModuleItemKind { Sub, Variable, Type, Region }`.
  - `Group` — string used for grouping/display: `"SUBS"`, `"VARIABLES"`,
    `"TYPES"`, `"REGIONS"`.
  - `Name`, `Signature`, `StartLine`, `EndLine`, `Container` (globals block
    name for variables, region name for region-contained items, else `""`).
  - `IsSelected` (bool, default `false`).
- `ProjectFile` gains:
  - `ObservableCollection<ModuleItem> Items` (flat list, ordered Sub → Var →
    Type → Region).
  - `bool IsExpanded` (UI state for the expandable row).
  - `FileMode Mode` semantics: setting `Mode = Custom` programmatically marks
    granular mode; helper `List<ModuleItem> SelectedItems => Items.Where(...)`.
  - `void ResetCustom()` — clears all `IsSelected`, returns `Mode` to
    `Skeleton`.

### 2. Engine — item extraction (`Engine/B4xItemExtractor.cs`)

- Static `List<ModuleItem> ExtractItems(string source, B4XNode root, string moduleName)`.
  Works entirely off an existing `B4xParser.Parse` result (no second parse):
  - Walk `root.Children` with `FlattenSubsAndTypes`-style traversal, tracking
    container (region name / globals kind).
  - Subs (Kind `Sub`), Types (Kind `Type`) → items with `Signature`.
  - Regions (Kind `Region`) → one item covering the region's line span.
  - Globals blocks (`Process_Globals`/`Class_Globals`/`Globals`) → split each
    `Dim` / `Private … Dim` line in `[StartLine+1, EndLine-1]` into one
    `Variable` item using:
    - `^\s*(?:Private\s+|Public\s+)?Dim\s+([A-Za-z_]\w*)\s+As\s+([\w.]+(?:\s*\(\s*\))?)(?:\s*=\s*(.*))?$`
      (port of `ReDim`/`RePrivateDim`).
  - `Signature` formats:
    - `{access} Sub {name}({params}) As {return}` — params `name As type`, comma
      joined. Globals blocks are NOT emitted as Sub items.
    - `Var {name} As {type}` (+ ` = {initializer}` when present).
    - `Type {name}({field: name As type; …})`.
    - `Region '{name}'`.
- `access` is `Private`/`Public` from the node's `IsPrivate`.

### 3. Engine — granular builder (`Engine/B4xGranularBuilder.cs`)

- Static `(string code, int estimatedChars) BuildCustom(string source,
  List<ModuleItem> items, string moduleName)`.
- Algorithm:
  - Emit module preamble: leading comment/metadata lines up to the first
    item's start line (module header comment block), so context has the
    module's doc header.
  - Emit `INDEX` block (markdown list, ALL items):
    ```
    Index of '{name}': {total} items ({selected} selected)
    - [x] Sub Foo(...) — line 12
    - [ ] Var count As Int — line 8
    ```
    Ordered in the same Sub→Var→Type→Region order.
  - Emit `SELECTED ITEMS` block: for each selected item, its exact source
    lines (`StartLine..EndLine`) as a ```b4x``` block.
  - **Dedup rule:** if a Region item is selected, its contained Subs/Types are
    skipped in the selected-emission pass (they are inside the region's lines).
  - `estimatedChars` = index length + sum of selected raw line lengths (used
    for token estimates).

### 4. Bundle integration (`Services/BundleBuilder.cs`)

- In the `.bas` branch of the files loop:
  - `Mode == Full` → unchanged (whole text).
  - `Mode == Skeleton` → unchanged (`SkeletonGenerator`).
  - `Mode == Custom` → `B4xGranularBuilder.BuildCustom`.
- Layouts (`.bal/.bjl/.bil`) unchanged — no granular items.

### 5. UI (`MainWindow.xaml` / `.xaml.cs`, `App.xaml`)

- **3-state mode button** per `.bas` row cycles `Full → Skeleton → Custom →
  Full`. Layout rows keep 2-state cycle (Skeleton ↔ Full).
- **Expandable row:** chevron button `▸/▾` (visible only for `.bas`) toggles
  `IsExpanded`. When expanded, an `ItemsControl` bound to `Items` renders each
  item as: checkbox (`IsSelected`), kind badge, signature text (mono). Group
  headers `SUBS / VARIABLES / TYPES / REGIONS` are implemented as four
  fixed sections whose `Visibility` is set in code-behind (empty sections
  hidden) — avoids converters/`CollectionViewSource` complexity and matches the
  codebase's plain-binding style.
- Checking any item sets the row's `Mode` to `Custom`. A per-row `↺` reset
  button (shown when `IsExpanded`) calls `ResetCustom()`.
- `AllButton`/`RefreshButton`/`UpdateSummary` behavior unchanged.
- Token estimate in Custom mode = `BuildCustom(...).estimatedChars / 4`.

### 6. Verification

- New test project `B4XContext.Tests` (xUnit, `net8.0-windows`) in `tests/`:
  - `B4xItemExtractorTests` — variable splitting (single, private, with init,
    comma-free), subs/types/regions items, nested-in-region tracking, dedup.
  - `B4xGranularBuilderTests` — index contents (✓/✗ markers, counts),
    selected emission, region dedup, preamble.
- Manual smoke: build, scan a small B4X project, toggle Custom, expand,
  select items, GENERATE PROMPT, inspect clipboard markdown.
- Full build: `dotnet build b4x_context.csproj -c Release` must pass.
  Tests: `dotnet test tests/B4XContext.Tests/`.

## Out of Scope

- Granular selection of views inside `.bal/.bjl/.bil` layouts.
- Per-item Full/Skeleton sub-mode (selected items are always full code).
- Dependency-aware auto-selection (referenced-but-excluded detection).
- Wiring `_activeSub/_activeFile` (pre-existing dead feature, unchanged here).