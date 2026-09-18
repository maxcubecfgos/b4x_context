# Granular Item Selection — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let the user select individual Subs / variables / Types / Regions per `.bas` module (new `Custom` file mode) so the generated bundle contains only the chosen code plus a compact index of the whole module.

**Architecture:** Add `FileMode.Custom` + a `ModuleItem` model. A new `Engine/B4xItemExtractor` converts the existing `B4xParser.Parse` tree into items (variables split from globals blocks via the reference-project `Dim` regex). A new `Engine/B4xGranularBuilder` renders the module index + selected items. `BundleBuilder` gains a `Custom` branch. The file list row becomes expandable (chevron → item checkboxes), the mode toggle becomes 3-state, and token estimates reflect custom content. Layouts (`.bal/.bjl/.bil`) are untouched.

**Tech Stack:** .NET 8 WPF (net8.0-windows, `win-x64`), xUnit test project (new). No new NuGet deps in the app project.

**Spec:** `docs/superpowers/specs/2026-09-18-granular-item-selection-design.md`

## Global Constraints

- Target framework `net8.0-windows`; app project keeps `RuntimeIdentifier` `win-x64` — the test project must match both.
- App project adds NO NuGet packages (xUnit lives only in the test project, which is new).
- Follow existing code conventions: Allman braces, 4-space indent, `var`, static classes without DI, `B4XContext.Models` / `B4XContext.Engine` / `B4XContext.Services` namespaces.
- One parse per file: `B4xItemExtractor` reads an existing `B4xParser.Parse` result. Never re-parse for token estimates when items are cached.
- Parser line numbers are 1-based; source split is 0-based (index `StartLine-1`).
- No new top-level files beyond: `Models/ModuleItem.cs`, `Engine/B4xItemExtractor.cs`, `Engine/B4xGranularBuilder.cs`, `tests/B4XContext.Tests/*`.

---

## File Structure

- **Create** `Models/ModuleItem.cs` — `ModuleItemKind` enum + `ModuleItem` class.
- **Modify** `Models/ProjectFile.cs` — `FileMode.Custom`, filtered item views, `ResetCustom`, `EnsureItems`.
- **Create** `Engine/B4xItemExtractor.cs` — `B4xParser.B4XNode` → `List<ModuleItem>`.
- **Create** `Engine/B4xGranularBuilder.cs` — module index + selected-code markdown.
- **Modify** `Services/BundleBuilder.cs` — `Custom` branch for `.bas`.
- **Modify** `MainWindow.xaml(.cs)` — expandable rows, 3-state toggle, item checkboxes, reset.
- **Modify** `App.xaml` — `Custom` state in `ModeToggleButton` style.
- **Create** `tests/B4XContext.Tests/<project + 2 test files>`.

---

### Task 1: Test project scaffolding + Models

**Files:**
- Create: `tests/B4XContext.Tests/B4XContext.Tests.csproj`
- Create: `tests/B4XContext.Tests/ModelsTests.cs`
- Modify: `Models/ProjectFile.cs`
- Create: `Models/ModuleItem.cs`
- Modify: `b4x_context.slnx`

**Interfaces:**
- Produces:
  - `enum Models.FileMode { Skeleton, Full, Custom }`
  - `enum Models.ModuleItemKind { Sub, Variable, Type, Region }`
  - `class Models.ModuleItem { Kind, Name, Signature, StartLine, EndLine, Container, IsSelected, string Group }`
  - `class Models.ProjectFile { ObservableCollection<ModuleItem> Items { get; }, bool IsExpanded, void ResetCustom(), bool HasItems, IEnumerable<ModuleItem> ItemsSubs / ItemsVariables / ItemsTypes / ItemsRegions, bool HasSubs / HasVariables / HasTypes / HasRegions }`

- [ ] **Step 1: Write the failing test**

`tests/B4XContext.Tests/B4XContext.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <UseWPF>true</UseWPF>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.9.0" />
    <PackageReference Include="xunit" Version="2.7.0" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.7" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\b4x_context.csproj" />
  </ItemGroup>
</Project>
```

`tests/B4XContext.Tests/ModelsTests.cs`:

```csharp
using System.Linq;
using B4XContext.Models;
using Xunit;

namespace B4XContext.Tests
{
    public class ModelsTests
    {
        [Fact]
        public void ResetCustom_clears_selection_and_returns_to_skeleton()
        {
            var pf = new ProjectFile(@"C:\proj\Main.bas")
            {
                Mode = FileMode.Custom,
                Included = true,
                Kind = "bas"
            };
            pf.Items.Add(new ModuleItem { Kind = ModuleItemKind.Sub, Name = "Foo", IsSelected = true });

            pf.ResetCustom();

            Assert.False(pf.Items.Single().IsSelected);
            Assert.Equal(FileMode.Skeleton, pf.Mode);
        }

        [Fact]
        public void Item_groups_are_four_distinct_labels()
        {
            Assert.Equal("SUBS", new ModuleItem { Kind = ModuleItemKind.Sub }.Group);
            Assert.Equal("VARIABLES", new ModuleItem { Kind = ModuleItemKind.Variable }.Group);
            Assert.Equal("TYPES", new ModuleItem { Kind = ModuleItemKind.Type }.Group);
            Assert.Equal("REGIONS", new ModuleItem { Kind = ModuleItemKind.Region }.Group);
        }

        [Fact]
        public void Filtered_views_and_flag_properties_reflect_items()
        {
            var pf = new ProjectFile(@"C:\proj\Main.bas") { Kind = "bas" };
            pf.Items.Add(new ModuleItem { Kind = ModuleItemKind.Sub, Name = "Foo" });
            pf.Items.Add(new ModuleItem { Kind = ModuleItemKind.Variable, Name = "x" });

            Assert.Single(pf.ItemsSubs);
            Assert.Single(pf.ItemsVariables);
            Assert.Empty(pf.ItemsTypes);
            Assert.Empty(pf.ItemsRegions);
            Assert.True(pf.HasSubs);
            Assert.True(pf.HasVariables);
            Assert.False(pf.HasTypes);
            Assert.False(pf.HasRegions);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```
