using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using TreeSitter;

namespace B4XContext.Engine
{
    public sealed class MultiLangSkeleton
    {
        public string Skeleton { get; set; }
        public int OriginalLines { get; set; }
        public int SkeletonLines { get; set; }
        public double CompressionRatio => OriginalLines == 0 ? 0 : (double)(OriginalLines - SkeletonLines) / OriginalLines;
    }

    /// <summary>
    /// Tree-sitter skeleton generator for non-B4X languages.
    /// Walks the CST and keeps imports, declaration headers and leading comments,
    /// omitting bodies with "... N lines omitted" markers. Falls back to keeping
    /// non-empty lines when parsing fails, and passes raw-only languages through.
    /// </summary>
    public static class MultiLangSkeletonizer
    {
        private const int MAX_SKELETON_LINES = 200;
        private const int MAX_SKELETON_CHARS = 8000;
        private const int MAX_FALLBACK_LINE_LEN = 200;
        private const ulong MAX_SOURCE_BYTES = 10 * 1024 * 1024;

        // C-family/Dart style signature opening a block, e.g.
        //   "Widget build(BuildContext context) {"  /  "Future<void> load() async {"
        private static readonly Regex FallbackSignatureRe = new Regex(
            @"^[A-Za-z_][\w\.<>,\[\]\? ]*\s+[A-Za-z_]\w*\s*\([^;{}]*\)\s*(?:async\s*)?\{\s*$",
            RegexOptions.Compiled);

        private struct Span
        {
            public int Start;      // 0-based first line
            public int HeaderEnd;  // 0-based last header line
            public int End;        // 0-based last line of the node
            public bool Container; // descend to collect nested declarations
        }

        public static MultiLangSkeleton Skeletonize(string source, string extOrKind)
        {
            var profile = LangSupport.FromExtension(extOrKind);
            var lines = (source ?? "").Split(new[] { "\n" }, StringSplitOptions.None);
            int originalLines = lines.Length;

            if (string.IsNullOrWhiteSpace(source))
                return new MultiLangSkeleton { Skeleton = "", OriginalLines = originalLines, SkeletonLines = 0 };

            if (profile.IsRawOnly)
                return new MultiLangSkeleton { Skeleton = source, OriginalLines = originalLines, SkeletonLines = originalLines };

            if (profile.Kind == SkeletonKind.Dart)
            {
                try
                {
                    return DartSkeletonizer.Skeletonize(source, profile);
                }
                catch (Exception)
                {
                    return StructuralCompress(source, extOrKind, originalLines);
                }
            }

            if (profile.Family != StructuralFamily.None || string.IsNullOrEmpty(profile.GrammarId))
                return StructuralCompress(source, extOrKind, originalLines);

            if (source.Length > (int)MAX_SOURCE_BYTES)
                return StructuralCompress(source, extOrKind, originalLines);

            try
            {
                using var language = new Language(ResolveLibraryId(profile.GrammarId));
                using var parser = new TreeSitter.Parser(language);
                using var tree = parser.Parse(source);
                if (tree is null)
                    return StructuralCompress(source, extOrKind, originalLines);

                var root = tree.RootNode;
                var spans = new List<Span>();
                CollectSpans(root, profile, spans, lines);
                return BuildOutput(lines, originalLines, profile, spans);
            }
            catch (Exception)
            {
                return StructuralCompress(source, extOrKind, originalLines);
            }
        }

        private static string ResolveLibraryId(string grammarId)
        {
            switch (grammarId)
            {
                case "csharp":
                    return "c-sharp";
                default:
                    return grammarId;
            }
        }

        private static void CollectSpans(Node node, LangProfile profile, List<Span> spans, string[] lines)
        {
            var kind = node.Type;
            int start = node.StartPosition.Row;
            int end = node.EndPosition.Row;

            if (profile.ImportKinds.Contains(kind))
            {
                spans.Add(new Span { Start = start, HeaderEnd = end, End = end, Container = false });
                return;
            }

            if (profile.DeclKinds.Contains(kind))
            {
                bool container = profile.ContainerDeclKinds.Contains(kind);
                int headerEnd = container ? start : FindHeaderEnd(node, start, end, profile, lines);
                spans.Add(new Span { Start = start, HeaderEnd = headerEnd, End = end, Container = container });
                if (!container)
                    return;
            }

            foreach (var child in node.Children)
                CollectSpans(child, profile, spans, lines);
        }

        private static int FindHeaderEnd(Node node, int start, int end, LangProfile profile, string[] lines)
        {
            foreach (var child in node.Children)
            {
                if (!profile.BodyKinds.Contains(child.Type)) continue;

                int bodyStart = child.StartPosition.Row;
                if (bodyStart <= start)
                    return start; // `{` (or block start) on the signature line
                if (end <= bodyStart)
                    return start; // body occupies every remaining line -> signature only

                var text = lines[Math.Min(bodyStart, lines.Length - 1)].TrimStart();
                if (text.StartsWith("{"))
                    return Math.Min(bodyStart, end);
                return Math.Max(start, Math.Min(bodyStart - 1, end));
            }
            return start;
        }

