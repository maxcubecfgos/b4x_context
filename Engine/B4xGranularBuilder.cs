using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using B4XContext.Models;

namespace B4XContext.Engine
{
    public static class B4xGranularBuilder
    {
        public static (string code, int estimatedChars) BuildCustom(string source, List<ModuleItem> items, string moduleName,
            int maxHeaderLines = 40)
        {
            var lines = (source ?? "").Split(new[] { "\n" }, StringSplitOptions.None);
            var ordered = items.OrderBy(i => i.StartLine).ThenBy(i => i.Kind).ToList();

            // A selected Region (B4X) or Type (Dart class/mixin/enum/...) already covers every
            // item declared inside it, so its members are not emitted twice.
            var regionNames = new HashSet<string>(
                ordered.Where(i => i.IsSelected && (i.Kind == ModuleItemKind.Region || i.Kind == ModuleItemKind.Type)).Select(i => i.Name),
                StringComparer.OrdinalIgnoreCase);

            var toEmit = new List<ModuleItem>();
            foreach (var it in ordered)
            {
                if (!it.IsSelected) continue;
                if (it.Kind == ModuleItemKind.Region)
                {
                    toEmit.Add(it);
                    continue;
                }
                if (it.Container.Length > 0 && regionNames.Contains(it.Container)) continue;
                toEmit.Add(it);
            }

            int firstItemLine = ordered.Count > 0 ? ordered.Min(i => i.StartLine) : lines.Length + 1;
            int headerCount = Math.Min(Math.Max(0, firstItemLine - 1), maxHeaderLines);
            var sb = new StringBuilder();
            for (int li = 0; li < headerCount && li < lines.Length; li++)
                sb.AppendLine(lines[li]);

            string index = BuildIndex(items, moduleName);
            if (index.Length > 0)
            {
                sb.AppendLine();
                sb.AppendLine(index);
            }

            if (toEmit.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("## SELECTED ITEMS");
                sb.AppendLine();
                foreach (var it in toEmit)
                {
                    AppendBlock(sb, lines, it.StartLine, it.EndLine);
                    sb.AppendLine();
                }
            }

            sb.AppendLine("<- EOF module ->");
            return (sb.ToString(), sb.Length);
        }

        public static string BuildIndex(IEnumerable<ModuleItem> items, string moduleName)
        {
            var list = items.OrderBy(i => i.StartLine).ThenBy(i => i.Kind).ToList();
            var sel = list.Where(i => i.IsSelected).ToList();
            if (sel.Count == 0) return "";
            var sb = new StringBuilder();
            sb.AppendLine($"### Index of '{moduleName}': {list.Count} items ({sel.Count} selected)");
            foreach (var it in sel)
            {
                string sig = string.IsNullOrEmpty(it.Signature) ? it.Name : it.Signature;
                string line = $"  - [x] {sig} — line {it.StartLine}";
                if (it.Container.Length > 0) line += $" (in {it.Container})";
                sb.AppendLine(line);
            }
            return sb.ToString().TrimEnd();
        }

        private static void AppendBlock(StringBuilder sb, string[] lines, int startLine, int endLine)
        {
            int from = Math.Max(0, startLine - 1);
            int to = Math.Min(lines.Length - 1, endLine - 1);
            for (int li = from; li <= to; li++)
                sb.AppendLine(lines[li]);
        }
    }
}