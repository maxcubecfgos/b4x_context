using System;
using System.Collections.Generic;
using System.Text;

namespace B4XContext.Engine
{
    /// <summary>
    /// Dart skeleton generator built on a hand-written Dart lexer plus a structural
    /// (brace/paren aware) declaration extractor. TreeSitter.DotNet ships no Dart
    /// grammar, so B4X_Context parses Dart itself.
    ///
    /// Kept in the skeleton: library/import/export/part directives, leading comments
    /// and doc comments (trimmed), declaration headers of class/mixin/extension
    /// (incl. extension type)/enum/typedef, and every member signature
    /// (fields, constructors incl. initializer lists, methods, getters/setters,
    /// operators, abstract members). Bodies are replaced by
    /// "// ... (N lines omitted) ..." markers, enum constants are kept verbatim.
    ///
    /// Everything is line/offset based: string literals (raw, triple quoted and with
    /// interpolation), nested block comments and closures never confuse the brace
    /// accounting.
    /// </summary>
    public static class DartSkeletonizer
    {
        private const int MAX_LINES = 200;
        private const int MAX_CHARS = 8000;
        private const int MAX_LINE_LEN = 200;
        /// <summary>Comment lines kept above a type (or the file header) before the omitted marker.</summary>
        private const int HEADER_DOC_LINES = 6;

        /// <summary>Comment lines kept above a member: Flutter member docs are long, signatures matter more.</summary>
        private const int MEMBER_DOC_LINES = 3;

        /// <summary>Multi-line header/statement longer than this keeps first + last line only.</summary>
        private const int HEAD_TRIM_THRESHOLD = 6;

        /// <summary>Arrow (expression) bodies are signatures, they are trimmed much earlier.</summary>
        private const int ARROW_TRIM_THRESHOLD = 2;

        private const int MAX_ENUM_CONSTANT_LINES = 40;

        private enum Tk { Ident, Number, Str, LineComment, BlockComment, Punct }

        private sealed class Token
        {
            public Tk Kind;
            public string Text = "";
            public int Line;      // 0-based first line
            public int EndLine;   // 0-based last line
            public int Start;     // char offset
            public int End;       // char offset after the token
        }

        /// <summary>1-based inclusive line range of an implementation-only region (a body).</summary>
        public sealed class DartBodyRange
        {
            public int StartLine;
            public int EndLine;
        }

        /// <summary>A declaration found by the Dart parser (used by the granular/Custom bundle).</summary>
        public sealed class DartDeclaration
        {
            public string Name = "";
            public string Signature = "";
            public int StartLine;        // 1-based, includes an attached doc comment
            public int EndLine;          // 1-based, includes the body
            public string Container = ""; // enclosing class/mixin/extension/enum
            public bool IsType;
            public bool IsCallable;
        }

        public static MultiLangSkeleton Skeletonize(string source, LangProfile profile)
        {
            var src = Normalize(source ?? "");
            var lines = src.Split('\n');
            int originalLines = lines.Length;

            if (string.IsNullOrWhiteSpace(src))
                return new MultiLangSkeleton { Skeleton = "", OriginalLines = originalLines, SkeletonLines = 0 };

            var tokens = Lex(src);
            var extractor = new Extractor(lines, tokens, profile ?? LangSupport.FromExtension("dart"), null, null);
            extractor.Run();
            var skeleton = extractor.Build();

            return new MultiLangSkeleton
            {
                Skeleton = skeleton,
                OriginalLines = originalLines,
                SkeletonLines = skeleton.Length == 0 ? 0 : skeleton.Split('\n').Length
            };
        }

        /// <summary>
        /// Walks the same structure as <see cref="Skeletonize"/> but returns the declarations
        /// instead of text: classes/mixins/extensions/enums/typedefs plus their members.
        /// Used for per-item (Custom) bundles and the per-item token estimate.
        /// </summary>
        public static List<DartDeclaration> ExtractDeclarations(string source)
        {
            var result = new List<DartDeclaration>();
            var src = Normalize(source ?? "");
            if (string.IsNullOrWhiteSpace(src)) return result;

            var lines = src.Split('\n');
            var extractor = new Extractor(lines, Lex(src), LangSupport.FromExtension("dart"), result, null);
            extractor.Run();
            return result;
        }

        /// <summary>
        /// Line ranges that hold implementation only: function/constructor bodies and closure bodies.
        /// The skeleton never emits a line from inside one of them (class/enum bodies are not
        /// implementation — their member signatures are part of the skeleton).
        /// </summary>
        public static List<DartBodyRange> ImplementationRanges(string source)
        {
            var result = new List<DartBodyRange>();
            var src = Normalize(source ?? "");
            if (string.IsNullOrWhiteSpace(src)) return result;

            var lines = src.Split('\n');
            var extractor = new Extractor(lines, Lex(src), LangSupport.FromExtension("dart"), null, result);
            extractor.Run();
            return result;
        }