        private static MultiLangSkeleton BuildOutput(string[] lines, int originalLines, LangProfile profile, List<Span> spans)
        {
            if (spans.Count == 0)
                return StructuralCompress(string.Join("\n", lines), profile.FenceTag, originalLines);

            spans.Sort((a, b) =>
            {
                int c = a.Start.CompareTo(b.Start);
                return c != 0 ? c : a.End.CompareTo(b.End);
            });

            var sb = new StringBuilder();
            var emitted = new HashSet<int>();
            int cursor = -1; // last emitted source line index
            bool prevEmitted = false;

            foreach (var span in spans)
            {
                if (span.End <= cursor)
                    continue; // already covered by a parent span

                // Gap between previous content and this span.
                if (span.Start > cursor + 1)
                {
                    for (int i = cursor + 1; i < span.Start && i < lines.Length; i++)
                    {
                        if (emitted.Contains(i)) continue;
                        var t = lines[i].Trim();
                        if (t.Length == 0)
                        {
                            if (prevEmitted && sb.Length > 0 && sb[sb.Length - 1] != '\n')
                                sb.Append('\n');
                        }
                        else if (IsCommentish(t))
                        {
                            AppendLine(sb, lines[i], ref prevEmitted);
                            emitted.Add(i);
                        }
                    }
                }

                // Header lines.
                int firstHeader = Math.Max(span.Start, cursor + 1);
                for (int i = firstHeader; i <= span.HeaderEnd && i <= span.End && i < lines.Length; i++)
                {
                    if (emitted.Contains(i)) continue;
                    AppendLine(sb, lines[i], ref prevEmitted);
                    emitted.Add(i);
                }

                // Omit marker.
                if (span.HeaderEnd < span.End)
                {
                    int omitted = span.End - span.HeaderEnd;
                    AppendLine(sb, profile.MakeOmitMarker(omitted), ref prevEmitted);
                }

                cursor = span.Container ? Math.Max(cursor, span.HeaderEnd) : Math.Max(cursor, span.End);
            }

            // Tail: comments/blanks after the last span.
            for (int i = cursor + 1; i < lines.Length; i++)
            {
                if (emitted.Contains(i)) continue;
                var t = lines[i].Trim();
                if (t.Length == 0)
                {
                    if (prevEmitted && sb.Length > 0 && sb[sb.Length - 1] != '\n')
                        sb.Append('\n');
                }
                else if (IsCommentish(t))
                {
                    AppendLine(sb, lines[i], ref prevEmitted);
                    emitted.Add(i);
                }
            }

            var skeleton = CapOutput(sb, profile);
            return new MultiLangSkeleton
            {
                Skeleton = skeleton,
                OriginalLines = originalLines,
                SkeletonLines = skeleton.Split('\n').Length
            };
        }

        private static void AppendLine(StringBuilder sb, string line, ref bool prevEmitted)
        {
            if (prevEmitted)
                sb.Append('\n');
            sb.Append(line);
            prevEmitted = true;
        }

        private static bool IsCommentish(string trimmed)
        {
            return trimmed.StartsWith("#")
                || trimmed.StartsWith("'")
                || trimmed.StartsWith("//")
                || trimmed.StartsWith("/*")
                || trimmed.StartsWith("*/")
                || trimmed.StartsWith("*")
                || trimmed.StartsWith("<!--");
        }

        private static MultiLangSkeleton StructuralCompress(string source, string ext, int originalLines)
        {
            var key = (ext ?? "").TrimStart('.').ToLowerInvariant();
            if (key == "lock")
                return new MultiLangSkeleton { Skeleton = "", OriginalLines = originalLines, SkeletonLines = 0 };

            bool isConfig = key == "toml" || key == "ini" || key == "cfg" || key == "conf" || key == "env" || key == "properties";
            bool isMarkdown = key == "md" || key == "markdown";

            var output = new List<string>();
            bool prevEmpty = false;
            bool hasOutput = false;

            foreach (var line in source.Split(new[] { "\n" }, StringSplitOptions.None))
            {
                var trimmed = line.Trim();

                if (trimmed.Length == 0)
                {
                    if (hasOutput && !prevEmpty)
                    {
                        output.Add("");
                        prevEmpty = true;
                    }
                    continue;
                }
                prevEmpty = false;

                if (IsStructuralLine(trimmed, isConfig, isMarkdown))
                {
                    output.Add(TruncateLine(line, MAX_FALLBACK_LINE_LEN));
                    hasOutput = true;
                }
            }

            var joined = string.Join("\n", output);
            var capped = CapStructural(joined);
            return new MultiLangSkeleton
            {
                Skeleton = capped,
                OriginalLines = originalLines,
                SkeletonLines = capped.Length == 0 ? 0 : capped.Split('\n').Length
            };
        }

