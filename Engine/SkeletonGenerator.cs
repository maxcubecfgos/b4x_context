using System;
using System.Collections.Generic;
using System.Linq;

namespace B4XContext.Engine
{
    /// <summary>
    /// Port of b4x_skeleton.generate_module_skeleton from Python.
    /// Produces a skeleton version of a B4X module, keeping full bodies for requested names.
    /// </summary>
    public static class SkeletonGenerator
    {
        // The Python implementation relies on a parser that returns nodes with
        // start_line, end_line, kind, name, leading_comment. Here we implement
        // a helper that receives those nodes via a lightweight interface.

        public class Node
        {
            public int StartLine { get; set; }
            public int? EndLine { get; set; }
            public string Kind { get; set; }
            public string Name { get; set; }
            public string LeadingComment { get; set; }
        }

        public static string GenerateModuleSkeleton(string source, IEnumerable<Node> nodes, IEnumerable<string> keepFullNames = null)
        {
            var keepSet = new HashSet<string>((keepFullNames ?? Enumerable.Empty<string>()).Select(n => n.ToLowerInvariant()));
            var lines = source?.Split(new[] {"\n"}, StringSplitOptions.None) ?? new string[0];
            if (nodes == null || !nodes.Any())
                return source;

            var outLines = new List<string>();
            int lastEmittedLine = 0;
            int nLines = lines.Length;

            foreach (var node in nodes)
            {
                int start = Math.Max(1, Math.Min(node.StartLine, nLines));
                int end = Math.Max(start, Math.Min(node.EndLine ?? start, nLines));

                if (start - 1 > lastEmittedLine)
                {
                    for (int i = lastEmittedLine; i < start - 1; i++)
                        outLines.Add(lines[i]);
                }

                string headerLine = start - 1 < nLines ? lines[start - 1] : string.Empty;
                bool alwaysFull = node.Kind == "Process_Globals" || node.Kind == "Globals" || node.Kind == "Class_Globals" || node.Kind == "Type";
                bool keepFull = alwaysFull || keepSet.Contains((node.Name ?? string.Empty).ToLowerInvariant());

                if (keepFull)
                {
                    for (int i = start - 1; i < end; i++)
                        outLines.Add(lines[i]);
                }
                else
                {
                    if (node.Kind == "Process_Globals" || node.Kind == "Globals" || node.Kind == "Class_Globals")
                    {
                        var header = $"Sub {node.Kind.Replace('_', ' ')}";
                        outLines.Add(header);
                    }
                    else
                    {
                        outLines.Add(headerLine);
                    }

                    int bodyLineCount = Math.Max(0, (end - start + 1) - 2);
                    if (!string.IsNullOrEmpty(node.LeadingComment))
                    {
                        foreach (var c in node.LeadingComment.Split('\n'))
                            outLines.Add("\t" + c);
                    }

                    outLines.Add($"\t'... ({bodyLineCount} lines omitted, use keep_full to see the full body) ...");
                    string closer = node.Kind == "Sub" || node.Kind == "Process_Globals" || node.Kind == "Globals" || node.Kind == "Class_Globals" ? "End Sub" : "End Type";
                    outLines.Add(closer);
                }

                lastEmittedLine = end;
            }

            if (lastEmittedLine < nLines)
            {
                for (int i = lastEmittedLine; i < nLines; i++)
                    outLines.Add(lines[i]);
            }

            return string.Join("\n", outLines);
        }
    }
}
