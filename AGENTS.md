# B4X_Context — Agent Guide

## Build & run

```powershell
dotnet build -c Release
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

Release build by default; no Debug config defined. .NET 8 SDK required. Windows-only (WPF + WinForms). No NuGet deps — pure framework references.

CI: `.github/workflows/release.yml` — triggers on `v*` tag push, builds self-contained single-file exe, zips, creates GitHub Release.

## Architecture

b4x_context.csproj         — .NET 8 WPF app (net8.0-windows), single-file publish
b4x_context.slnx           — single-project solution (slnx format)
├── Engine/                — B4X language toolchain (no external deps)
│   ├── B4xLexer.cs        — tokenizer (keywords, strings, comments, directives)
│   ├── B4xParser.cs       — parser (Sub/Type/Region detection, block matching)
│   ├── SkeletonGenerator.cs — produce skeleton code (omit body, keep headers)
│   ├── BalDecoder.cs      — .bal/.bjl layout binary decoder → JSON or text outline
│   └── BuildOutputParser.cs — regex-based B4X compiler output parser (javac errors too)
├── Services/              — business logic
│   ├── ProjectScanner.cs  — scan folder for .bas/.bal/.bjl/.bil/.b4a/.b4j/.b4i files
│   ├── BundleBuilder.cs   — assemble markdown bundle + copy to clipboard
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

- **Scanner** ignores `Objects`, `bin`, `gen`, `obj`, `.git` folders.
- **Builder locator** checks hardcoded paths under `C:\Program Files (x86)\Anywhere Software\` + per-project config override in `b4x_context_config.json` (key: `builder_path`).
- **Settings** stored at `%APPDATA%\B4XContext\settings.json` (default hotkey: `Ctrl+Shift+P`).
- **Global Ctrl+C** uses low-level keyboard hook (`WH_KEYBOARD_LL`) — captured text populates the preamble box. App skips own output (bundles start with `# Context Bundle`).
- **Global hotkey** (default `Ctrl+Shift+P`) uses `RegisterHotKey` — brings window to foreground.
- **Parse error handling**: parser reports unmatched blocks + wrong closer names; errors don't crash but are collected in `ParseIssue` list.
- **Build timeout**: 300s default, passed via `BuilderRunner.RunBuild(builder, projFile, 300)`.
- Build debug logs written to `%TEMP%\b4x_builder_invoke.log`.

## Publishing quirks (csproj)

- `PublishTrimmed` disabled when `UseWindowsForms=true` (avoids NETSDK1175).
- `_SuppressWpfTrimError` set to `true` to allow WPF trimming.
- `PublishReadyToRun` + `EnableCompressionInSingleFile` enabled.
- `SelfContained` + `PublishSingleFile` enabled by default.
- `DebugType` = `none` in publish profile.
- `RuntimeIdentifier` defaults to `win-x64`.

## Testing

No tests, linters, or formatters configured in this repo.

## Code conventions

- Namespaces: `B4XContext.Models`, `B4XContext.Services`, `B4XContext.Engine`, `b4x_context` (UI).
- Dark theme via `App.xaml` resources — all new UI elements should use `StaticResource` references.
- Engine files are pure stateless static classes (no DI). Services use static methods too.
- `CodeUtils.ReadTextSafely()` handles multi-encoding fallback (UTF-8 BOM → UTF-8 → windows-1252 → latin1) and strips B4X design surface markers.