dotnet test tests/B4XContext.Tests/B4XContext.Tests.csproj -c Release
```
Expected: FAIL — `FileMode` has no `Custom`; `ProjectFile` has no `Items`/`ResetCustom`; `ModuleItem` missing.

- [ ] **Step 3: Write the implementation**

`Models/ModuleItem.cs`:

```csharp
using System;

namespace B4XContext.Models
{
    public enum ModuleItemKind
    {
        Sub,
        Variable,
        Type,
        Region
    }

    public class ModuleItem
    {
        public ModuleItemKind Kind { get; set; }
        public string Name { get; set; }
        public string Signature { get; set; }
        public int StartLine { get; set; }
        public int EndLine { get; set; }
        public string Container { get; set; } = "";
        public bool IsSelected { get; set; }

        public string Group =>
            Kind switch
            {
                ModuleItemKind.Sub => "SUBS",
                ModuleItemKind.Variable => "VARIABLES",
                ModuleItemKind.Type => "TYPES",
                _ => "REGIONS",
            };
    }
}
```

`Models/ProjectFile.cs` (full replacement):

```csharp
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace B4XContext.Models
{
    public enum FileMode
    {
        Skeleton,
        Full,
        Custom
    }

    public class ProjectFile
    {
        public string Path { get; set; }
        public string Name => System.IO.Path.GetFileName(Path);
        public string Directory => System.IO.Path.GetDirectoryName(Path) ?? "";
        public bool Included { get; set; } = true;
        public FileMode Mode { get; set; } = FileMode.Skeleton;
        public int EstimatedTokens { get; set; } = 0;
        public string Kind { get; set; } = "file";
        public ObservableCollection<ModuleItem> Items { get; } = new ObservableCollection<ModuleItem>();
        public bool IsExpanded { get; set; }

        public ProjectFile(string path)
        {
            Path = path ?? throw new ArgumentNullException(nameof(path));
        }

        public bool HasItems => Items.Count > 0;

        public IEnumerable<ModuleItem> ItemsSubs => Items.Where(i => i.Kind == ModuleItemKind.Sub);
        public IEnumerable<ModuleItem> ItemsVariables => Items.Where(i => i.Kind == ModuleItemKind.Variable);
        public IEnumerable<ModuleItem> ItemsTypes => Items.Where(i => i.Kind == ModuleItemKind.Type);
        public IEnumerable<ModuleItem> ItemsRegions => Items.Where(i => i.Kind == ModuleItemKind.Region);

        public bool HasSubs => Items.Any(i => i.Kind == ModuleItemKind.Sub);
        public bool HasVariables => Items.Any(i => i.Kind == ModuleItemKind.Variable);
        public bool HasTypes => Items.Any(i => i.Kind == ModuleItemKind.Type);
        public bool HasRegions => Items.Any(i => i.Kind == ModuleItemKind.Region);

        public void ResetCustom()
        {
            foreach (var item in Items) item.IsSelected = false;
            Mode = FileMode.Skeleton;
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

```
dotnet test tests/B4XContext.Tests/B4XContext.Tests.csproj -c Release
```
Expected: PASS (3 tests).

- [ ] **Step 5: Register project + commit**

`b4x_context.slnx`:

```xml
<Solution>
  <Project Path="b4x_context.csproj" />
  <Project Path="tests\B4XContext.Tests\B4XContext.Tests.csproj" />
</Solution>
```

```bash
git add Models/ModuleItem.cs Models/ProjectFile.cs tests/B4XContext.Tests b4x_context.slnx
git commit -m "feat: add FileMode.Custom and ModuleItem model with test scaffolding"
```

---

### Task 2: Engine item extraction

**Files:**
- Create: `Engine/B4xItemExtractor.cs`
- Create: `tests/B4XContext.Tests/B4xItemExtractorTests.cs`

**Interfaces:**
- Consumes: `B4xParser.Parse(string)` → `(B4XNode root, List<ParseIssue>)`; `B4XNode.{Kind,Name,StartLine,EndLine,IsPrivate,Children}`; `Models.ModuleItem`.
- Produces: `static List<ModuleItem> ExtractItems(string source, B4xParser.B4XNode root)`

- [ ] **Step 1: Write the failing test**

`tests/B4XContext.Tests/B4xItemExtractorTests.cs`:

```csharp
using System.Linq;
using B4XContext.Engine;
using B4XContext.Models;
using Xunit;

namespace B4XContext.Tests
{
    public class B4xItemExtractorTests
    {
        private const string Sample = @"'Code module for testing
#Region Project Attributes
	#If B4A
#End Region

Sub Process_Globals
	Dim appName As String = ""Demo""
	Dim counter As Int
	Private Dim secret As Long
End Sub

Type Person(Name As String, Age As Int)

Sub Foo(x As Int) As Boolean
	Log(x)
	Return True
End Sub

Private Sub Bar
	Wait For (t As Timer) Timer_Tick
End Sub

#Region Helpers
Sub Zap
	Log(""zap"")
End Sub
#End Region";

        private static System.Collections.Generic.List<ModuleItem> Extract(string src) =>
            B4xItemExtractor.ExtractItems(src, B4xParser.Parse(src).root);

        [Fact]
        public void Extracts_variables_from_globals_block()
        {
            var items = Extract(Sample);
            var vars = items.Where(i => i.Kind == ModuleItemKind.Variable).ToList();

            Assert.Equal(new[] { "appName", "counter", "secret" }, vars.Select(v => v.Name));
            Assert.All(vars, v => Assert.Equal("Process_Globals", v.Container));
            Assert.Contains(vars, v => v.Name == "secret" && v.Signature.Contains("Private Dim secret As Long"));
            Assert.Contains(vars, v => v.Name == "appName" && v.Signature.Contains(""""));
        }

        [Fact]
        public void Extracts_subs_types_regions_with_lines_and_container()
        {
            var items = Extract(Sample);

            var subs = items.Where(i => i.Kind == ModuleItemKind.Sub).ToList();
            Assert.Contains(subs, s => s.Name == "Foo" && s.Signature.StartsWith("Sub Foo(x As Int) As Boolean"));
            Assert.Contains(subs, s => s.Name == "Zap" && s.Container == "Helpers");

            var types = items.Where(i => i.Kind == ModuleItemKind.Type).ToList();
            Assert.Single(types);
            Assert.Equal("Person", types[0].Name);
            Assert.Equal(types[0].StartLine, types[0].EndLine);

            var regions = items.Where(i => i.Kind == ModuleItemKind.Region).ToList();
            Assert.Single(regions);
            Assert.Equal("Helpers", regions[0].Name);
        }

        [Fact]
        public void No_globals_sub_item_is_emitted()
        {
            var items = Extract(Sample);
            Assert.DoesNotContain(items, i => i.Kind == ModuleItemKind.Sub && i.Name == "Process_Globals");
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```
dotnet test tests/B4XContext.Tests/B4XContext.Tests.csproj -c Release --filter B4xItemExtractorTests
```
Expected: FAIL — `B4xItemExtractor` missing.

- [ ] **Step 3: Write minimal implementation**

`Engine/B4xItemExtractor.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using B4XContext.Models;

namespace B4XContext.Engine
{
    public static class B4xItemExtractor
    {
        // Ported from b4x_intellisense B4xParser.ReDim / RePrivateDim.
        private static readonly Regex DimLineRe = new Regex(
            @"^\s*(?:Private\s+|Public\s+)?Dim\s+([A-Za-z_]\w*)\s+As\s+([\w.]+(?:\s*\(\s*\))?)(?:\s*=\s*(.*))?$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly string[] GlobalsKinds = { "Process_Globals", "Globals", "Class_Globals" };

        public static List<ModuleItem> ExtractItems(string source, B4xParser.B4XNode root)
        {
            var items = new List<ModuleItem>();
            if (root == null) return items;
            var lines = source?.Split(new[] { "\n" }, StringSplitOptions.None) ?? Array.Empty<string>();
            foreach (var node in root.Children)
                Walk(node, "", items, lines);
            return items;
        }

        private static void Walk(B4xParser.B4XNode node, string container, List<ModuleItem> items, string[] lines)
        {
            if (node == null) return;

            switch (node.Kind)
            {
                case "Region":
                    items.Add(MakeItem(ModuleItemKind.Region, node.Name, node, container, lines));
                    foreach (var c in node.Children)
                        Walk(c, node.Name, items, lines);
                    return;

                case "Sub":
                    if (node.Name == "Process_Globals" || node.Name == "Globals" || node.Name == "Class_Globals")
                        return;
                    items.Add(MakeItem(ModuleItemKind.Sub, node.Name, node, container, lines));
                    return;

                case "Type":
                    items.Add(MakeItem(ModuleItemKind.Type, node.Name, node, container, lines));
                    return;

                case "Process_Globals":
                case "Globals":
                case "Class_Globals":
                    SplitGlobalsVariables(node, lines, items);
                    return;
            }

            foreach (var c in node.Children)
                Walk(c, container, items, lines);
        }

        private static ModuleItem MakeItem(ModuleItemKind kind, string name, B4xParser.B4XNode node, string container, string[] lines)
        {
            int start = Math.Max(1, node.StartLine);
            int end = Math.Max(start, node.EndLine ?? start);
            return new ModuleItem
            {
                Kind = kind,
                Name = name,
                Signature = HeaderLine(lines, start) ?? name,
                StartLine = start,
                EndLine = end,
                Container = container
            };
        }

        private static void SplitGlobalsVariables(B4xParser.B4XNode globalsNode, string[] lines, List<ModuleItem> items)
        {
            if (globalsNode.EndLine == null) return;
            int endExclusive = globalsNode.EndLine.Value;
            for (int ln = globalsNode.StartLine + 1; ln < endExclusive; ln++)
            {
                int idx = ln - 1;
                if (idx < 0 || idx >= lines.Length) continue;
                var m = DimLineRe.Match(lines[idx]);
                if (!m.Success) continue;
                string name = m.Groups[1].Value;
                string type = m.Groups[2].Value.Trim();
                string init = m.Groups[3].Success ? m.Groups[3].Value.Trim() : "";
                items.Add(new ModuleItem
                {
                    Kind = ModuleItemKind.Variable,
                    Name = name,
                    Signature = $"Var {name} As {type}" + (init.Length > 0 ? $" = {init}" : ""),
                    StartLine = ln,
                    EndLine = ln,
                    Container = globalsNode.Kind
                });
            }
        }

        private static string HeaderLine(string[] lines, int line)
        {
            int idx = line - 1;
            if (idx < 0 || idx >= lines.Length) return null;
            var trimmed = lines[idx].Trim();
            return trimmed.Length > 0 ? trimmed : null;
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

```
dotnet test tests/B4XContext.Tests/B4XContext.Tests.csproj -c Release --filter B4xItemExtractorTests
```
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add Engine/B4xItemExtractor.cs tests/B4XContext.Tests/B4xItemExtractorTests.cs
git commit -m "feat: extract granular module items (subs, variables, types, regions)"
```

---

### Task 3: Engine granular builder

**Files:**
- Create: `Engine/B4xGranularBuilder.cs`
- Create: `tests/B4XContext.Tests/B4xGranularBuilderTests.cs`

**Interfaces:**
- Consumes: `ModuleItem` (from Task 1), `B4xItemExtractor.ExtractItems` (from Task 2).
- Produces: `static (string code, int estimatedChars) BuildCustom(string source, System.Collections.Generic.List<ModuleItem> items, string moduleName)`

- [ ] **Step 1: Write the failing test**

`tests/B4XContext.Tests/B4xGranularBuilderTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using B4XContext.Engine;
using B4XContext.Models;
using Xunit;

namespace B4XContext.Tests
{
    public class B4xGranularBuilderTests
    {
        private const string Sample = @"'Header comment
Sub Process_Globals
	Dim counter As Int
End Sub

Sub Foo(x As Int)
	Log(x)
End Sub

Private Sub Bar
	Log(""bar"")
End Sub

#Region R1
Sub Inside
	Log(""in"")
End Sub
#End Region";

        private static (string code, int chars) Build(System.Collections.Generic.List<ModuleItem> items, bool withRegion = false)
        {
            var src = Sample;
            if (withRegion) items.First(i => i.Kind == ModuleItemKind.Region).IsSelected = true;
            return B4xGranularBuilder.BuildCustom(src, items, "Mod.bas");
        }

        [Fact]
        public void Index_marks_selected_and_counts()
        {
            var items = B4xItemExtractor.ExtractItems(Sample, B4xParser.Parse(Sample).root);
            items.First(i => i.Name == "Foo").IsSelected = true;

            var (code, _) = BuildCustom(items, "Mod.bas");

            Assert.Contains("Index of 'Mod.bas': 4 items (1 selected)", code);
            Assert.Contains("- [x] Sub Foo(x As Int)", code);
            Assert.Contains("- [ ] ", code);
            Assert.DoesNotContain("SELECTED ITEMS", code.Take(code.IndexOf("## SELECTED ITEMS", System.StringComparison.Ordinal)).ToString());
        }

        [Fact]
        public void Emits_only_selected_item_bodies()
        {
            var items = B4xItemExtractor.ExtractItems(Sample, B4xParser.Parse(Sample).root);
            items.First(i => i.Name == "Foo").IsSelected = true;

            var (code, _) = BuildCustom(Sample, items, "Mod.bas");

            Assert.Contains("Log(x)", code);
            Assert.DoesNotContain("Log(\"bar\")", code);
        }

        [Fact]
        public void Selected_region_suppresses_contained_sub()
        {
            var items = B4xItemExtractor.ExtractItems(Sample, B4xParser.Parse(Sample).root);
            items.First(i => i.Name == "R1").IsSelected = true;

            var (code, _) = BuildCustom(Sample, items, "Mod.bas");

            Assert.Contains("Log(\"in\")", code); // via region body
            Assert.DoesNotContain("Sub Inside", code.Split("## SELECTED ITEMS")[1]); // no separate sub block
        }

        [Fact]
        public void Estimated_chars_positive_and_preamble_preserved()
        {
            var items = B4xItemExtractor.ExtractItems(Sample, B4xParser.Parse(Sample).root);
            items.First(i => i.Name == "Bar").IsSelected = true;

            var (code, chars) = BuildCustom(Sample, items, "Mod.bas");

            Assert.True(chars > 0);
            Assert.Contains("'Header comment", code);
        }
    }
}
```

Note: the `Index_marks_selected_and_counts` assertion that uses `code.Take(...).ToString()` is wrong (it stringifies a char iterator). Replace the final assertion in that test with:

```csharp
Assert.Contains("Index of 'Mod.bas': 4 items (1 selected)", code);
Assert.Contains("- [x] Sub Foo(x As Int)", code);
Assert.Contains("- [ ] ", code);
var sel = code.Split(new[] { "## SELECTED ITEMS" }, System.StringSplitOptions.None);
Assert.Single(sel[1].Split("```b4x", System.StringSplitOptions.None)) == false; // has at least one code block
```

- [ ] **Step 2: Run test to verify it fails**

```
dotnet test tests/B4XContext.Tests/B4XContext.Tests.csproj -c Release --filter B4xGranularBuilderTests
```
Expected: FAIL — `B4xGranularBuilder` missing (the throwaway assertion line above should be dropped — keep the test file clean: use only the corrected index test).

- [ ] **Step 3: Write minimal implementation**

`Engine/B4xGranularBuilder.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using B4XContext.Models;

namespace B4XContext.Engine
{
    public static class B4xGranularBuilder
    {
        public static (string code, int estimatedChars) BuildCustom(string source, List<ModuleItem> items, string moduleName)
        {
            var lines = source?.Split(new[] { "\n" }, StringSplitOptions.None) ?? Array.Empty<string>();
            var ordered = items.OrderBy(i => i.StartLine).ThenBy(i => (int)i.Kind).ToList();
            var sb = new StringBuilder();
            var selectedOrdered = ordered.Where(i => i.IsSelected).ToList();

            // Module preamble: everything before the first item.
            int firstLine = ordered.Count > 0 ? Math.Max(1, ordered[0].StartLine) : lines.Length + 1;
            int headerCount = Math.Max(0, firstLine - 1);
            for (int i = 0; i < headerCount && i < lines.Length; i++)
                sb.AppendLine(lines[i]);

            // Compact index of ALL items.
            sb.AppendLine();
            sb.AppendLine($"Index of '{moduleName}': {ordered.Count} items ({selectedOrdered.Count} selected)");
            foreach (var it in ordered)
                sb.AppendLine($"- [{(it.IsSelected ? "x" : " ")}] {it.Signature} — line {it.StartLine}");

            // Selected items, minus items inside selected regions.
            var regionNames = new HashSet<string>(selectedOrdered.Where(i => i.Kind == ModuleItemKind.Region).Select(i => i.Name));
            var toEmit = new List<ModuleItem>();
            foreach (var it in selectedOrdered)
            {
                if (it.Kind == ModuleItemKind.Region) { toEmit.Add(it); continue; }
                if (it.Container.Length > 0 && regionNames.Contains(it.Container)) continue;
                toEmit.Add(it);
            }

            sb.AppendLine();
            sb.AppendLine("## SELECTED ITEMS");
            foreach (var it in toEmit)
            {
                int start = Math.Max(1, Math.Min(it.StartLine, lines.Length));
                int end = Math.Max(start, Math.Min(it.EndLine, lines.Length));
                sb.AppendLine();
                sb.AppendLine(it.Signature);
                sb.AppendLine("```b4x");
                for (int i = start - 1; i < end && i < lines.Length; i++)
                    sb.AppendLine(lines[i]);
                sb.AppendLine("```");
            }

            return (sb.ToString(), sb.Length);
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

```
dotnet test tests/B4XContext.Tests/B4XContext.Tests.csproj -c Release --filter B4xGranularBuilderTests
```
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add Engine/B4xGranularBuilder.cs tests/B4XContext.Tests/B4xGranularBuilderTests.cs
git commit -m "feat: granular bundle builder (module index + selected items)"
```

---

### Task 4: BundleBuilder custom branch

**Files:**
- Modify: `Services/BundleBuilder.cs` (the `.bas` branch inside `BuildMarkdown`, ~lines 84-112)

**Interfaces:**
- Consumes: `FileMode.Custom`, `B4xGranularBuilder.BuildCustom`, `B4xItemExtractor.ExtractItems`, `B4xParser.Parse`.
- Produces: no new public API; changes `.bas` emission behavior for `Custom`.

- [ ] **Step 1: Write the failing test conceptually**

No unit test (needs UI/plain files); verified by Task 7 manual smoke + build. Run build to confirm the file still compiles after the logic change (Custom currently falls through to the Full branch — a latent bug the new branch fixes).

- [ ] **Step 2: Implement**

Replace the `.bas`/`else` block in `BundleBuilder.BuildMarkdown` (currently lines 84-112) with:

```csharp
else
{
    var txt = CodeUtils.ReadTextSafely(f.Path);
    if (f.Mode == FileMode.Full)
    {
        sb.AppendLine("```b4x");
        sb.AppendLine(txt);
        sb.AppendLine("```");
    }
    else if (f.Mode == FileMode.Custom)
    {
        var items = GetItems(f, txt);
        var (code, _) = Engine.B4xGranularBuilder.BuildCustom(txt, items, f.Name);
        sb.AppendLine("```markdown");
        sb.AppendLine(code);
        sb.AppendLine("```");
    }
    else // Skeleton
    {
        var keep = new List<string>();
        if (!string.IsNullOrEmpty(activeSub) && !string.IsNullOrEmpty(activeFile) && System.IO.Path.GetFullPath(activeFile) == System.IO.Path.GetFullPath(f.Path)) keep.Add(activeSub);
        var (root, issues) = Engine.B4xParser.Parse(txt);
        var nodes = Engine.B4xParser.FlattenSubsAndTypes(root);
        var snodes = nodes.Select(n => new Engine.SkeletonGenerator.Node
        {
            StartLine = n.StartLine,
            EndLine = n.EndLine,
            Kind = n.Kind,
            Name = n.Name,
            LeadingComment = n.LeadingComment
        }).ToList();
        var skeleton = Engine.SkeletonGenerator.GenerateModuleSkeleton(txt, snodes, keep);
        sb.AppendLine("```b4x");
        sb.AppendLine(skeleton);
        sb.AppendLine("```");
    }
}
```

Add the private helper at the end of the class (before the closing brace):

```csharp
private static System.Collections.Generic.List<B4XContext.Models.ModuleItem> GetItems(B4XContext.Models.ProjectFile f, string txt)
{
    if (f.Items.Count == 0)
    {
        var (root, _) = Engine.B4xParser.Parse(txt);
        foreach (var it in Engine.B4xItemExtractor.ExtractItems(txt, root))
            f.Items.Add(it);
    }
    return f.Items.ToList();
}
```

- [ ] **Step 3: Verify build**

```
dotnet build tests/B4XContext.Tests/B4XContext.Tests.csproj -c Release
```
Expected: 0 errors (this compiles both projects).

- [ ] **Step 4: Commit**

```bash
git add Services/BundleBuilder.cs
git commit -m "feat: emit granular custom modules in the bundle"
```

---

### Task 5: UI — expandable rows, 3-state toggle, item checkboxes

**Files:**
- Modify: `MainWindow.xaml` (files `ListView` DataTemplate, ~lines 98-143)
- Modify: `MainWindow.xaml.cs` (`ToggleMode_Click` ~447-456, new handlers, `EstimateTokensForFile` ~372-400, `UpdateEstimatedTokens` ~402-445)
- Modify: `App.xaml` (`ModeToggleButton` style, ~lines 410-428)

**Interfaces:**
- Consumes: `ProjectFile` new members (Task 1), `B4xParser`/`B4xItemExtractor` (Task 2), `B4xGranularBuilder` (Task 3).
- Produces: no new public API. New event handlers: `ToggleExpand_Click`, `Item_Checked`, `ResetCustom_Click`; updated `ToggleMode_Click` (3-state for `.bas`, 2-state otherwise).

- [ ] **Step 1: Implement XAML row template**

Replace the `FilesListView.ItemTemplate` content (the `<DataTemplate> … </DataTemplate>`, lines 99-142) with:

```xml
<DataTemplate>
    <Grid Margin="0,1,0,1">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
        </Grid.RowDefinitions>

        <!-- Main row -->
        <Grid Grid.Row="0" MinHeight="40">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="Auto" />
                <ColumnDefinition Width="Auto" />
                <ColumnDefinition Width="Auto" />
                <ColumnDefinition Width="*" />
                <ColumnDefinition Width="Auto" />
                <ColumnDefinition Width="Auto" />
            </Grid.ColumnDefinitions>

            <!-- Expand chevron (.bas only) -->
            <Button Grid.Column="0" VerticalAlignment="Center" Margin="2,0,0,0"
                    Padding="2,2" MinWidth="20" Background="Transparent" BorderThickness="0"
                    Click="ToggleExpand_Click" ToolTip="Select individual items">
                <Button.Style>
                    <Style TargetType="Button" BasedOn="{StaticResource {x:Type Button}}">
                        <Setter Property="Visibility" Value="Collapsed" />
                        <Setter Property="Content" Value="▸" />
                        <Style.Triggers>
                            <DataTrigger Binding="{Binding Kind}" Value="bas">
                                <Setter Property="Visibility" Value="Visible" />
                            </DataTrigger>
                            <DataTrigger Binding="{Binding IsExpanded}" Value="True">
                                <Setter Property="Content" Value="▾" />
                            </DataTrigger>
                        </Style.Triggers>
                    </Style>
                </Button.Style>
            </Button>

            <!-- Include checkbox -->
            <CheckBox Grid.Column="1" Grid.RowSpan="1"
                      IsChecked="{Binding Included, Mode=TwoWay}"
                      Margin="4,0,0,0" VerticalAlignment="Center"
                      Checked="FileInclude_Checked" Unchecked="FileInclude_Checked" />

            <!-- Extension badge -->
            <Border Grid.Column="2" Style="{StaticResource FileTypeBadge}" Margin="6,0,0,0">
                <TextBlock Text="{Binding Kind}" FontSize="8.5" FontWeight="Bold"
                           Foreground="#FF1A1C2A" VerticalAlignment="Center" HorizontalAlignment="Center" />
            </Border>

            <!-- Name + directory -->
            <StackPanel Grid.Column="3" Margin="8,3,6,3" VerticalAlignment="Center">
                <TextBlock Text="{Binding Name}" FontSize="13"
                           TextTrimming="CharacterEllipsis" VerticalAlignment="Center"
                           FontWeight="Medium" />
                <TextBlock Text="{Binding Directory}" Style="{StaticResource DirectoryTextStyle}"
                           Margin="0,1,0,0" />
            </StackPanel>

            <!-- Mode + reset -->
            <StackPanel Grid.Column="4" Orientation="Horizontal" Margin="0,0,4,0" VerticalAlignment="Center">
                <Button Content="{Binding Mode}" Click="ToggleMode_Click"
                        Margin="0,0,4,0" VerticalAlignment="Center"
                        Style="{StaticResource ModeToggleButton}" />
                <Button Content="↺" Click="ResetCustom_Click" ToolTip="Clear selection (back to Skeleton)"
                        VerticalAlignment="Center" Padding="4,2" FontSize="11"
                        Style="{StaticResource ReselectionButton}" />
            </StackPanel>
        </Grid>

        <!-- Expanded items area -->
        <Border Grid.Row="1" Margin="18,2,8,6" Padding="8,6"
                Background="{StaticResource InputBackgroundBrush}"
                BorderBrush="{StaticResource DividerBrush}" BorderThickness="1"
                CornerRadius="6">
            <Border.Style>
                <Style TargetType="Border">
                    <Setter Property="Visibility" Value="Collapsed" />
                    <Style.Triggers>
                        <DataTrigger Binding="{Binding IsExpanded}" Value="True">
                            <Setter Property="Visibility" Value="Visible" />
                        </DataTrigger>
                    </Style.Triggers>
                </Style>
            </Border.Style>
            <Border.Resources>
                <Style x:Key="ItemRowStyle" TargetType="Grid">
                    <Setter Property="Margin" Value="0,1,0,1" />
                </Style>
                <Style x:Key="GroupHeaderStyle" TargetType="TextBlock">
                    <Setter Property="FontSize" Value="10" />
                    <Setter Property="FontWeight" Value="Bold" />
                    <Setter Property="Foreground" Value="{StaticResource MutedTextBrush}" />
                    <Setter Property="Margin" Value="0,6,0,2" />
                </Style>
                <BooleanToVisibilityConverter x:Key="BoolToVis" />
            </Border.Resources>

            <StackPanel>
                <TextBlock Text="SUBS" Style="{StaticResource GroupHeaderStyle}"
                           Visibility="{Binding HasSubs, Converter={StaticResource BoolToVis}}" />
                <ItemsControl ItemsSource="{Binding ItemsSubs}">
                    <ItemsControl.ItemTemplate>
                        <DataTemplate>
                            <Grid Style="{StaticResource ItemRowStyle}">
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="Auto" />
                                    <ColumnDefinition Width="*" />
                                </Grid.ColumnDefinitions>
                                <CheckBox Grid.Column="0" IsChecked="{Binding IsSelected, Mode=TwoWay}"
                                          Tag="{Binding DataContext, RelativeSource={RelativeSource AncestorType={x:Type ListViewItem}}}"
                                          Checked="Item_Checked" Unchecked="Item_Checked"
                                          VerticalAlignment="Center" />
                                <TextBlock Grid.Column="1" Margin="6,0,0,0" VerticalAlignment="Center"
                                           FontFamily="Consolas" FontSize="11" Foreground="{StaticResource PrimaryTextBrush}"
                                           Text="{Binding Signature}" TextTrimming="CharacterEllipsis" />
                            </Grid>
                        </DataTemplate>
                    </ItemsControl.ItemTemplate>
                </ItemsControl>

                <TextBlock Text="VARIABLES" Style="{StaticResource GroupHeaderStyle}"
                           Visibility="{Binding HasVariables, Converter={StaticResource BoolToVis}}" />
                <ItemsControl ItemsSource="{Binding ItemsVariables}">
                    <ItemsControl.ItemTemplate>
                        <DataTemplate>
                            <Grid Style="{StaticResource ItemRowStyle}">
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="Auto" />
                                    <ColumnDefinition Width="*" />
                                </Grid.ColumnDefinitions>
                                <CheckBox Grid.Column="0" IsChecked="{Binding IsSelected, Mode=TwoWay}"
                                          Tag="{Binding DataContext, RelativeSource={RelativeSource AncestorType={x:Type ListViewItem}}}"
                                          Checked="Item_Checked" Unchecked="Item_Checked"
                                          VerticalAlignment="Center" />
                                <TextBlock Grid.Column="1" Margin="6,0,0,0" VerticalAlignment="Center"
                                           FontFamily="Consolas" FontSize="11" Foreground="{StaticResource PrimaryTextBrush}"
                                           Text="{Binding Signature}" TextTrimming="CharacterEllipsis" />
                            </Grid>
                        </DataTemplate>
                    </ItemsControl.ItemTemplate>
                </ItemsControl>

                <TextBlock Text="TYPES" Style="{StaticResource GroupHeaderStyle}"
                           Visibility="{Binding HasTypes, Converter={StaticResource BoolToVis}}" />
                <ItemsControl ItemsSource="{Binding ItemsTypes}">
                    <ItemsControl.ItemTemplate>
                        <DataTemplate>
                            <Grid Style="{StaticResource ItemRowStyle}">
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="Auto" />
                                    <ColumnDefinition Width="*" />
                                </Grid.ColumnDefinitions>
                                <CheckBox Grid.Column="0" IsChecked="{Binding IsSelected, Mode=TwoWay}"
                                          Tag="{Binding DataContext, RelativeSource={RelativeSource AncestorType={x:Type ListViewItem}}}"
                                          Checked="Item_Checked" Unchecked="Item_Checked"
                                          VerticalAlignment="Center" />
                                <TextBlock Grid.Column="1" Margin="6,0,0,0" VerticalAlignment="Center"
                                           FontFamily="Consolas" FontSize="11" Foreground="{StaticResource PrimaryTextBrush}"
                                           Text="{Binding Signature}" TextTrimming="CharacterEllipsis" />
                            </Grid>
                        </DataTemplate>
                    </ItemsControl.ItemTemplate>
                </ItemsControl>

                <TextBlock Text="REGIONS" Style="{StaticResource GroupHeaderStyle}"
                           Visibility="{Binding HasRegions, Converter={StaticResource BoolToVis}}" />
                <ItemsControl ItemsSource="{Binding ItemsRegions}">
                    <ItemsControl.ItemTemplate>
                        <DataTemplate>
                            <Grid Style="{StaticResource ItemRowStyle}">
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="Auto" />
                                    <ColumnDefinition Width="*" />
                                </Grid.ColumnDefinitions>
                                <CheckBox Grid.Column="0" IsChecked="{Binding IsSelected, Mode=TwoWay}"
                                          Tag="{Binding DataContext, RelativeSource={RelativeSource AncestorType={x:Type ListViewItem}}}"
                                          Checked="Item_Checked" Unchecked="Item_Checked"
                                          VerticalAlignment="Center" />
                                <TextBlock Grid.Column="1" Margin="6,0,0,0" VerticalAlignment="Center"
                                           FontFamily="Consolas" FontSize="11" Foreground="{StaticResource PrimaryTextBrush}"
                                           Text="{Binding Signature}" TextTrimming="CharacterEllipsis" />
                            </Grid>
                        </DataTemplate>
                    </ItemsControl.ItemTemplate>
                </ItemsControl>
            </StackPanel>
        </Border>
    </Grid>
</DataTemplate>
```

Note: `BooleanToVisibilityConverter` used above requires a resource; it ships with WPF so declare it once in `App.xaml` resources (`<BooleanToVisibilityConverter x:Key="BoolToVis" />`).

- [ ] **Step 2: App.xaml — mode button + converter + reset button styles**

App.xaml:
- Add near the top of `<Application.Resources>`:
  ```xml
  <BooleanToVisibilityConverter x:Key="BoolToVis" />
  ```
- In `ModeToggleButton` style triggers, add:
  ```xml
  <DataTrigger Binding="{Binding Mode}" Value="Custom">
      <Setter Property="Background" Value="{StaticResource StatusWarningBrush}" />
      <Setter Property="BorderBrush" Value="{StaticResource StatusWarningBrush}" />
  </DataTrigger>
  ```
- Add a `ReselectionButton` style (near the other button styles):
  ```xml
  <Style x:Key="ReselectionButton" TargetType="Button">
      <Setter Property="Visibility" Value="Collapsed" />
      <Setter Property="Background" Value="{StaticResource ButtonHoverBrush}" />
      <Setter Property="Foreground" Value="{StaticResource MutedTextBrush}" />
      <Setter Property="BorderBrush" Value="{StaticResource CardBorderBrush}" />
      <Setter Property="BorderThickness" Value="1" />
      <Setter Property="Padding" Value="5,2" />
      <Setter Property="FontSize" Value="11" />
      <Setter Property="Cursor" Value="Hand" />
      <Style.Triggers>
          <DataTrigger Binding="{Binding Mode}" Value="Custom">
              <Setter Property="Visibility" Value="Visible" />
          </DataTrigger>
      </Style.Triggers>
  </Style>
  ```

- [ ] **Step 3: Code-behind handlers**

`MainWindow.xaml.cs`:

Replace `ToggleMode_Click` (lines 447-456):

```csharp
private void ToggleMode_Click(object sender, RoutedEventArgs e)
{
    if (sender is System.Windows.Controls.Button btn && btn.DataContext is ProjectFile pf)
    {
        if (pf.Kind == "bas" && pf.Items.Count > 0)
        {
            pf.Mode = pf.Mode switch
            {
                B4XContext.Models.FileMode.Full => B4XContext.Models.FileMode.Skeleton,
                B4XContext.Models.FileMode.Skeleton => B4XContext.Models.FileMode.Custom,
                _ => B4XContext.Models.FileMode.Full
            };
            if (pf.Mode == B4XContext.Models.FileMode.Custom && pf.Items.Count == 0) EnsureItems(pf);
        }
        else
        {
            pf.Mode = pf.Mode == B4XContext.Models.FileMode.Skeleton
                ? B4XContext.Models.FileMode.Full
                : B4XContext.Models.FileMode.Skeleton;
        }
        FilesListView.Items.Refresh();
        UpdateEstimatedTokens();
    }
}
```

New handlers (add near `ToggleMode_Click`):

```csharp
private void ToggleExpand_Click(object sender, RoutedEventArgs e)
{
    if (sender is System.Windows.Controls.Button btn && btn.DataContext is ProjectFile pf)
    {
        pf.IsExpanded = !pf.IsExpanded;
        if (pf.IsExpanded) EnsureItems(pf);
        FilesListView.Items.Refresh();
        UpdateEstimatedTokens();
    }
}

private void EnsureItems(ProjectFile pf)
{
    try
    {
        if (pf.Kind != "bas" || pf.Items.Count > 0) return;
        var txt = CodeUtils.ReadTextSafely(pf.Path);
        var (root, _) = B4xParser.Parse(txt);
        foreach (var it in B4xItemExtractor.ExtractItems(txt, root)) pf.Items.Add(it);
    }
    catch { }
}

private void Item_Checked(object sender, RoutedEventArgs e)
{
    if (sender is System.Windows.Controls.CheckBox cb && cb.Tag is ProjectFile pf)
    {
        pf.Mode = B4XContext.Models.FileMode.Custom;
        FilesListView.Items.Refresh();
        UpdateEstimatedTokens();
    }
}

private void ResetCustom_Click(object sender, RoutedEventArgs e)
{
    if (sender is System.Windows.Controls.Button btn && btn.DataContext is ProjectFile pf)
    {
        pf.ResetCustom();
        FilesListView.Items.Refresh();
        UpdateEstimatedTokens();
    }
}
```

Import note: `CodeUtils` and `B4xItemExtractor` come from `B4XContext.Services` / `B4XContext.Engine`, both already imported at the top of `MainWindow.xaml.cs`.

- [ ] **Step 4: Build**

```
dotnet build tests/B4XContext.Tests/B4XContext.Tests.csproj -c Release
```
Expected: 0 errors (XAML compiles).

- [ ] **Step 5: Commit**

```bash
git add MainWindow.xaml MainWindow.xaml.cs App.xaml
git commit -m "feat: expandable file rows with per-item granular selection (3-state mode)"
```

---

### Task 6: Custom token estimation

**Files:**
- Modify: `MainWindow.xaml.cs` (`EstimateTokensForFile` — lines 372-400)

- [ ] **Step 1: Implement**

In `EstimateTokensForFile`, before the `if (f.Mode == Skeleton)` branch, the `Full` case is the trailing `else`. Rewrite the method body:

```csharp
private int EstimateTokensForFile(ProjectFile f)
{
    try
    {
        if (f == null || !System.IO.File.Exists(f.Path)) return 0;
        var txt = CodeUtils.ReadTextSafely(f.Path);
        if (f.Mode == B4XContext.Models.FileMode.Skeleton)
        {
            // generate skeleton and estimate size
            var (root, issues) = B4xParser.Parse(txt);
            var nodes = B4xParser.FlattenSubsAndTypes(root);
            var snodes = nodes.Select(n => new SkeletonGenerator.Node
            {
                StartLine = n.StartLine,
                EndLine = n.EndLine,
                Kind = n.Kind,
                Name = n.Name,
                LeadingComment = n.LeadingComment
            }).ToList();
            var skeleton = SkeletonGenerator.GenerateModuleSkeleton(txt, snodes, Enumerable.Empty<string>());
            return Math.Max(0, skeleton.Length / 4);
        }
        else if (f.Mode == B4XContext.Models.FileMode.Custom)
        {
            if (f.Items.Count == 0) EnsureItems(f);
            var (_, chars) = Engine.B4xGranularBuilder.BuildCustom(txt, f.Items.ToList(), f.Name);
            return Math.Max(0, chars / 4);
        }
        else
        {
            return Math.Max(0, txt.Length / 4);
        }
    }
    catch { return 0; }
}
```

Note: `MainWindow.xaml.cs` already imports `B4XContext.Models` as a namespace and currently resolves `FileMode.Skeleton` as `B4XContext.Models.FileMode.Skeleton` via the `B4XContext.Models;` using — keep either spelling; the compiled code above uses the fully-qualified form to be unambiguous. `EnsureItems` is defined in Task 5.

- [ ] **Step 2: Build + test**

```
dotnet build tests/B4XContext.Tests/B4XContext.Tests.csproj -c Release
dotnet test  tests/B4XContext.Tests/B4XContext.Tests.csproj -c Release
```
Expected: 0 errors; all tests pass.

- [ ] **Step 3: Commit**

```bash
git add MainWindow.xaml.cs
git commit -m "feat: estimate tokens for custom granular mode"
```

---

### Task 7: Manual smoke + final verification

**Files:** none (manual QA)

- [ ] **Step 1: Publish-free manual run**

```
dotnet run --project b4x_context.csproj -c Release
```

- [ ] **Step 2: Manual checks**

1. Scan a real B4X project folder.
2. Click a `.bas` chevron → row expands showing SUBS / VARIABLES / TYPES / REGIONS with signatures.
3. Check a Sub and a variable → mode button shows `Custom`, `↺` appears.
4. Type a task, click **GENERATE PROMPT**, paste clipboard — confirm the output contains: module preamble, `Index of '…': N items (M selected)` with `[x]`/`[ ]` markers, and **## SELECTED ITEMS** with only the chosen code.
5. Select a Region containing subs → confirm subs are not emitted separately.
6. Click `↺` → mode returns to `Skeleton`, tokens recompute.
7. Toggle `Full` then `Skeleton` on a layout (`.bal`) file → unchanged two-state behavior.
8. Run full test suite once more:

```
dotnet test tests/B4XContext.Tests/B4XContext.Tests.csproj -c Release
```

- [ ] **Step 3: Commit any fixups**

If manual smoke reveals bugs, fix them in small commits as part of this task (engine/edge cases), then re-run build + tests. Final commit message style: `fix: …` describing what was corrected.

---

## Self-Review notes

- **Index marker test** (`Task 3`): the plan's first draft of `Index_marks_selected_and_counts` contained a broken `.Take().ToString()` assertion; corrected in-place to assert the `## SELECTED ITEMS` split contains a code block.
- **Spec coverage:** items (Sub/Var/Type/Region) ✓ Task 2; index always ✓ Task 3; selected full code ✓ Task 3; dedup region ✓ Task 3; Full/Skeleton untouched ✓ Task 4; 3-state toggle ✓ Task 5; expandable rows ✓ Task 5; token estimate ✓ Task 6; layouts untouched ✓ Task 4/5; verification ✓ Task 7.
- **Type consistency:** `BuildCustom` returns `(string code, int estimatedChars)` — used identically in BundleBuilder (Task 4) and EstimateTokensForFile (Task 6). `ModuleItem.Group` used in tests only (display uses group headers instead) — kept per spec.