        private static string Normalize(string s) =>
            s.Replace("\r\n", "\n").Replace('\r', '\n');

        // ---------------------------------------------------------------- lexer

        private static bool IsIdentStart(char c) => char.IsLetter(c) || c == '_' || c == '$';

        private static bool IsIdentPart(char c) => char.IsLetterOrDigit(c) || c == '_' || c == '$';

        private static List<Token> Lex(string s)
        {
            var toks = new List<Token>();
            int n = s.Length, i = 0, line = 0;

            // shebang (rare, but it must not be parsed as code)
            if (s.StartsWith("#!"))
            {
                int start = 0;
                while (i < n && s[i] != '\n') i++;
                toks.Add(new Token { Kind = Tk.LineComment, Text = s.Substring(start, i - start), Line = 0, EndLine = 0, Start = 0, End = i });
            }

            while (i < n)
            {
                char c = s[i];

                if (c == '\n') { line++; i++; continue; }
                if (c == ' ' || c == '\t' || c == '\f' || c == '\v') { i++; continue; }

                if (c == '/' && i + 1 < n && s[i + 1] == '/')
                {
                    int start = i, l = line;
                    while (i < n && s[i] != '\n') i++;
                    toks.Add(new Token { Kind = Tk.LineComment, Text = s.Substring(start, i - start), Line = l, EndLine = l, Start = start, End = i });
                    continue;
                }

                if (c == '/' && i + 1 < n && s[i + 1] == '*')
                {
                    int start = i, l = line, depth = 1;
                    i += 2;
                    while (i < n && depth > 0)
                    {
                        if (s[i] == '\n') { line++; i++; continue; }
                        if (s[i] == '/' && i + 1 < n && s[i + 1] == '*') { depth++; i += 2; continue; }
                        if (s[i] == '*' && i + 1 < n && s[i + 1] == '/') { depth--; i += 2; continue; }
                        i++;
                    }
                    toks.Add(new Token { Kind = Tk.BlockComment, Text = s.Substring(start, i - start), Line = l, EndLine = line, Start = start, End = i });
                    continue;
                }

                if (c == 'r' && i + 1 < n && (s[i + 1] == '\'' || s[i + 1] == '"'))
                {
                    int start = i, l = line;
                    i = ScanString(s, i + 1, ref line, true);
                    toks.Add(new Token { Kind = Tk.Str, Text = s.Substring(start, i - start), Line = l, EndLine = line, Start = start, End = i });
                    continue;
                }

                if (c == '\'' || c == '"')
                {
                    int start = i, l = line;
                    i = ScanString(s, i, ref line, false);
                    toks.Add(new Token { Kind = Tk.Str, Text = s.Substring(start, i - start), Line = l, EndLine = line, Start = start, End = i });
                    continue;
                }

                if (char.IsDigit(c))
                {
                    int start = i, l = line;
                    while (i < n && (char.IsLetterOrDigit(s[i]) || s[i] == '_' || s[i] == '.')) i++;
                    toks.Add(new Token { Kind = Tk.Number, Text = s.Substring(start, i - start), Line = l, EndLine = l, Start = start, End = i });
                    continue;
                }

                if (IsIdentStart(c))
                {
                    int start = i, l = line;
                    while (i < n && IsIdentPart(s[i])) i++;
                    toks.Add(new Token { Kind = Tk.Ident, Text = s.Substring(start, i - start), Line = l, EndLine = l, Start = start, End = i });
                    continue;
                }

                toks.Add(new Token { Kind = Tk.Punct, Text = c.ToString(), Line = line, EndLine = line, Start = i, End = i + 1 });
                i++;
            }

            return toks;
        }

        /// <summary>Scans a string literal (i points at the opening quote) and returns the index after it.</summary>
        private static int ScanString(string s, int i, ref int line, bool raw)
        {
            char quote = s[i];
            bool triple = i + 2 < s.Length && s[i + 1] == quote && s[i + 2] == quote;
            i += triple ? 3 : 1;

            while (i < s.Length)
            {
                char c = s[i];

                if (c == '\n')
                {
                    if (!triple) return i; // unterminated single-line literal: stop at the newline
                    line++;
                    i++;
                    continue;
                }

                if (!raw && c == '\\') { i += 2; continue; }

                if (!raw && c == '$')
                {
                    if (i + 1 < s.Length && s[i + 1] == '{') { i = SkipInterpolation(s, i + 2, ref line); continue; }
                    if (i + 1 < s.Length && IsIdentStart(s[i + 1]))
                    {
                        i += 2;
                        while (i < s.Length && IsIdentPart(s[i])) i++;
                        continue;
                    }
                    i++;
                    continue;
                }

                if (c == quote)
                {
                    if (!triple) return i + 1;
                    if (i + 2 < s.Length && s[i + 1] == quote && s[i + 2] == quote) return i + 3;
                    i++;
                    continue;
                }

                i++;
            }

            return i;
        }