        private static bool IsStructuralLine(string trimmed, bool isConfig, bool isMarkdown)
        {
            if (trimmed.StartsWith("import ")
                || trimmed.StartsWith("from ")
                || trimmed.StartsWith("export ")
                || trimmed.StartsWith("require(")
                || trimmed.StartsWith("use ")
                || trimmed.StartsWith("mod ")
                || trimmed.StartsWith("package ")
                || trimmed.StartsWith("#include")
                || trimmed.StartsWith("using ")
                || trimmed.StartsWith("class ")
                || trimmed.StartsWith("struct ")
                || trimmed.StartsWith("enum ")
                || trimmed.StartsWith("interface ")
                || trimmed.StartsWith("trait ")
                || trimmed.StartsWith("type ")
                || trimmed.StartsWith("typedef ")
                || trimmed.StartsWith("fn ")
                || trimmed.StartsWith("func ")
                || trimmed.StartsWith("function ")
                || trimmed.StartsWith("def ")
                || trimmed.StartsWith("pub fn ")
                || trimmed.StartsWith("async fn ")
                || trimmed.StartsWith("pub async fn ")
                || trimmed.Contains("fn ")
                || trimmed.StartsWith("const ")
                || trimmed.StartsWith("let ")
                || trimmed.StartsWith("var ")
                || trimmed.StartsWith("final ")
                || trimmed.StartsWith("static ")
                || trimmed.StartsWith("pub ")
                || trimmed.StartsWith("public ")
                || trimmed.StartsWith("private ")
                || trimmed.StartsWith("protected ")
                || trimmed.StartsWith("abstract ")
                || trimmed.StartsWith("mixin ")
                || trimmed.StartsWith("extension ")
                || trimmed.StartsWith("sealed ")
                || trimmed.StartsWith("part ")
                || trimmed.StartsWith("library ")
                || FallbackSignatureRe.IsMatch(trimmed)
                || trimmed.StartsWith("@")
                || trimmed.StartsWith("#[")
                || trimmed == "end"
                || trimmed.StartsWith("///")
                || trimmed.StartsWith("//!")
                || trimmed.StartsWith("/**")
                || trimmed.StartsWith("* ")
                || (trimmed.StartsWith("#") && !trimmed.StartsWith("# "))
                || (isConfig && IsConfigLine(trimmed))
                || (isMarkdown && IsMarkdownStructural(trimmed)))
                return true;
            return false;
        }

        private static bool IsConfigLine(string trimmed)
        {
            if (trimmed.StartsWith("#") || trimmed.StartsWith(";"))
                return false;
            if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                return true;
            if (trimmed.StartsWith("export "))
                return trimmed.Contains('=');
            return trimmed.Contains('=');
        }

        private static bool IsMarkdownStructural(string trimmed)
        {
            return trimmed.StartsWith('#')
                || trimmed.StartsWith("```")
                || trimmed.StartsWith("- ")
                || trimmed.StartsWith("* ");
        }

        private static string TruncateLine(string line, int maxLen)
        {
            if (line.Length <= maxLen)
                return line;
            return line.Substring(0, maxLen) + "...";
        }

        private static string CapStructural(string skeleton)
        {
            if (skeleton.Length == 0)
                return "";

            var lines = skeleton.Split('\n');
            bool truncated = false;
            if (lines.Length > MAX_SKELETON_LINES)
            {
                lines = Sub(lines, 0, MAX_SKELETON_LINES);
                truncated = true;
            }

            var result = string.Join("\n", lines);
            if (result.Length > MAX_SKELETON_CHARS)
            {
                result = result.Substring(0, MAX_SKELETON_CHARS);
                int lastNl = result.LastIndexOf('\n');
                if (lastNl >= 0)
                    result = result.Substring(0, lastNl);
                truncated = true;
            }

            if (truncated)
                result += "\n// ...";
            return result;
        }

        private static string CapOutput(StringBuilder sb, LangProfile profile)
        {
            var lines = sb.ToString().Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            bool truncated = false;
            if (lines.Length > MAX_SKELETON_LINES)
            {
                lines = Sub(lines, 0, MAX_SKELETON_LINES);
                truncated = true;
            }

            var text = string.Join("\n", lines);
            if (text.Length > MAX_SKELETON_CHARS)
            {
                text = text.Substring(0, MAX_SKELETON_CHARS);
                int lastNl = text.LastIndexOf('\n');
                if (lastNl > 0)
                    text = text.Substring(0, lastNl);
                truncated = true;
            }

            while (text.Contains("\n\n\n"))
                text = text.Replace("\n\n\n", "\n\n");

            if (truncated)
                text += "\n" + profile.MakeTruncatedMarker();
            return text;
        }

        private static T[] Sub<T>(T[] arr, int start, int count)
        {
            var result = new T[count];
            Array.Copy(arr, start, result, 0, count);
            return result;
        }
    }
}