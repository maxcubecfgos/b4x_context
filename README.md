
# B4X_Context

A lightweight Windows companion tool for B4X (B4A/B4J) developers. It scans
your B4X project, decodes layout files, lets you pick exactly which
code/layouts to include (full or skeleton), optionally compiles the project
and attaches real errors, and copies a clean, minimal Markdown context
bundle to your clipboard — ready to paste into any AI assistant.

## Why

B4X's own "Code Bundle" export (Ctrl+R) dumps the *entire* project as JSON,
which quickly exceeds usable context size on real projects. B4X_Context lets
you send only what's relevant: the exact Sub you're working on, chosen
modules in full or skeleton form, and (optionally) your last compile errors.

## Download

Grab the latest `.exe` from the [Releases](../../releases) page — no
installer needed, just download and run. Every release is built
automatically from this exact source via GitHub Actions
(`.github/workflows/release.yml`) — nothing is manually uploaded, so any
release can be traced back to the exact commit that produced it.

Requires Windows 10/11. The published build is self-contained (bundles its
own .NET 8 runtime), so no separate .NET install is required.

## Setup: connect it to your B4X IDE

1. In B4A or B4J: **Tools → IDE Options → External Tools**
2. Add a new tool:

   | Field | Value |
   |---|---|
   | Title | B4X Context |
   | Command | path to `B4XContext.exe` |
   | Parameters | `"%FILE%" "%LINE%"` |
   | Run Mode | Hidden |

3. Optionally assign a keyboard shortcut under **Tools → IDE Options →
   Keyboard Shortcuts**.
4. Or skip External Tools entirely: just leave the app running, select code
   in the IDE, press `Ctrl+C`, and it's captured automatically (global
   Ctrl+C capture, see Features below).

## Features

- Real B4X tokenizer/parser (not regex) for accurate Sub/Type detection
- `.bal`/`.bjl` layout decoding (B4A and B4J share the same binary format)
- Per-file Skeleton / Full / Excluded modes with live token estimates
- Optional compile step (B4ABuilder.exe / B4JBuilder.exe) with structured,
  readable errors attached to the bundle
- Global Ctrl+C capture — copy code anywhere, it lands in the app automatically
- Configurable global hotkey to bring the app to the foreground instantly
- Dark theme

## Building from source

```
git clone <this repo>
cd B4XContext
dotnet build -c Release
```

Requires the .NET 8 SDK.

## License

MIT