        /// <summary>Skips a <c>${ ... }</c> interpolation body (i points after the opening brace).</summary>
        private static int SkipInterpolation(string s, int i, ref int line)
        {
            int depth = 1;
            while (i < s.Length && depth > 0)
            {
                char c = s[i];

                if (c == '\n') { line++; i++; continue; }

                if (c == '\'' || c == '"') { i = ScanString(s, i, ref line, false); continue; }
                if (c == 'r' && i + 1 < s.Length && (s[i + 1] == '\'' || s[i + 1] == '"')) { i = ScanString(s, i + 1, ref line, true); continue; }

                if (c == '/' && i + 1 < s.Length && s[i + 1] == '/') { while (i < s.Length && s[i] != '\n') i++; continue; }
                if (c == '/' && i + 1 < s.Length && s[i + 1] == '*')
                {
                    int d = 1;
                    i += 2;
                    while (i < s.Length && d > 0)
                    {
                        if (s[i] == '\n') line++;
                        if (s[i] == '/' && i + 1 < s.Length && s[i + 1] == '*') { d++; i += 2; continue; }
                        if (s[i] == '*' && i + 1 < s.Length && s[i + 1] == '/') { d--; i += 2; continue; }
                        i++;
                    }
                    continue;
                }

                if (c == '{') { depth++; i++; continue; }
                if (c == '}') { depth--; i++; continue; }

                i++;
            }

            return i;
        }

        // ------------------------------------------------------------ extractor

        private sealed class Extractor
        {
            private readonly string[] _lines;
            private readonly List<Token> _toks;
            private readonly LangProfile _profile;
            private readonly StringBuilder _sb = new StringBuilder();
            private readonly HashSet<int> _emitted = new HashSet<int>();
            private int _lastSrcLine = -1;

            private readonly List<DartDeclaration> _decls;
            private readonly List<DartBodyRange> _bodies;
            private string _container = "";

            public Extractor(string[] lines, List<Token> toks, LangProfile profile,
                List<DartDeclaration> decls, List<DartBodyRange> bodies)
            {
                _lines = lines;
                _toks = toks;
                _profile = profile;
                _decls = decls;
                _bodies = bodies;
            }

            /// <summary>Records the lines strictly inside a body (0-based, both exclusive).</summary>
            private void RecordBody(int openBraceIdx, int closeBraceIdx)
            {
                if (_bodies == null || closeBraceIdx <= openBraceIdx) return;

                int start = _toks[openBraceIdx].Line + 1;
                int end = _toks[closeBraceIdx].Line - 1;
                if (end >= start) _bodies.Add(new DartBodyRange { StartLine = start + 1, EndLine = end + 1 });
            }

            public void Run() => ParseScope(0, _toks.Count);

            public string Build()
            {
                var raw = _sb.ToString().Split('\n');
                var kept = new List<string>(raw.Length);

                foreach (var line in raw)
                {
                    if (line.Length == 0 && (kept.Count == 0 || kept[kept.Count - 1].Length == 0)) continue;
                    kept.Add(line);
                }
                while (kept.Count > 0 && kept[kept.Count - 1].Length == 0)
                    kept.RemoveAt(kept.Count - 1);

                bool truncated = false;
                if (kept.Count > MAX_LINES)
                {
                    kept.RemoveRange(MAX_LINES, kept.Count - MAX_LINES);
                    truncated = true;
                }

                var text = string.Join("\n", kept);
                if (text.Length > MAX_CHARS)
                {
                    text = text.Substring(0, MAX_CHARS);
                    int lastNl = text.LastIndexOf('\n');
                    if (lastNl > 0) text = text.Substring(0, lastNl);
                    truncated = true;
                }

                if (truncated) text += "\n" + _profile.MakeTruncatedMarker();
                return text;
            }

            // ---- scope walking ------------------------------------------------

