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
                var tree = BuildAsciiTree(files.Select(f => f.Path));
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
                    if (f.Kind == "bal")
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

        public static string BuildAsciiTree(IEnumerable<string> paths)
        {
            // Build a simple ASCII tree grouped by common root
            var grouped = paths.Select(p => p.Replace('\\', '/')).ToList();
            var sb = new StringBuilder();
            foreach (var p in grouped.OrderBy(p => p))
            {
                sb.AppendLine(p);
            }
            return sb.ToString();
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
