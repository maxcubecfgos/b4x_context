# B4X_Context — Agent Guide

## Build & run

```powershell
dotnet build -c Release
dotnet publish -c Release -o publish
```

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
│   └── CodeUtils.cs       — UTF-8/win-1252 fallback reading, BOM strip, @EndOfDesignText@ skip
├── Models/
│   └── ProjectFile.cs     — Path, Included, Mode (Skeleton/Full), Kind, EstimatedTokens
├── MainWindow.xaml(.cs)   — UI: file list, text boxes, compile + generate buttons
├── HotkeySettingsWindow.xaml(.cs) — global hotkey config dialog
└── App.xaml               — dark theme resources, WPF styles

## Key behaviors

- **Scanner (blocklist, paridad PromptPacker)**: scans B4X (`bas/bal/bjl/bil/b4a/b4j/b4i`) + **everything else** (generic code, `xaml/csproj/sln/slnx`, `md`, `txt`, `LICENSE`, `TODO`, extensionless…). Walks with a stack-based DFS that prunes **before descending**: ignores a folder list (`Objects`, `bin`, `gen`, `obj`, `.git`, `node_modules`, `target`, `build`, `dist`, `.venv`, `__pycache__`, `.next`, `vendor`, `coverage`, `.hg`, `.svn`, `.vscode`, `.idea`, `.cache`, `.pytest_cache`, `.mypy_cache`, `.ruff_cache`, `out`, `tmp`, `temp`, `logs`, `log`, `venv`, helpers dirs, etc.), hidden files/dirs (leading `.`), **reparse points/junctions** (e.g. `compiler_hosts/*/Libraries/` junctions must never be traversed), project `.gitignore` (root + nested, last-match-wins, `!` negation, dir-only `dir/`, `**`), plus `IgnoredFileNames` (`.ds_store`, `thumbs.db`, `desktop.ini`) and `IgnoredFileSuffixes` (binary/media/office/datos: images, fonts, exe/dll/so, archives, media, audio, `.log/.map/.cache/.min.js/.min.css/.bak/.lock/.icns`). Grammar IDs confirmed: `python`, `typescript`, `tsx`, `javascript` (js/mjs/cjs/jsx — no `js`/`jsx` grammars in pack), `rust`, `go`, `c`, `csharp`, `json`, `css`, `scss`, `less`, `html`. `xaml`/`csproj`/`slnx`/`sln` and any unknown ext get a **structural default profile** (no tree-sitter): `MultiLangSkeletonizer.StructuralCompress` = port of PromptPacker's `fallback_compress` (imports/defs/fn/class/var/visibility/decorators/config `key=value`/markdown `#`+fences+lists; `.lock` → empty; lines truncated to 200 chars, skeleton capped at 200 lines / 8000 chars with `// ...` marker).
- **PromptPacker-style defaults**: on project load **no file is checked** (`ProjectFile.Included = false`) **except `README.md` at root**, which is auto-selected as Full when nothing is included. The file list is grouped by relative directory; clicking a folder header toggles all files inside it. `AllButton` cycles **None → All(Skeleton) → All(Full) → None**.
- **Async token estimation**: token counts are computed in a background `Task.Run` worker with a per-path cache (`_tokenCache`, `MainWindow.xaml.cs`); the list appears instantly at load and the token counters fill in as each file completes. Toggles re-use the cache (no re-parse). Compression % is recomputed from the same cache. `bal/bjl/bil` are estimated by raw length (no B4X parse); `EstimateTokensForFile` also fills `ProjectFile.LineCount` for the tree. On re-scan (`LoadProjectFolder`) the cache is only invalidated for files that vanished or whose size changed (`Services/TokenEstimateCache.StaleKeys`).
- **Token counting = real cl100k BPE** (`Services/TokenCounter` via SharpToken 2.0.6, `GptEncoding.GetEncoding("cl100k_base")`, lazy init with `text.Length/4` fallback). Full = cl100k(full code text), Skeleton = cl100k(actual skeleton text), preamble/task overhead = cl100k. Matches Python `tiktoken.get_encoding('cl100k_base')` token-for-token on real files (verified: `splash.b4a` → 45,015 on the `ReadTextSafely`-stripped body). Tree text in the bundle uses the original prompt-pack-lite heuristic `(chars+8)/4`.
- **Bundle FILE TREE** (`BundleBuilder.BuildAsciiTree`) renders the PromptPacker-style tree from `DisplayPath`: connectors `|- `/`\- `, dirs/files interleaved and sorted by name, each file annotated `(size, N lines)` (bytes `B/KB/MB`), capped at 4000 lines with `... tree truncated ...`.
- **Auto-Fill button** (PREAMBLE / CONTEXT): `Services/AutoPreambleGenerator` reads root files only — `package.json` (Project/Description/Key Stack Node, deps filtered by keyword list, top 10), `Cargo.toml` (regex name/description + "Stack Hint: Rust Project detected."), `README.md` (architecture/flow/`┌`/`╔` block ≤25 lines → "Project Context:", else first 15 lines minus `[!` badges → "Project Overview:"), and a "Codebase Profile" with top-5 extensions (excluding png/jpg/jpeg/svg/ico/lock/json/map). Appends to the preamble box; if nothing found shows an info dialog.
- **File watcher**: `FileSystemWatcher` over the project root (500 ms debounce) auto re-scans preserving selections by path; opening the same folder again also preserves. Loading a different folder resets state.
- **Builder locator** checks hardcoded paths under `C:\Program Files (x86)\Anywhere Software\` + per-project config override in `b4x_context_config.json` (key: `builder_path`).
- **Settings** stored at `%APPDATA%\B4XContext\settings.json` (default hotkey: `Ctrl+Shift+P`).
- **Global Ctrl+C** uses low-level keyboard hook (`WH_KEYBOARD_LL`) — captured text populates the preamble box. App skips own output (bundles start with `# Context Bundle`).
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
- `PruneUnusedTreeSitterGrammars` target strips the ~19 unused `tree-sitter-*.dll` grammars (verilog/razor/agda/swift/java…) from publish → folder ~21 MB instead of ~78 MB. Build/test outputs keep all grammars so `MultiLangSkeletonizerTests` stay green.
- Startup is measured at ~0.7–0.8 s (warm) vs ~1.0–1.2 s for the old self-contained single-file build; the first launch of a new build is slower (loader/AV cache).

## Testing

xUnit suite in `tests/B4XContext.Tests` (`dotnet test tests\B4XContext.Tests -c Release`). No linters or formatters configured.

## Code conventions

- Namespaces: `B4XContext.Models`, `B4XContext.Services`, `B4XContext.Engine`, `b4x_context` (UI).
- Dark theme via `App.xaml` resources — all new UI elements should use `StaticResource` references.
- Engine files are pure stateless static classes (no DI). Services use static methods too.
- `CodeUtils.ReadTextSafely()` handles multi-encoding fallback (UTF-8 BOM → UTF-8 → windows-1252 → latin1) and strips B4X design surface markers.