            private void ParseScope(int from, int to)
            {
                int i = from;
                int commentStart = -1, commentEnd = -1; // comment block sitting directly above the next element
                while (i < to)
                {
                    var t = _toks[i];

                    if (t.Kind == Tk.LineComment || t.Kind == Tk.BlockComment)
                    {
                        // consecutive line comments form a single logical block (e.g. a doc comment)
                        int endLine = t.EndLine;
                        int k = i + 1;
                        if (t.Kind == Tk.LineComment)
                        {
                            while (k < to && _toks[k].Kind == Tk.LineComment && _toks[k].Line == endLine + 1)
                            {
                                endLine = _toks[k].EndLine;
                                k++;
                            }
                        }

                        if (commentEnd >= 0 && t.Line == commentEnd + 1) { /* contiguous, extend the block */ }
                        else commentStart = t.Line;
                        commentEnd = endLine;

                        EmitComment(t.Line, endLine);
                        i = k;
                        continue;
                    }

                    if (t.Kind == Tk.Punct && t.Text == ";") { i++; commentStart = commentEnd = -1; continue; }

                    int next = ParseElement(i, to, commentStart, commentEnd);
                    commentStart = commentEnd = -1;
                    i = next > i ? next : i + 1;
                }
            }

            private int ParseElement(int i, int limit, int docStart, int docEnd)
            {
                int paren = 0, brace = 0, assign = -1, bodyIdx = -1, stmtIdx = -1, arrowIdx = -1;
                int paramIdx = -1;   // first parameter list before any initializer -> callable
                int blockOpen = -1;  // closure body nested in a statement/expression (set(), then { ... })
                int clampLine = -1; // first multi-line token caps the header (its continuation lines stay out)
                int j = i;

                for (; j < limit; j++)
                {
                    var t = _toks[j];
                    if (t.EndLine > t.Line && clampLine < 0) clampLine = t.Line;
                    if (t.Kind != Tk.Punct) continue;

                    switch (t.Text)
                    {
                        case "(":
                            if (paren == 0 && paramIdx < 0 && assign < 0 &&
                                (LooksLikeParamList(j) || OperatorSymbol(j).Length > 0)) paramIdx = j;
                            paren++;
                            break;
                        case "[":
                            paren++;
                            break;
                        case ")":
                        case "]":
                            if (paren > 0) paren--;
                            break;
                        case "{":
                            if (paren == 0 && brace == 0 && assign < 0) bodyIdx = j;
                            else
                            {
                                if (paren == 0 && brace == 0 && blockOpen < 0 && IsClosureBrace(j)) blockOpen = j;
                                brace++;
                            }
                            break;
                        case "}":
                            if (brace > 0) brace--;
                            else stmtIdx = j; // malformed: treat the closing brace as the element end
                            break;
                        case ";":
                            if (paren == 0 && brace == 0) stmtIdx = j;
                            break;
                        case "=":
                            if (paren == 0 && brace == 0 && assign < 0 && !IsCompoundAssign(j))
                            {
                                if (IsArrow(j, limit)) arrowIdx = j;
                                else if (paramIdx < 0) assign = j; // a param list means the '=' is a ctor initializer
                            }
                            break;
                    }

                    if (bodyIdx >= 0 || stmtIdx >= 0 || arrowIdx >= 0) break;
                }

                bool callableLike = (paramIdx >= 0 && !IsFunctionTypeParamList(paramIdx))
                    || (assign < 0 && (bodyIdx >= 0 || arrowIdx >= 0));

                if (bodyIdx >= 0)
                {
                    int closeIdx = MatchBrace(bodyIdx, limit);
                    string typeName = TypeNameOf(i, bodyIdx, out int keywordIdx);
                    // a parameter list before the keyword means the keyword is just a name (`String? extension`)
                    bool isType = typeName.Length > 0 && (paramIdx < 0 || keywordIdx < paramIdx);
                    if (!isType) typeName = "";
                    Record(i, bodyIdx, closeIdx >= 0 ? _toks[closeIdx].EndLine : _toks[Math.Max(i, limit - 1)].EndLine,
                        isType, callableLike, paramIdx, assign, docStart, docEnd);
                    return HandleBody(i, bodyIdx, limit, clampLine, isType, typeName);
                }
                if (arrowIdx >= 0)
                {
                    int endIdx = FindStatementEnd(arrowIdx + 1, limit);
                    Record(i, arrowIdx, endIdx >= 0 ? _toks[endIdx].EndLine : _toks[Math.Max(arrowIdx, limit - 1)].EndLine,
                        false, callableLike, paramIdx, assign, docStart, docEnd);
                    return HandleArrow(i, arrowIdx, limit, clampLine, blockOpen);
                }
                if (stmtIdx >= 0)
                {
                    // library/import/export/part are never selectable items, they always stay in the header
                    if (!IsDirective(i))
                    {
                        bool isType = HasKeyword(i, stmtIdx, "typedef");
                        Record(i, stmtIdx, _toks[stmtIdx].EndLine, isType, callableLike, paramIdx, assign, docStart, docEnd);
                    }
                    return HandleStatement(i, stmtIdx, clampLine, blockOpen);
                }

                // Unterminated element: emit what we collected and stop.
                EmitHead(_toks[i].Line, _toks[Math.Max(i, limit - 1)].EndLine);
                return limit;
            }

