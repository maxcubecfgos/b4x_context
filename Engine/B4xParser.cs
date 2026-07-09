using System;
using System.Collections.Generic;
using System.Linq;

namespace B4XContext.Engine
{
    public static class B4xParser
    {
        public class B4XNode
        {
            public string Kind { get; set; }
            public string Name { get; set; }
            public string Params { get; set; }
            public string ReturnType { get; set; }
            public int StartLine { get; set; }
            public int? EndLine { get; set; }
            public bool IsPrivate { get; set; }
            public string LeadingComment { get; set; } = "";
            public List<B4XNode> Children { get; set; } = new List<B4XNode>();
        }

        public class ParseIssue
        {
            public string Message { get; set; }
            public int Line { get; set; }
            public string Severity { get; set; }
        }

        private static string NormalizeKw(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return char.ToUpper(s[0]) + (s.Length > 1 ? s.Substring(1).ToLower() : "");
        }

        private static bool ParensBalanced(List<B4xLexer.Token> tokens)
        {
            int depth = 0; bool seenOpen = false;
            foreach (var t in tokens)
            {
                if (t.Value == "(") { depth++; seenOpen = true; }
                else if (t.Value == ")") { depth--; if (depth < 0) return false; }
            }
            return seenOpen && depth == 0;
        }

        private static List<(List<B4xLexer.Token> tokens, string comment)> SplitLogicalLines(List<B4xLexer.Token> tokens)
        {
            var lines = new List<(List<B4xLexer.Token>, string)>();
            var current = new List<B4xLexer.Token>();
            var commentBuffer = new List<string>();
            foreach (var t in tokens)
            {
                if (t.Type == B4xLexer.TOK_NEWLINE)
                {
                    if (current.Any()) { lines.Add((new List<B4xLexer.Token>(current), string.Join("\n", commentBuffer))); commentBuffer.Clear(); }
                    current.Clear();
                }
                else if (t.Type == B4xLexer.TOK_EOF)
                {
                    if (current.Any()) lines.Add((new List<B4xLexer.Token>(current), string.Join("\n", commentBuffer)));
                }
                else if (t.Type == B4xLexer.TOK_COMMENT)
                {
                    if (!current.Any()) commentBuffer.Add(t.Value);
                }
                else
                {
                    current.Add(t);
                }
            }
            return lines;
        }

        private static void CloseBlock(string closerName, int line, List<(string closer, B4XNode node, int openedAt)> stack, List<B4XNode> containerStack, List<ParseIssue> issues)
        {
            if (!stack.Any()) { issues.Add(new ParseIssue { Message = $"Unexpected \"{closerName}\": no open block to close.", Line = line, Severity = "error" }); return; }
            var top = stack.Last();
            var topCloser = top.closer; var topNode = top.node; var topLine = top.openedAt;
            if (!string.Equals(topCloser.Trim(), closerName.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ParseIssue { Message = $"Expected \"{topCloser}\" (opened at line {topLine}) but found \"{closerName}\".", Line = line, Severity = "error" });
            }
            stack.RemoveAt(stack.Count - 1);
            if (topNode != null)
            {
                topNode.EndLine = line;
                containerStack.RemoveAt(containerStack.Count - 1);
            }
        }

        private static (string paramsText, string returnType) ExtractParamsAndReturn(List<B4xLexer.Token> tokens)
        {
            string parameters = null; string returnType = null;
            int i = 0;
            if (i < tokens.Count && tokens[i].Value == "(")
            {
                int depth = 0; var parts = new List<string>(); int j = i;
                while (j < tokens.Count)
                {
                    if (tokens[j].Value == "(") depth++;
                    if (tokens[j].Value == ")") depth--;
                    parts.Add(tokens[j].Value);
                    j++;
                    if (depth == 0) break;
                }
                parameters = string.Join(" ", parts).Replace(" ( ", "(").Replace(" )", ")").Replace(" , ", ", ").Replace(" ,", ",").Replace("( ", "(");
                i = j;
            }
            if (i < tokens.Count && NormalizeKw(tokens[i].Value) == "As")
            {
                if (i + 1 < tokens.Count) returnType = tokens[i + 1].Value;
            }
            return (parameters, returnType);
        }

