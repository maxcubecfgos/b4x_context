using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace B4XContext.Engine
{
    /// <summary>
    /// Smart skeleton generator for B4X modules.
    /// Produces压缩 skeleton output keeping only structural elements:
    /// signatures, Dim declarations, leading comments, region markers.
    /// Bodies are compressed to "... N lines omitted" with optional keep-full override.
    /// </summary>
    public static class SkeletonGenerator
    {
        private const int MAX_SKELETON_LINES = 200;
        private const int MAX_SKELETON_CHARS = 8000;

        private static readonly Regex DimLineRe = new Regex(
            @"^\s*(?:Private\s+|Public\s+)?Dim\s+",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex CommentLineRe = new Regex(
            @"^\s*'",
            RegexOptions.Compiled);

        public class Node
        {
            public int StartLine { get; set; }
            public int? EndLine { get; set; }
            public string Kind { get; set; }
            public string Name { get; set; }
            public string LeadingComment { get; set; }
            public string Params { get; set; }
            public string ReturnType { get; set; }
        }

        public class SkeletonResult
        {
            public string Skeleton { get; set; }
            public int OriginalLines { get; set; }
            public int SkeletonLines { get; set; }
            public double CompressionRatio => OriginalLines == 0 ? 0 : (double)(OriginalLines - SkeletonLines) / OriginalLines;
        }

        /// <summary>
        /// Generate a smart skeleton for a B4X module.
        /// </summary>
        /// <param name="source">Original source code</param>
        /// <param name="nodes">Parsed AST nodes</param>
        /// <param name="keepFullNames">Sub/Type names to keep full bodies for</param>
        /// <returns>SkeletonResult with skeleton text and stats</returns>
        public static SkeletonResult GenerateSkeletonResult(string source, IEnumerable<Node> nodes, IEnumerable<string> keepFullNames = null)
        {
            var keepSet = new HashSet<string>((keepFullNames ?? Enumerable.Empty<string>()).Select(n => n.ToLowerInvariant()));
            var lines = source?.Split(new[] { "\n" }, StringSplitOptions.None) ?? new string[0];
            int originalLines = lines.Length;

            if (nodes == null || !nodes.Any())
                return new SkeletonResult { Skeleton = source ?? "", OriginalLines = originalLines, SkeletonLines = originalLines };

            var outLines = new List<string>();
            int lastEmittedLine = 0;
            int nLines = lines.Length;

            foreach (var node in nodes)
            {
                int start = Math.Max(1, Math.Min(node.StartLine, nLines));
                int end = Math.Max(start, Math.Min(node.EndLine ?? start, nLines));

                // Emit any lines between previous node and this one (module-level code, comments)
                if (start - 1 > lastEmittedLine)
                {
                    for (int i = lastEmittedLine; i < start - 1; i++)
                    {
                        var line = lines[i];
                        if (!string.IsNullOrWhiteSpace(line))
                            outLines.Add(line);
                    }
                }

                bool alwaysFull = node.Kind == "Process_Globals" || node.Kind == "Globals" || node.Kind == "Class_Globals";
                bool keepFull = alwaysFull || keepSet.Contains((node.Name ?? string.Empty).ToLowerInvariant());

                if (keepFull)
                {
                    // Emit full body
                    for (int i = start - 1; i < end; i++)
                        outLines.Add(lines[i]);
                }
                else
                {
                    // Smart skeleton based on kind
                    switch (node.Kind)
                    {
                        case "Sub":
                            EmitSubSkeleton(outLines, lines, node, start, end);
                            break;
                        case "Type":
                            EmitTypeSkeleton(outLines, lines, node, start, end);
                            break;
                        case "Region":
                            EmitRegionSkeleton(outLines, lines, node, start, end);
                            break;
                        case "Process_Globals":
                        case "Globals":
                        case "Class_Globals":
                            EmitGlobalsSkeleton(outLines, lines, node, start, end);
                            break;
                        default:
                            EmitGenericSkeleton(outLines, lines, node, start, end);
                            break;
                    }
                }

                lastEmittedLine = end;
            }

            // Emit remaining lines after last node
            if (lastEmittedLine < nLines)
            {
                for (int i = lastEmittedLine; i < nLines; i++)
                {
                    var line = lines[i];
                    if (!string.IsNullOrWhiteSpace(line))
                        outLines.Add(line);
                }
            }

            // Cap output
            var result = CapOutput(outLines);

            return new SkeletonResult
            {
                Skeleton = result,
                OriginalLines = originalLines,
                SkeletonLines = result.Split('\n').Length
            };
        }

        /// <summary>
        /// Legacy API: Generate skeleton string (backward compatible).
        /// </summary>
        public static string GenerateModuleSkeleton(string source, IEnumerable<Node> nodes, IEnumerable<string> keepFullNames = null)
        {
            return GenerateSkeletonResult(source, nodes, keepFullNames).Skeleton;
        }

        private static void EmitSubSkeleton(List<string> outLines, string[] lines, Node node, int start, int end)
        {
            // Emit leading comment if present
            EmitLeadingComment(outLines, node);

            // Emit the Sub signature (first line)
            int headerIdx = Math.Max(0, start - 1);
            var headerLine = lines[headerIdx];
            outLines.Add(headerLine);

            // Extract Dim declarations from the body
            var dimLines = new List<string>();
            for (int i = start; i < end; i++)
            {
                var line = lines[i];
                if (DimLineRe.IsMatch(line))
                    dimLines.Add(line.TrimEnd());
            }

            if (dimLines.Count > 0)
            {
                outLines.Add("\t' --- Variables ---");
                foreach (var d in dimLines)
                    outLines.Add("\t" + d.TrimStart());
            }

            // Emit body compression marker
            int bodyLineCount = Math.Max(0, (end - start + 1) - 1); // -1 for header
            if (dimLines.Count > 0)
                bodyLineCount -= dimLines.Count + 1; // account for dim lines + separator

            if (bodyLineCount > 0)
                outLines.Add($"\t'... ({bodyLineCount} lines omitted)...");

            outLines.Add("End Sub");
        }

        private static void EmitTypeSkeleton(List<string> outLines, string[] lines, Node node, int start, int end)
        {
            EmitLeadingComment(outLines, node);

            // Emit Type declaration line
            int headerIdx = Math.Max(0, start - 1);
            outLines.Add(lines[headerIdx]);

            // For single-line Type, just add closer
            if (start == end)
            {
                outLines.Add("End Type");
                return;
            }

            // Extract field declarations (Dim lines) from Type body
            var fieldLines = new List<string>();
            for (int i = start; i < end; i++)
            {
                var line = lines[i];
                var trimmed = line.Trim();
                // Type fields are typically Dim declarations or simple assignments
                if (DimLineRe.IsMatch(line) || trimmed.StartsWith("Public ") || trimmed.StartsWith("Private "))
                    fieldLines.Add(trimmed);
            }

            if (fieldLines.Count > 0)
            {
                foreach (var f in fieldLines)
                    outLines.Add("\t" + f);
            }
            else
            {
                // No recognized fields, show line count
                int bodyLineCount = Math.Max(0, (end - start + 1) - 2);
                if (bodyLineCount > 0)
                    outLines.Add($"\t'... ({bodyLineCount} lines omitted)...");
            }

            outLines.Add("End Type");
        }

        private static void EmitRegionSkeleton(List<string> outLines, string[] lines, Node node, int start, int end)
        {
            // Keep region markers as section dividers
            int headerIdx = Math.Max(0, start - 1);
            outLines.Add(lines[headerIdx]);

            // Emit body lines that are meaningful (comments, non-empty)
            for (int i = start; i < end; i++)
            {
                var line = lines[i];
                if (!string.IsNullOrWhiteSpace(line))
                    outLines.Add(line);
            }

            // Close region
            if (end < lines.Length)
                outLines.Add(lines[end - 1]); // #End Region
        }

        private static void EmitGlobalsSkeleton(List<string> outLines, string[] lines, Node node, int start, int end)
        {
            EmitLeadingComment(outLines, node);

            // Emit the Sub declaration
            int headerIdx = Math.Max(0, start - 1);
            var headerLine = lines[headerIdx];
            outLines.Add(headerLine);

            // Extract Dim declarations from globals
            var dimLines = new List<string>();
            for (int i = start; i < end; i++)
            {
                var line = lines[i];
                if (DimLineRe.IsMatch(line))
                    dimLines.Add(line.TrimEnd());
            }

            if (dimLines.Count > 0)
            {
                foreach (var d in dimLines)
                    outLines.Add("\t" + d.TrimStart());
            }

            outLines.Add("End Sub");
        }

        private static void EmitGenericSkeleton(List<string> outLines, string[] lines, Node node, int start, int end)
        {
            // Emit first line as header
            int headerIdx = Math.Max(0, start - 1);
            outLines.Add(lines[headerIdx]);

            int bodyLineCount = Math.Max(0, (end - start + 1) - 1);
            if (bodyLineCount > 0)
                outLines.Add($"\t'... ({bodyLineCount} lines omitted)...");

            // Emit closer if it exists
            if (end <= lines.Length)
                outLines.Add(lines[end - 1]);
        }

        private static void EmitLeadingComment(List<string> outLines, Node node)
        {
            if (!string.IsNullOrEmpty(node.LeadingComment))
            {
                foreach (var c in node.LeadingComment.Split('\n'))
                {
                    var trimmed = c.Trim();
                    if (!string.IsNullOrWhiteSpace(trimmed))
                        outLines.Add(trimmed);
                }
            }
        }

        private static string CapOutput(List<string> outLines)
        {
            // Remove consecutive empty lines
            var cleaned = new List<string>();
            bool prevEmpty = false;
            foreach (var line in outLines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    if (!prevEmpty && cleaned.Count > 0)
                        cleaned.Add("");
                    prevEmpty = true;
                }
                else
                {
                    cleaned.Add(line);
                    prevEmpty = false;
                }
            }

            // Truncate by line count
            bool truncated = false;
            if (cleaned.Count > MAX_SKELETON_LINES)
            {
                cleaned = cleaned.Take(MAX_SKELETON_LINES).ToList();
                truncated = true;
            }

            // Truncate by char count
            var result = string.Join("\n", cleaned);
            if (result.Length > MAX_SKELETON_CHARS)
            {
                result = result.Substring(0, MAX_SKELETON_CHARS);
                // Find last newline to avoid cutting mid-line
                int lastNl = result.LastIndexOf('\n');
                if (lastNl > 0)
                    result = result.Substring(0, lastNl);
                truncated = true;
            }

            if (truncated)
                result += "\n'... (skeleton truncated)...";

            return result;
        }
    }
}