            private int HandleStatement(int startIdx, int endIdx, int clampLine, int blockOpen)
            {
                EmitExpression(_toks[startIdx].Line, Clamp(clampLine, _toks[endIdx].EndLine), blockOpen,
                    HEAD_TRIM_THRESHOLD);
                return endIdx + 1;
            }

            private int HandleArrow(int startIdx, int arrowIdx, int limit, int clampLine, int blockOpen)
            {
                int endIdx = FindStatementEnd(arrowIdx + 1, limit);
                int endLine = endIdx >= 0 ? _toks[endIdx].EndLine : _toks[Math.Max(arrowIdx, limit - 1)].EndLine;
                EmitExpression(_toks[startIdx].Line, Clamp(clampLine, endLine), blockOpen, ARROW_TRIM_THRESHOLD);
                return endIdx >= 0 ? endIdx + 1 : limit;
            }

            /// <summary>
            /// Emits a statement or an arrow expression. When it contains a closure body the body is
            /// replaced by a marker, so implementation lines never leak into the skeleton.
            /// </summary>
            private void EmitExpression(int startLine, int endLine, int blockOpen, int trimThreshold)
            {
                if (blockOpen < 0)
                {
                    EmitHead(startLine, endLine, trimThreshold);
                    return;
                }

                int blockLine = _toks[blockOpen].Line;
                if (blockLine >= endLine)
                {
                    EmitHead(startLine, endLine, trimThreshold);
                    return;
                }

                int blockClose = MatchBrace(blockOpen, _toks.Count);
                RecordBody(blockOpen, blockClose);
                Emit(startLine, blockLine);

                int closingLine = blockClose >= 0 ? _toks[blockClose].Line : blockLine;
                Marker(closingLine - blockLine - 1);
                Emit(closingLine, Math.Max(closingLine, endLine));
            }

            private int HandleBody(int startIdx, int braceIdx, int limit, int clampLine, bool container, string containerName)
            {
                int headerLine = _toks[braceIdx].Line;
                int closeIdx = MatchBrace(braceIdx, limit);
                int bodyEnd = closeIdx >= 0 ? closeIdx : limit;

                EmitHead(_toks[startIdx].Line, Clamp(clampLine, headerLine));

                if (container)
                {
                    if (bodyEnd > braceIdx + 1)
                    {
                        int membersFrom = braceIdx + 1;
                        if (HasKeyword(startIdx, braceIdx, "enum"))
                            membersFrom = EmitEnumConstants(braceIdx, bodyEnd);

                        var previous = _container;
                        _container = containerName;
                        ParseScope(membersFrom, bodyEnd);
                        _container = previous;
                    }
                }
                else if (closeIdx > braceIdx)
                {
                    RecordBody(braceIdx, closeIdx);
                    int omitted = _toks[closeIdx].Line - headerLine - 1;
                    if (omitted > 0) Marker(omitted);
                    _lastSrcLine = Math.Max(_lastSrcLine, _toks[closeIdx].Line);
                }

                return closeIdx >= 0 ? closeIdx + 1 : limit;
            }

            /// <summary>Emits the enum constant section (verbatim, capped) and returns the index of the first member.</summary>
            private int EmitEnumConstants(int braceIdx, int closeIdx)
            {
                int semi = -1, depth = 0;
                for (int k = braceIdx + 1; k < closeIdx; k++)
                {
                    var t = _toks[k];
                    if (t.Kind != Tk.Punct) continue;

                    if (t.Text == "(" || t.Text == "[") depth++;
                    else if (t.Text == ")" || t.Text == "]") { if (depth > 0) depth--; }
                    else if (t.Text == ";" && depth == 0) { semi = k; break; }
                }

                int firstConst = braceIdx + 1;
                int lastConst = semi >= 0 ? semi - 1 : closeIdx - 1;

                if (lastConst >= firstConst)
                {
                    int from = _toks[firstConst].Line;
                    int to = _toks[lastConst].EndLine;
                    int count = to - from + 1;
                    if (count > MAX_ENUM_CONSTANT_LINES)
                    {
                        Emit(from, from);
                        Marker(count - 2);
                        Emit(to, to);
                    }
                    else
                    {
                        Emit(from, to);
                    }
                }

                return semi >= 0 ? semi + 1 : closeIdx;
            }

            private int FindStatementEnd(int from, int limit)
            {
                int paren = 0, brace = 0;
                for (int k = from; k < limit; k++)
                {
                    var t = _toks[k];
                    if (t.Kind != Tk.Punct) continue;

                    switch (t.Text)
                    {
                        case "(":
                        case "[":
                            paren++;
                            break;
                        case ")":
                        case "]":
                            if (paren > 0) paren--;
                            break;
                        case "{":
                            brace++;
                            break;
                        case "}":
                            if (brace > 0) brace--;
                            else return k;
                            break;
                        case ";":
                            if (paren == 0 && brace == 0) return k;
                            break;
                    }
                }
                return -1;
            }

