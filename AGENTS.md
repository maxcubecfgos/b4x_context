# B4X_Context — Agent Guide

## Build & run

```powershell
dotnet build -c Release
dotnet publish b4x_context.csproj -c Release -o publish
```

Publish must target the `.csproj`, not the `.slnx`: `dotnet publish -o` at solution level is
unsupported (NETSDK1194) and the solution wrapper re-copies the tree-sitter grammars after the
`PruneUnusedTreeSitterGrammars` target, leaving the unused grammar DLLs (~85 MB extra) in `publish\`.

Release build by default; no Debug config defined. .NET 10 SDK required (net10.0-windows). Windows-only (WPF + WinForms). NuGet: `TreeSitter.DotNet 1.3.0` (per-language `tree-sitter-*.dll` grammars, native win-x64, used for multi-language skeletons) + `SharpToken 2.0.6`. Framework-dependent publish — the target machine needs the **.NET 10 Desktop Runtime** (no embedded runtime, no single-file): this keeps startup fast by avoiding per-launch decompression of a ~700 MB bundle, and `ts_pack_core_ffi.dll` (442 MB) is **not** shipped.

CI: `.github/workflows/release.yml` — triggers on `v*` tag push, builds a framework-dependent folder, zips, creates GitHub Release. Runs on .NET 10.

## Architecture

b4x_context.csproj         — .NET 10 WPF app (net10.0-windows), framework-dependent folder publish
b4x_context.slnx           — single-project solution (slnx format)
├── Engine/                — B4X language toolchain (no external deps)
│   ├── B4xLexer.cs        — tokenizer (keywords, strings, comments, directives)
│   ├── B4xParser.cs       — parser (Sub/Type/Region detection, block matching)
│   ├── SkeletonGenerator.cs — produce skeleton code (omit body, keep headers)
│   ├── LangSupport.cs     — multi-language profiles (grammar id, fence tag, node kinds)
│   ├── MultiLangSkeletonizer.cs — tree-sitter skeleton for non-B4X languages (+ fallback)
│   ├── DartSkeletonizer.cs — hand-written Dart lexer + structural extractor (skeleton, no grammar)
│   ├── DartItemExtractor.cs — Dart declarations → ModuleItem for the granular "Custom" mode
│   ├── B4xGranularBuilder.cs — Custom bundle: header + index + selected item blocks
│   ├── BalDecoder.cs      — .bal/.bjl layout binary decoder → JSON or text outline
│   └── BuildOutputParser.cs — regex-based B4X compiler output parser (javac errors too)
├── Services/              — business logic
│   ├── GitIgnoreMatcher.cs — minimal .gitignore matcher (globs, negation, dir-only, **, nested scopes)
│   ├── ProjectScanner.cs  — pruned walker for B4X + generic code files
│   ├── BundleBuilder.cs   — assemble markdown bundle + copy to clipboard
│   ├── AutoPreambleGenerator.cs — Auto-Fill: package.json/Cargo.toml/README + codebase profile
│   ├── BuilderLocator.cs  — find B4ABuilder/B4JBuilder.exe on disk
│   ├── BuilderRunner.cs   — run builder process (300s timeout), capture output
│   ├── BuildFormatter.cs  — format parsed errors to markdown
│   ├── ContextBudget.cs   — context budget for local models: 25% output reserve, warn-only overflow analysis (offenders largest-first)
│   ├── LocalCompactor.cs  — opencode-style anchored summaries via local endpoint; probe/list-models/pick-model + Ollama native chat (`think:false`)
│   └── CodeUtils.cs       — UTF-8/win-1252 fallback reading, BOM strip, @EndOfDesignText@ skip
├── Models/
│   └── ProjectFile.cs     — Path, Included, Mode (Skeleton/Full), Kind, EstimatedTokens, Summary/UseSummary (compacted files)
├── MainWindow.xaml(.cs)   — UI: file list, text boxes, compile + generate buttons, budget indicator, COMPACT button
├── HotkeySettingsWindow.xaml(.cs) — settings dialog: hotkey, context-window slider (1K–1M, default 4K), local endpoint + model combo
└── App.xaml               — light slate/lavender theme resources, WPF styles (incl. implicit ComboBox/Slider templates)

## Key behaviors

- **Scanner (blocklist, paridad PromptPacker)**: scans B4X (`bas/bal/bjl/bil/b4a/b4j/b4i`) + **everything else** (generic code, `xaml/csproj/sln/slnx`, `md`, `txt`, `LICENSE`, `TODO`, extensionless…). Walks with a stack-based DFS that prunes **before descending**: ignores a folder list (`Objects`, `bin`, `gen`, `obj`, `.git`, `node_modules`, `target`, `build`, `dist`, `.venv`, `__pycache__`, `.next`, `vendor`, `coverage`, `.hg`, `.svn`, `.vscode`, `.idea`, `.cache`, `.pytest_cache`, `.mypy_cache`, `.ruff_cache`, `out`, `tmp`, `temp`, `logs`, `log`, `venv`, helpers dirs, etc.), hidden files/dirs (leading `.`), **reparse points/junctions** (e.g. `compiler_hosts/*/Libraries/` junctions must never be traversed), project `.gitignore` (root + nested, last-match-wins, `!` negation, dir-only `dir/`, `**`), plus `IgnoredFileNames` (`.ds_store`, `thumbs.db`, `desktop.ini`) and `IgnoredFileSuffixes` (binary/media/office/datos: images, fonts, exe/dll/so, archives, media, audio, `.log/.map/.cache/.min.js/.min.css/.bak/.lock/.icns`). Grammar IDs confirmed: `python`, `typescript`, `tsx`, `javascript` (js/mjs/cjs/jsx — no `js`/`jsx` grammars in pack), `rust`, `go`, `c`, `csharp`, `java`, `json`, `css`, `scss`, `less`, `html`. `scss`/`less` have **no grammar in `TreeSitter.DotNet 1.3.0`** (the pack has no `tree-sitter-scss`/`-less`), so they carry a profile with `GrammarId = null` and `SkeletonKind.Structural`; `dart` also has no grammar but uses its own parser (`SkeletonKind.Dart`, see below); `xaml`/`csproj`/`slnx`/`sln` and any unknown ext get a **structural default profile** — all of them go through `MultiLangSkeletonizer.StructuralCompress` = port of PromptPacker's `fallback_compress` (imports/defs/fn/class/var/visibility/decorators, `mixin`/`extension`/`part`/`library`/`abstract` headers, C-family/Dart block-opening signatures like `Widget build(BuildContext c) {`, config `key=value`/markdown `#`+fences+lists; `.lock` → empty; lines truncated to 200 chars, skeleton capped at 200 lines / 8000 chars with `// ...` marker).
- **Dart (no tree-sitter grammar available)**: `.dart` is parsed by `Engine/DartSkeletonizer` — a hand-written Dart lexer (strings incl. raw/triple/interpolated, nested block comments, shebang) plus a brace/paren aware declaration extractor. It keeps `library`/`import`/`export`/`part` directives, leading comments and doc comments (capped per declaration: **6 lines** above a type/file header, **3 lines** above a member, then `// ... (N lines omitted) ...`), and the headers of class/mixin/extension/extension type/enum/typedef plus every member signature (fields, constructors incl. initializer lists, methods, getters/setters/operators, abstract members, annotations); bodies and closure bodies become `// ... (N lines omitted) ...`, enum constants are kept verbatim, multi-line headers/statements bigger than 6 lines keep first + last line, arrow bodies bigger than 2 lines are trimmed, and the result is capped at 200 lines / 8000 chars. Dispatch: `LangProfile.Kind == SkeletonKind.Dart` (see `RegisterHandwritten`); any exception falls back to `StructuralCompress`. **No line from inside an implementation body ever reaches the skeleton**: `DartSkeletonizer.ImplementationRanges()` returns the exact 1-based ranges of function/constructor/closure bodies (type bodies excluded, their member signatures are the skeleton), double-quoted/`$`-interpolated/raw/triple strings and nested block comments are lexed as single tokens, and a closure nested in a statement is collapsed by `EmitExpression`. Verified against the full Flutter repo (`packages/`, 4081 files): 0 exceptions, 0 cap violations, **0 body lines leaked**, 11.7% line ratio (2.015.409 → 236.100 lines).
- **Granular "Custom" mode covers Dart too**: `ProjectFile.SupportsGranular` (B4X modules + `dart`) drives the chevron/panel in `MainWindow.xaml`, `EnsureItems` and the Skeleton → Full → Custom cycle. `BundleBuilder.GetItems` routes `.dart` to `DartItemExtractor` (types → TYPES, methods/constructors/getters/setters/operators → SUBS, fields/top-level variables → VARIABLES, members carry `Container` = enclosing type). Custom bundles reuse `B4xGranularBuilder.BuildCustom` with `maxHeaderLines = int.MaxValue` (Dart header = everything before the first item, i.e. the directives) and a selected type suppresses its members so nothing is emitted twice.
- **PromptPacker-style defaults**: on project load **no file is checked** (`ProjectFile.Included = false`) **except `README.md` at root**, which is auto-selected as Full when nothing is included. The file list is grouped by relative directory; clicking a folder header toggles all files inside it. `AllButton` cycles **None → All(Skeleton) → All(Full) → None**.
- **Async token estimation**: token counts are computed in a background `Task.Run` worker with a per-path cache (`_tokenCache`, `MainWindow.xaml.cs`); the list appears instantly at load and the token counters fill in as each file completes. Toggles re-use the cache (no re-parse). Compression % is recomputed from the same cache. `bal/bjl/bil` are estimated by raw length (no B4X parse); `EstimateTokensForFile` also fills `ProjectFile.LineCount` for the tree. On re-scan (`LoadProjectFolder`) the cache is only invalidated for files that vanished or whose size changed (`Services/TokenEstimateCache.StaleKeys`).
- **Token counting = real cl100k BPE** (`Services/TokenCounter` via SharpToken 2.0.6, `GptEncoding.GetEncoding("cl100k_base")`, lazy init with `text.Length/4` fallback). Full = cl100k(full code text), Skeleton = cl100k(actual skeleton text), preamble/task overhead = cl100k. Matches Python `tiktoken.get_encoding('cl100k_base')` token-for-token on real files (verified: `splash.b4a` → 45,015 on the `ReadTextSafely`-stripped body). Tree text in the bundle uses the original prompt-pack-lite heuristic `(chars+8)/4`.
- **Bundle FILE TREE** (`BundleBuilder.BuildAsciiTree`) renders the PromptPacker-style tree from `DisplayPath`: connectors `|- `/`\- `, dirs/files interleaved and sorted by name, each file annotated `(size, N lines)` (bytes `B/KB/MB`), capped at 4000 lines with `... tree truncated ...`.
- **Auto-Fill button** (PREAMBLE / CONTEXT): `Services/AutoPreambleGenerator` reads root files only — `package.json` (Project/Description/Key Stack Node, deps filtered by keyword list, top 10), `Cargo.toml` (regex name/description + "Stack Hint: Rust Project detected."), `README.md` (architecture/flow/`┌`/`╔` block ≤25 lines → "Project Context:", else first 15 lines minus `[!` badges → "Project Overview:"), and a "Codebase Profile" with top-5 extensions (excluding png/jpg/jpeg/svg/ico/lock/json/map). Appends to the preamble box; if nothing found shows an info dialog.
- **File watcher**: `FileSystemWatcher` over the project root (500 ms debounce) auto re-scans preserving selections by path; opening the same folder again also preserves. Loading a different folder resets state.
- **Builder locator** checks hardcoded paths under `C:\Program Files (x86)\Anywhere Software\` + per-project config override in `b4x_context_config.json` (key: `builder_path`).
- **Context budget for local models** (`Services/ContextBudget`, modeled on opencode's `usable()`): the prompt budget is `context − 25% reserved for the answer` (4K → 3.072 usable, floor 256). Presets 1K–1M (`Stops`) configured **only** via the Settings slider (the Pack Summary combo was removed as redundant); `Analyze()` is **warn-only**: colors the token counter red, shows `BudgetText` (usable/free) and `BudgetWarningText` listing the largest offenders (greedy, largest-first) — it never removes files. Persisted as `TargetContext` in `%APPDATA%\B4XContext\settings.json`.
- **Compact bundle layout** (`BundleBuilder.BuildMarkdown`): order is `TASK → RESPONSE RULES → PREAMBLE → FILE TREE → FILES`, so a 4K window always keeps the goal + contract in view (`compactRules: false` disables the rules block). `ProjectFile.UseSummary` makes a file emit its `Summary` under a `(Summary)` header instead of source.
- **COMPACT button** (opencode-style compaction): `AutoCompactButton_Click` preflights `LocalCompactor.ProbeAsync` (`GET {base}/models`, 2.5 s timeout) → if the server is down shows a MessageBox with endpoint/model/start-Ollama guidance; then `ListModelsAsync` verifies the configured model exists (else MessageBox with available models + `PickModel` suggestion); then each offender is summarized via `LocalCompactor.SummarizeAsync` with an anchored template (`## Purpose/Key Symbols/Contracts/Notes`, terse bullets, verbatim identifiers, ≤20 bullets) until the bundle fits. The button turns into **CANCEL** while running (`_compactCts`), a 1 s ticker shows phase + elapsed seconds, changing Settings cancels a running job, and each request is capped at `RequestTimeoutSeconds`=120 s (timeout → clear message instead of hanging). **Ollama detection**: native `POST /api/chat` with `think:false` (reasoning models like qwen3.5 otherwise burn `MaxOutputTokens`=2048 on hidden thinking and return empty content) — non-Ollama servers fall back to `/v1/chat/completions`. Failures are classified with `IsOffline`/`ParseApiError`.
- **Settings dialog**: context slider (snap to `ContextBudget.Stops`, shows reserved/usable), endpoint TextBox (re-lists models on LostFocus) and **non-editable** model ComboBox populated from the server (an editable one swallowed clicks meant to open the list); auto-picks a local coding model when the configured one is missing (never `:cloud`). All persisted (`TargetContext`, `LocalEndpoint`, `LocalModel`).
- **Settings** stored at `%APPDATA%\B4XContext\settings.json` (default hotkey: `Ctrl+Shift+P`).
- **Preamble PASTE button**: reads the clipboard on demand and appends to the preamble box (`Services/TextUtils.AppendBlock`). No global clipboard hook — clipboard is only read when the user clicks PASTE.
- **Global hotkey** (default `Ctrl+Shift+P`) uses `RegisterHotKey` — brings window to foreground.
- **Parse error handling**: parser reports unmatched blocks + wrong closer names; errors don't crash but are collected in `ParseIssue` list.
- **Build timeout**: 300s default, passed via `BuilderRunner.RunBuild(builder, projFile, 300)`.
- Build debug logs written to `%TEMP%\b4x_builder_invoke.log`.

## Publishing quirks (csproj)

- Framework-dependent folder build (`SelfContained=false`, `PublishSingleFile=false`) — target machine needs the **.NET 10 Desktop Runtime**.
- `_SuppressWpfTrimError` set to `true` to allow WPF trimming.
- `PublishReadyToRun` enabled (crossgen compiles app assemblies only in framework-dependent mode).
- `DebugType` = `none` in publish profile.
- `RuntimeIdentifier` defaults to `win-x64`.
- `PruneUnusedTreeSitterGrammars` target strips the ~18 unused `tree-sitter-*.dll` grammars (verilog/razor/agda/swift/ruby…) from publish → folder ~21 MB instead of ~78 MB. Build/test outputs keep all grammars so `MultiLangSkeletonizerTests` stay green.
- Startup is measured at ~0.7–0.8 s (warm) vs ~1.0–1.2 s for the old self-contained single-file build; the first launch of a new build is slower (loader/AV cache).

## Testing

xUnit suite in `tests/B4XContext.Tests` (`dotnet test tests\B4XContext.Tests -c Release`). No linters or formatters configured.

## Code conventions

- Namespaces: `B4XContext.Models`, `B4XContext.Services`, `B4XContext.Engine`, `b4x_context` (UI).
- Dark theme via `App.xaml` resources — all new UI elements should use `StaticResource` references.
- Engine files are pure stateless static classes (no DI). Services use static methods too.
- `CodeUtils.ReadTextSafely()` handles multi-encoding fallback (UTF-8 BOM → UTF-8 → windows-1252 → latin1) and strips B4X design surface markers.
