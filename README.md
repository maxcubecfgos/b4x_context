# B4X_Context

A lightweight **Windows** companion tool for B4X (B4A/B4J) and, more broadly,
for any code project. It scans your project, builds compact markdown context
(Skeleton or Full per file, or granular Sub/Type selection), optionally
compiles the project and attaches real compiler errors, and copies a clean
Markdown bundle to your clipboard — ready to paste into any AI assistant.

## Why

B4X's own "Code Bundle" export (Ctrl+R) dumps the *entire* project as JSON,
which quickly exceeds usable context size on real projects. B4X_Context lets
you send only what's relevant: the exact Sub you're working on, chosen files
in full or skeleton form, and (optionally) your last compile errors. It
follows the same flow as [PromptPacker Lite](https://github.com/ClarkOhlenbusch/PromptPacker):
instant project loading, nothing selected by default, and folder-first
selection.

## Download

Grab the latest zip from the [Releases](../../releases) page — no installer
needed, just download, extract and run `b4x_context.exe`. Every release is
built automatically from this exact source via GitHub Actions
(`.github/workflows/release.yml`) — nothing is manually uploaded, so any
release can be traced back to the exact commit that produced it.

Requires **Windows 10/11** and the **.NET 10 Desktop Runtime**. The published
build is framework-dependent (a plain folder, no embedded runtime and no
giant single-file bundle), so install the runtime from
https://dotnet.microsoft.com/download/dotnet/10.0 if you don't have it.

## Usage

1. Launch `B4XContext.exe` and leave it running in the background.
2. Copy any text (in the B4X IDE or anywhere), then use the **PASTE** button
   in the preamble box to pull it from the clipboard on demand.
3. Press **Ctrl+Shift+P** (configurable) to bring the app window to the
   foreground.
4. **Scan** your project folder. The file tree loads instantly, grouped by
   directory — **no file is selected by default** except `README.md` (root),
   which comes pre-selected as Full. Click a **folder header** to
   select/deselect every file inside it; `ALL` cycles **None → All (Skeleton)
   → All (Full) → None**.
5. Pick the mode per file — **Skeleton** (headers only), **Full**, or
   **Custom** (B4X granular Sub/Type selection). Token estimates fill in
   automatically in the background.
6. Optionally **Auto-Fill** the preamble box from root files
   (`package.json` / `Cargo.toml` / `README.md` + codebase profile), then
   optionally **compile** to attach real errors, and **GENERATE PROMPT** to
   copy the bundle (with a PromptPacker-style file tree) to your clipboard.

## Features

- Real B4X tokenizer/parser (not regex) for accurate Sub/Type detection
- `.bal`/`.bjl`/`.bil` layout decoding (B4A and B4J share the same binary format)
- **Multi-language skeletons** via tree-sitter: Python, TypeScript/TSX,
  JavaScript, Rust, Go, C, C#, JSON/JSONC, CSS, HTML; every other file type
  (`.xaml`, `.csproj`, `.sln`, `.slnx`, `.scss`, `.less`, `README.md`,
  `LICENSE`, `TODO`, extensionless…) gets a structural skeleton via a faithful
  port of PromptPacker's `fallback_compress`
- PromptPacker-style workflow: scan loads **all** non-ignored files (binary,
  media, archives, `.lock`, `.min.js` and desktop metadata are skipped),
  folder-grouped list, folder-header click selects all inside
- **Auto-Fill** button — builds a project context from root files
  (`package.json`, `Cargo.toml`, `README.md`, codebase profile) into the
  preamble box
- Auto re-scan on file changes (500 ms debounce) while preserving your
  selections
- Async token estimation with a per-path cache (instant load, numbers fill
  in without blocking the UI)
- Smart project scan: respects `.gitignore`, skips hidden files,
  IDE/cache/build folders, and **never** follows junctions/reparse points
  (e.g. `compiler_hosts/*/Libraries/`)
- Per-file Skeleton / Full / Custom modes with live token estimates
- Optional compile step (B4ABuilder.exe / B4JBuilder.exe) with structured,
  readable errors attached to the bundle
- **PASTE** button — manual clipboard paste into the preamble box (no global
  keyboard hook, clipboard is only read when you click it)
- Configurable global hotkey to bring the app to the foreground instantly
- Settings stored at `%APPDATA%\B4XContext\settings.json`
- Dark theme

## Building from source

```powershell
git clone https://github.com/maxcubecfgos/b4x_context
cd b4x_context
dotnet build -c Release
```

Requires the **.NET 10 SDK** (Windows). To produce the framework-dependent
folder build (the target machine just needs the .NET 10 Desktop Runtime):

```powershell
dotnet publish b4x_context.csproj -c Release -o publish
```

## Tests

```powershell
dotnet test tests\B4XContext.Tests -c Release
```

xUnit suite covering the scanner, `.gitignore` matcher, B4X + multi-language
skeletonizers, bundle builder and the ASCII file tree.

## License

MIT