            private int MatchBrace(int openIdx, int limit)
            {
                int depth = 0;
                for (int k = openIdx; k < limit; k++)
                {
                    var t = _toks[k];
                    if (t.Kind != Tk.Punct) continue;

                    if (t.Text == "{") depth++;
                    else if (t.Text == "}")
                    {
                        depth--;
                        if (depth == 0) return k;
                    }
                }
                return -1;
            }

            // ---- classification ----------------------------------------------

            private bool HasKeyword(int startIdx, int braceIdx, string keyword)
            {
                for (int k = startIdx; k < braceIdx; k++)
                {
                    var t = _toks[k];
                    if (t.Kind == Tk.Ident && t.Text == keyword) return true;
                }
                return false;
            }

            private bool IsArrow(int eqIdx, int limit)
            {
                if (eqIdx + 1 >= limit) return false;
                var next = _toks[eqIdx + 1];
                return next.Kind == Tk.Punct && next.Text == ">" && next.Start == _toks[eqIdx].End;
            }

            private bool IsDirective(int idx)
            {
                if (idx < 0 || idx >= _toks.Count || _toks[idx].Kind != Tk.Ident) return false;

                int next = NextCode(idx + 1, _toks.Count - 1);
                if (next < 0) return false;
                var nt = _toks[next];

                switch (_toks[idx].Text)
                {
                    case "library":
                        return nt.Kind == Tk.Punct && nt.Text == ";";
                    case "import":
                    case "export":
                    case "part":
                        return nt.Kind == Tk.Str || (nt.Kind == Tk.Ident && nt.Text == "of");
                }
                return false;
            }

            private bool IsClosureBrace(int braceIdx)
            {
                int prev = PrevCode(braceIdx - 1);
                if (prev < 0) return false;

                var t = _toks[prev];
                if (t.Kind == Tk.Punct && t.Text == ")") return true;                       // () { ...  /  (x) { ...
                if (t.Kind == Tk.Ident && (t.Text == "async" || t.Text == "sync")) return true;
                return false;
            }

            private bool IsCompoundAssign(int eqIdx)
            {
                if (eqIdx <= 0) return false;

                var prev = _toks[eqIdx - 1];

                // 'operator ==' — the '=' is part of the operator name, not an assignment
                int prevCode = PrevCode(eqIdx - 1);
                if (prevCode >= 0 && _toks[prevCode].Kind == Tk.Ident && _toks[prevCode].Text == "operator") return true;

                // '==' tokenized as two '=' characters
                if (eqIdx + 1 < _toks.Count)
                {
                    var next = _toks[eqIdx + 1];
                    if (next.Kind == Tk.Punct && next.Text == "=" && next.Start == _toks[eqIdx].End) return true;
                }

                if (prev.Kind != Tk.Punct || prev.End != _toks[eqIdx].Start) return false;

                switch (prev.Text)
                {
                    case "=":
                    case "!":
                    case "<":
                    case ">":
                    case "?":
                    case "+":
                    case "-":
                    case "*":
                    case "/":
                    case "%":
                    case "~":
                    case "&":
                    case "|":
                    case "^":
                        return true;
                }
                return false;
            }

            /// <summary>True for `void Function() callback;` — the parameter list belongs to the declared type.</summary>
            private bool IsFunctionTypeParamList(int paramIdx)
            {
                if (paramIdx <= 0) return false;
                int k = PrevCode(paramIdx - 1);
                return k >= 0 && _toks[k].Kind == Tk.Ident && _toks[k].Text == "Function";
            }

            /// <summary>True when the '(' belongs to a call/parameter list rather than to annotation arguments.</summary>
            private bool LooksLikeParamList(int parenIdx)
            {
                int k = PrevCode(parenIdx - 1);
                if (k < 0) return false;

                // generic declaration: void f<T extends KeyEvent>(...) / Foo<T> bar<T>(...)
                if (_toks[k].Kind == Tk.Punct && _toks[k].Text == ">") k = MatchAngleBackwards(k);

                if (k < 0 || _toks[k].Kind != Tk.Ident) return false;
                int p = PrevCode(k - 1);
                return !(p >= 0 && _toks[p].Kind == Tk.Punct && _toks[p].Text == "@");
            }

            /// <summary>Walks back from a closing '&gt;' to the token before its matching '&lt;'.</summary>
            private int MatchAngleBackwards(int gtIdx)
            {
                int depth = 0;
                for (int i = gtIdx; i >= 0; i--)
                {
                    var t = _toks[i];
                    if (t.Kind != Tk.Punct) continue;
                    if (t.Text == ">") depth++;
                    else if (t.Text == "<")
                    {
                        depth--;
                        if (depth == 0) return PrevCode(i - 1);
                    }
                }
                return -1;
            }

