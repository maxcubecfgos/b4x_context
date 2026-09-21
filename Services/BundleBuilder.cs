using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SW = System.Windows;
using B4XContext.Models;
using B4XContext.Engine;
using FileMode = B4XContext.Models.FileMode;

namespace B4XContext.Services
{
    public static class BundleBuilder
    {
        private const int MAX_TREE_LINES = 4000;

        public static string BuildMarkdown(string preamble, string task, IEnumerable<ProjectFile> files, bool includeFileTree = true,
            string activeCode = null, string activeFile = null, string activeSub = null, string compileErrors = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Context Bundle");
            sb.AppendLine();

            if (!string.IsNullOrEmpty(compileErrors))
            {
                sb.AppendLine(compileErrors);
                sb.AppendLine("---");
            }

            sb.AppendLine("## PREAMBLE / CONTEXT");
            sb.AppendLine();
            sb.AppendLine(string.IsNullOrWhiteSpace(preamble) ? "(none)" : preamble);
            sb.AppendLine();

            sb.AppendLine("## TASK / QUERY");
            sb.AppendLine();
            sb.AppendLine(string.IsNullOrWhiteSpace(task) ? "(none)" : task);
            sb.AppendLine();

            if (includeFileTree)
            {
                sb.AppendLine("## FILE TREE");
                sb.AppendLine();
                var tree = BuildAsciiTree(files);
                sb.AppendLine("```");
                sb.AppendLine(tree);
                sb.AppendLine("```");
                sb.AppendLine();
            }

            sb.AppendLine("## FILES");
            sb.AppendLine();
            // Active code first
            if (!string.IsNullOrEmpty(activeCode))
            {
                var tag = activeCode.TrimStart().StartsWith("{") || activeCode.TrimStart().StartsWith("[") ? "json" : "b4x";
                var title = "PRIMARY ACTIVE TARGET";
                if (!string.IsNullOrEmpty(activeSub)) title += $" - Sub {activeSub}";
                if (!string.IsNullOrEmpty(activeFile)) title += $" ({System.IO.Path.GetFileName(activeFile)})";
                sb.AppendLine($"\n### {title}\n```{tag}\n{activeCode}\n```");
            }

            foreach (var f in files.Where(f => f.Included))
            {
                sb.AppendLine($"### {f.Name}   ({f.Mode})");
                sb.AppendLine();
                try
                {
                    if (f.Kind == "bal" || f.Kind == "bjl" || f.Kind == "bil")
                    {
                        var data = System.IO.File.ReadAllBytes(f.Path);
                        var decoded = Engine.BalDecoder.Decode(data, full: f.Mode == FileMode.Full);
                        if (f.Mode == FileMode.Skeleton)
                        {
                            sb.AppendLine("```text");
                            sb.AppendLine(decoded);
                            sb.AppendLine("```");
                        }
                        else
                        {
                            sb.AppendLine("```json");
                            sb.AppendLine(decoded);
                            sb.AppendLine("```");
                        }
                    }
                    else if (f.IsGenericText)
                    {
                        var txt = CodeUtils.ReadTextSafely(f.Path);
                        var fence = LangSupport.FenceTagFor(f.Kind);
                        if (f.Mode == FileMode.Skeleton)
                        {
                            var skeleton = Engine.MultiLangSkeletonizer.Skeletonize(txt, f.Kind).Skeleton;
                            sb.AppendLine($"```{fence}");
                            sb.AppendLine(skeleton);
                            sb.AppendLine("```");
                        }
                        else if (f.Mode == FileMode.Custom && f.Kind == "dart")
                        {
                            // Dart supports the same granular per-item selection as B4X modules.
                            var dartItems = GetItems(f, txt);
                            var (customCode, _) = Engine.B4xGranularBuilder.BuildCustom(txt, dartItems, f.Name, int.MaxValue);
                            sb.AppendLine($"```{fence}");
                            sb.AppendLine(customCode);
                            sb.AppendLine("```");
                        }
                        else
                        {
                            sb.AppendLine($"```{fence}");
                            sb.AppendLine(txt);
                            sb.AppendLine("```");
                        }
                    }
                    else
                    {
                        var txt = CodeUtils.ReadTextSafely(f.Path);
                        if (f.Mode == FileMode.Skeleton)
                        {
                            // Parse and create skeleton using parser nodes; keep active Sub full
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
                        else if (f.Mode == FileMode.Custom)
                        {
                            var items = GetItems(f, txt);
                            var (customCode, _) = Engine.B4xGranularBuilder.BuildCustom(txt, items, f.Name);
                            sb.AppendLine("```b4x");
                            sb.AppendLine(customCode);
                            sb.AppendLine("```");
                        }
                        else
                        {
                            sb.AppendLine("```b4x");
                            sb.AppendLine(txt);
                            sb.AppendLine("```");
                        }
                    }
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"(Could not read file: {ex.Message})");
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }

        public static List<ModuleItem> GetItems(ProjectFile f, string txt)
        {
            if (!f.HasItems)
            {
                List<ModuleItem> extracted;
                if (f.Kind == "dart")
                {
                    extracted = Engine.DartItemExtractor.ExtractItems(txt);
                }
                else
                {
                    var (root, _) = Engine.B4xParser.Parse(txt);
                    extracted = Engine.B4xItemExtractor.ExtractItems(txt, root);
                }

                foreach (var it in extracted) f.Items.Add(it);
            }
            return f.Items.ToList();
        }

        public static string DisplayPath(ProjectFile f)
        {
            if (string.IsNullOrEmpty(f.RelativeDirectory))
                return f.Name;
            return f.RelativeDirectory + "/" + f.Name;
        }

        private sealed class TreeNode
        {
            public readonly SortedDictionary<string, TreeNode> Children = new SortedDictionary<string, TreeNode>(StringComparer.OrdinalIgnoreCase);
            public ProjectFile File;
        }

        public static string BuildAsciiTree(IEnumerable<ProjectFile> files)
        {
            var root = new TreeNode();
            foreach (var f in files)
            {
                var parts = DisplayPath(f).Replace('\\', '/').Split('/');
                var node = root;
                for (int i = 0; i < parts.Length; i++)
                {
                    if (!node.Children.TryGetValue(parts[i], out var next))
                    {
                        next = new TreeNode();
                        node.Children.Add(parts[i], next);
                    }
                    node = next;
                }
                node.File = f;
            }

            var lines = new List<string>();
            bool truncated = false;
            RenderTree(root, null, "", true, true, lines, ref truncated);
            if (truncated)
                lines.Add("... tree truncated ...");
            return string.Join("\n", lines);
        }

        private static void RenderTree(TreeNode node, string name, string prefix, bool isLast, bool isRoot, List<string> lines, ref bool truncated)
        {
            if (truncated) return;

            if (!isRoot)
            {
                if (lines.Count >= MAX_TREE_LINES)
                {
                    truncated = true;
                    return;
                }
                var connector = isLast ? "\\- " : "|- ";
                var stats = node.File != null
                    ? $" ({FormatSize(node.File.Size)}, {node.File.LineCount} lines)"
                    : "";
                lines.Add($"{prefix}{connector}{name}{stats}");
            }

            if (truncated) return;

            var keys = node.Children.Keys.ToList();
            for (int i = 0; i < keys.Count; i++)
            {
                var key = keys[i];
                var child = node.Children[key];
                var childLast = i == keys.Count - 1;
                var childPrefix = isRoot ? "" : prefix + (isLast ? "   " : "|  ");
                RenderTree(child, key, childPrefix, childLast, false, lines, ref truncated);
                if (truncated) break;
            }
        }

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{(bytes / 1024):0} KB";
            return $"{(bytes / (1024.0 * 1024.0)):0.0} MB";
        }

        public static void CopyToClipboard(string text)
        {
            try
            {
                SW.Clipboard.SetText(text);
            }
            catch { }
        }
    }
}