        public static (B4XNode root, List<ParseIssue> issues) Parse(string source)
        {
            var tokenList = B4xLexer.Tokenize(source);
            var tokens = tokenList; // include comments
            var logicalLines = SplitLogicalLines(tokens);

            var issues = new List<ParseIssue>();
            var root = new B4XNode { Kind = "Module", Name = "", StartLine = 1 };
            var stack = new List<(string closer, B4XNode node, int openedAt)>();
            var containerStack = new List<B4XNode> { root };
            string pendingComment = "";

            foreach (var (lineTokens, commentText) in logicalLines)
            {
                if (!string.IsNullOrEmpty(commentText)) pendingComment = commentText;
                if (lineTokens == null || !lineTokens.Any()) continue;
                var first = lineTokens[0];

                if (first.Type == B4xLexer.TOK_DIRECTIVE)
                {
                    var text = first.Value.Trim();
                    var regionMatch = System.Text.RegularExpressions.Regex.Match(text, "(?i)^#Region\\b\\s*(.*)$");
                    var endRegionMatch = System.Text.RegularExpressions.Regex.Match(text, "(?i)^#End Region\\b");
                    if (regionMatch.Success)
                    {
                        var rawName = regionMatch.Groups[1].Value.Trim();
                        var regionName = rawName.StartsWith(":") ? rawName.Substring(1).Trim() : rawName;
                        var regionNode = new B4XNode { Kind = "Region", Name = string.IsNullOrEmpty(regionName) ? "(unnamed)" : regionName, StartLine = first.Line, LeadingComment = pendingComment };
                        pendingComment = "";
                        containerStack.Last().Children.Add(regionNode);
                        containerStack.Add(regionNode);
                        stack.Add(("End Region", regionNode, first.Line));
                    }
                    else if (endRegionMatch.Success)
                    {
                        CloseBlock("End Region", first.Line, stack, containerStack, issues);
                    }
                    continue;
                }

                if (first.Type != B4xLexer.TOK_KEYWORD)
                {
                    pendingComment = "";
                    continue;
                }

                var kw = NormalizeKw(first.Value);
                int idx = 0; bool isPrivate = false;
                if (kw == "Private" || kw == "Public") { isPrivate = kw == "Private"; idx = 1; }
                var stmt_kw = idx < lineTokens.Count ? NormalizeKw(lineTokens[idx].Value) : "";

                if (stmt_kw == "Sub")
                {
                    var nameTok = (idx + 1) < lineTokens.Count ? lineTokens[idx + 1] : null;
                    var normalized_name = nameTok != null ? nameTok.Value.ToLowerInvariant() : "";
                    if (normalized_name == "process_globals" || normalized_name == "globals" || normalized_name == "class_globals")
                    {
                        var kindMap = normalized_name == "process_globals" ? "Process_Globals" : normalized_name == "globals" ? "Globals" : "Class_Globals";
                        var pgNode = new B4XNode { Kind = kindMap, Name = "", StartLine = first.Line, LeadingComment = pendingComment };
                        pendingComment = "";
                        containerStack.Last().Children.Add(pgNode);
                        containerStack.Add(pgNode);
                        stack.Add(("End Sub", pgNode, first.Line));
                        continue;
                    }

                    var name = (nameTok != null && nameTok.Type == B4xLexer.TOK_IDENT) ? nameTok.Value : "(anonymous)";
                    var rest = lineTokens.Skip(idx + 2).ToList();
                    var (paramsText, returnType) = ExtractParamsAndReturn(rest);
                    var subNode = new B4XNode { Kind = "Sub", Name = name, Params = paramsText, ReturnType = returnType, StartLine = first.Line, IsPrivate = isPrivate, LeadingComment = pendingComment };
                    pendingComment = "";
                    containerStack.Last().Children.Add(subNode);
                    containerStack.Add(subNode);
                    stack.Add(("End Sub", subNode, first.Line));
                    continue;
                }

                if (stmt_kw == "Type")
                {
                    var nameTok = (idx + 1) < lineTokens.Count ? lineTokens[idx + 1] : null;
                    var name = (nameTok != null && nameTok.Type == B4xLexer.TOK_IDENT) ? nameTok.Value : "(anonymous)";
                    var hasOpenParen = lineTokens.Skip(idx + 2).Any(t => t.Value == "(");
                    var isBalancedOneLiner = hasOpenParen && ParensBalanced(lineTokens.Skip(idx + 2).ToList());
                    if (isBalancedOneLiner)
                    {
                        var typeNode = new B4XNode { Kind = "Type", Name = name, StartLine = first.Line, EndLine = first.Line, IsPrivate = isPrivate, LeadingComment = pendingComment };
                        pendingComment = "";
                        containerStack.Last().Children.Add(typeNode);
                        continue;
                    }
                    var typeNode2 = new B4XNode { Kind = "Type", Name = name, StartLine = first.Line, IsPrivate = isPrivate, LeadingComment = pendingComment };
                    pendingComment = "";
                    containerStack.Last().Children.Add(typeNode2);
                    containerStack.Add(typeNode2);
                    stack.Add(("End Type", typeNode2, first.Line));
                    continue;
                }

                if (new[] { "If", "Select", "Do", "For", "Try" }.Contains(stmt_kw))
                {
                    if (stmt_kw == "If")
                    {
                        int thenIdx = lineTokens.FindIndex(t => NormalizeKw(t.Value) == "Then");
                        bool hasCodeAfterThen = thenIdx >= 0 && thenIdx < lineTokens.Count - 1;
                        if (hasCodeAfterThen) { pendingComment = ""; continue; }
                    }
                    var closer = stmt_kw == "If" ? "End If" : stmt_kw == "Select" ? "End Select" : stmt_kw == "Do" ? "Loop" : stmt_kw == "For" ? "Next" : "End Try";
                    stack.Add((closer, null, first.Line));
                    pendingComment = "";
                    continue;
                }

                if (stmt_kw == "End")
                {
                    var what = (idx + 1) < lineTokens.Count ? NormalizeKw(lineTokens[idx + 1].Value) : "";
                    CloseBlock($"End {what}", first.Line, stack, containerStack, issues);
                    pendingComment = ""; continue;
                }
                if (stmt_kw == "Next" || stmt_kw == "Loop") { CloseBlock(stmt_kw, first.Line, stack, containerStack, issues); pendingComment = ""; continue; }

                pendingComment = "";
            }

            foreach (var s in stack)
            {
                issues.Add(new ParseIssue { Message = $"Unclosed block: expected \"{s.closer}\" (opened at line {s.openedAt}).", Line = s.openedAt, Severity = "error" });
            }

            return (root, issues);
        }

        public static List<B4XNode> FlattenSubsAndTypes(B4XNode node)
        {
            var result = new List<B4XNode>();
            if (node == null) return result;
            if (new[] { "Sub", "Type", "Process_Globals", "Globals", "Class_Globals" }.Contains(node.Kind)) result.Add(node);
            foreach (var c in node.Children) result.AddRange(FlattenSubsAndTypes(c));
            return result;
        }

        public static string FindEnclosingSub(string source, int line)
        {
            try
            {
                var (root, issues) = Parse(source);
                var nodes = FlattenSubsAndTypes(root);
                foreach (var node in nodes)
                {
                    if (node.Kind == "Sub" && node.StartLine <= line && (node.EndLine ?? node.StartLine) >= line) return node.Name;
                }
            }
            catch { }
            return null;
        }
    }
}