            private int PrevCode(int idx)
            {
                while (idx >= 0 && (_toks[idx].Kind == Tk.LineComment || _toks[idx].Kind == Tk.BlockComment))
                    idx--;
                return idx;
            }

            // ---- output ------------------------------------------------------

            private static int Clamp(int clampLine, int line) => clampLine >= 0 ? Math.Min(clampLine, line) : line;

            // ---- declaration recording (granular / Custom mode) ---------------

            private void Record(int startIdx, int endIdx, int endLine, bool isType, bool isCallable,
                int paramIdx, int assignIdx, int docStart, int docEnd)
            {
                if (_decls == null) return;

                int startLine = _toks[startIdx].Line;
                string name = NameOf(startIdx, endIdx, paramIdx, assignIdx, out int nameLine);
                if (name.Length == 0) return;

                if (docStart >= 0 && docEnd == startLine - 1) startLine = docStart;

                _decls.Add(new DartDeclaration
                {
                    Name = name,
                    Signature = LineAt(nameLine),
                    StartLine = startLine + 1,
                    EndLine = Math.Max(startLine, endLine) + 1,
                    Container = _container,
                    IsType = isType,
                    IsCallable = isCallable
                });
            }

            private string LineAt(int line)
            {
                int idx = Math.Min(Math.Max(0, line), _lines.Length - 1);
                var text = _lines[idx].Trim();
                return text.Length <= MAX_LINE_LEN ? text : text.Substring(0, MAX_LINE_LEN) + "...";
            }

            private static bool IsModifierKeyword(string text)
            {
                switch (text)
                {
                    case "class":
                    case "mixin":
                    case "enum":
                    case "extension":
                    case "type":
                    case "base":
                    case "interface":
                    case "final":
                    case "sealed":
                    case "abstract":
                    case "const":
                    case "static":
                    case "late":
                    case "external":
                    case "factory":
                    case "covariant":
                    case "var":
                        return true;
                }
                return false;
            }

            private string TypeNameOf(int startIdx, int boundaryIdx, out int keywordIdx)
            {
                keywordIdx = -1;
                for (int k = startIdx; k <= boundaryIdx; k++)
                {
                    var t = _toks[k];
                    if (t.Kind != Tk.Ident) continue;
                    if (t.Text != "class" && t.Text != "mixin" && t.Text != "enum" && t.Text != "extension") continue;

                    keywordIdx = k;
                    for (int m = k + 1; m <= boundaryIdx; m++)
                    {
                        var c = _toks[m];
                        if (c.Kind == Tk.Ident && !IsModifierKeyword(c.Text) && c.Text != "on") return c.Text;
                    }
                    return "";
                }
                return "";
            }

            private string NameOf(int startIdx, int endIdx, int paramIdx, int assignIdx, out int nameLine)
            {
                nameLine = _toks[startIdx].Line;

                string typeName = TypeNameOf(startIdx, endIdx, out _);
                if (typeName.Length > 0)
                {
                    nameLine = LineOfIdent(startIdx, endIdx, typeName);
                    return typeName;
                }

                if (paramIdx > 0)
                {
                    string symbol = OperatorSymbol(paramIdx);
                    if (symbol.Length > 0)
                    {
                        nameLine = _toks[PrevCode(paramIdx - 1)].Line;
                        return "operator " + symbol;
                    }

                    int nameIdx = PrevCode(paramIdx - 1);
                    if (nameIdx >= 0 && _toks[nameIdx].Kind == Tk.Ident && _toks[nameIdx].Text == "Function")
                    {
                        // `void Function() cb;` / `void Function()? cb;` — the name follows the parameter list
                        int close = MatchingParen(paramIdx, endIdx);
                        int after = close >= 0 ? NextCode(close + 1, endIdx) : -1;
                        while (after >= 0 && _toks[after].Kind == Tk.Punct && _toks[after].Text == "?")
                            after = NextCode(after + 1, endIdx);
                        if (after >= 0 && _toks[after].Kind == Tk.Ident) nameIdx = after;
                    }
                    else if (nameIdx >= 0 && _toks[nameIdx].Kind == Tk.Punct && _toks[nameIdx].Text == ">")
                    {
                        nameIdx = MatchAngleBackwards(nameIdx);   // generic method name: foo<T>(...)
                    }

                    if (nameIdx >= 0 && _toks[nameIdx].Kind == Tk.Ident)
                    {
                        nameLine = _toks[nameIdx].Line;
                        return Dotted(nameIdx, startIdx);
                    }
                }

                int stop = assignIdx >= 0 ? assignIdx : endIdx;
                for (int k = stop - 1; k >= startIdx; k--)
                {
                    var t = _toks[k];
                    if (t.Kind != Tk.Ident || IsModifierKeyword(t.Text)) continue;
                    nameLine = t.Line;
                    return t.Text;
                }

                return "";
            }

