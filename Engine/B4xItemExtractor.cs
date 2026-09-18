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