            private int LineOfIdent(int startIdx, int endIdx, string text)
            {
                for (int k = startIdx; k <= endIdx; k++)
                    if (_toks[k].Kind == Tk.Ident && _toks[k].Text == text) return _toks[k].Line;
                return _toks[startIdx].Line;
            }

            private string Dotted(int nameIdx, int startIdx)
            {
                var sb = new StringBuilder(_toks[nameIdx].Text);
                int k = PrevCode(nameIdx - 1);
                while (k >= startIdx + 1)
                {
                    if (_toks[k].Kind != Tk.Punct || _toks[k].Text != ".") break;
                    int prev = PrevCode(k - 1);
                    if (prev < startIdx || _toks[prev].Kind != Tk.Ident || IsModifierKeyword(_toks[prev].Text)) break;
                    sb.Insert(0, _toks[prev].Text + ".");
                    k = PrevCode(prev - 1);
                }
                return sb.ToString();
            }

            /// <summary>Returns "==", "[]", ... when the tokens before the parameter list are an operator name.</summary>
            private string OperatorSymbol(int parenIdx)
            {
                int last = PrevCode(parenIdx - 1);
                if (last < 0 || _toks[last].Kind == Tk.Ident) return "";

                var sb = new StringBuilder();
                int k = last;
                while (k >= 0 && _toks[k].Kind == Tk.Punct && (k == last || _toks[k].End == _toks[k + 1].Start))
                {
                    sb.Insert(0, _toks[k].Text);
                    k--;
                }

                int op = PrevCode(k);
                if (op >= 0 && _toks[op].Kind == Tk.Ident && _toks[op].Text == "operator") return sb.ToString();
                return "";
            }

            private int MatchingParen(int openIdx, int limit)
            {
                int depth = 0;
                for (int k = openIdx; k <= limit && k < _toks.Count; k++)
                {
                    var t = _toks[k];
                    if (t.Kind != Tk.Punct) continue;
                    if (t.Text == "(") depth++;
                    else if (t.Text == ")")
                    {
                        depth--;
                        if (depth == 0) return k;
                    }
                }
                return -1;
            }

            private int NextCode(int idx, int limit)
            {
                for (int k = idx; k <= limit && k < _toks.Count; k++)
                    if (_toks[k].Kind != Tk.LineComment && _toks[k].Kind != Tk.BlockComment) return k;
                return -1;
            }

            private void EmitComment(int from, int to)
            {
                int budget = _container.Length > 0 ? MEMBER_DOC_LINES : HEADER_DOC_LINES;
                int keepTo = Math.Min(to, from + budget - 1);

                Emit(from, keepTo);
                if (to > keepTo)
                {
                    Marker(to - keepTo);
                    _lastSrcLine = Math.Max(_lastSrcLine, to);
                }
            }

            /// <summary>Emits a header/statement, keeping first + last line when it is very long.</summary>
            private void EmitHead(int from, int to, int threshold = HEAD_TRIM_THRESHOLD)
            {
                if (to - from + 1 <= threshold)
                {
                    Emit(from, to);
                    return;
                }

                Emit(from, from);
                Marker(to - from - 1);
                Emit(to, to);
            }

            private void Emit(int from, int to)
            {
                if (from < 0 || to < from) return;

                from = Math.Min(from, _lines.Length - 1);
                to = Math.Min(to, _lines.Length - 1);

                if (_sb.Length > 0 && _lastSrcLine >= 0 && from > _lastSrcLine + 1 && HasBlankLine(_lastSrcLine + 1, from - 1))
                    _sb.Append('\n');

                for (int i = from; i <= to; i++)
                {
                    if (_emitted.Add(i)) _sb.Append(Truncate(_lines[i])).Append('\n');
                    _lastSrcLine = Math.Max(_lastSrcLine, i);
                }
            }

            private void Marker(int omitted)
            {
                if (omitted <= 0) return;
                _sb.Append(_profile.MakeOmitMarker(omitted)).Append('\n');
            }

            private bool HasBlankLine(int from, int to)
            {
                for (int i = Math.Max(0, from); i <= to && i < _lines.Length; i++)
                    if (_lines[i].Trim().Length == 0) return true;
                return false;
            }

            private static string Truncate(string line)
            {
                if (line.Length <= MAX_LINE_LEN) return line;
                return line.Substring(0, MAX_LINE_LEN) + "...";
            }
        }
    }
}
