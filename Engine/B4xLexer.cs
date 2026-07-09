using System;
using System.Collections.Generic;

namespace B4XContext.Engine
{
    public static class B4xLexer
    {
        public const string TOK_KEYWORD = "Keyword";
        public const string TOK_IDENT = "Identifier";
        public const string TOK_STRING = "StringLiteral";
        public const string TOK_NUMBER = "NumberLiteral";
        public const string TOK_OPERATOR = "Operator";
        public const string TOK_PUNCT = "Punctuation";
        public const string TOK_COMMENT = "Comment";
        public const string TOK_DIRECTIVE = "Directive";
        public const string TOK_NEWLINE = "Newline";
        public const string TOK_UNKNOWN = "Unknown";
        public const string TOK_EOF = "EOF";

        public static readonly HashSet<string> KEYWORDS = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Sub","End","Dim","Private","Public","Static",
            "If","Then","Else","ElseIf",
            "For","Each","Next","To","Step",
            "Do","Loop","While","Until",
            "Select","Case",
            "Try","Catch",
            "Type",
            "Class_Globals","Process_Globals","Globals",
            "Return","Exit","Continue",
            "Wait","Resumable",
            "New","As","Is","IsNot",
            "And","Or","Not","Xor","Mod",
            "True","False","Null",
            "Array","List","Map",
            "Me","Sender",
        };

        static readonly string[] OPERATORS = new[] { "<=", ">=", "<>", "==", "=", "<", ">", "+", "-", "*", "/", "&", "^" };
        static readonly char[] PUNCTUATION = new[] { '(', ')', ',', '.', ':' };

        public class Token
        {
            public string Type { get; set; }
            public string Value { get; set; }
            public int Line { get; set; }
            public int Col { get; set; }

            public Token(string type, string value, int line, int col)
            {
                Type = type; Value = value; Line = line; Col = col;
            }
        }

        public static List<Token> Tokenize(string source)
        {
            var tokens = new List<Token>();
            if (source == null) source = string.Empty;
            int line = 1, col = 1, i = 0, n = source.Length;

            void Advance(int count = 1)
            {
                for (int k = 0; k < count; k++)
                {
                    if (i < n && source[i] == '\n') { line++; col = 1; }
                    else col++;
                    i++;
                }
            }

            while (i < n)
            {
                int startLine = line, startCol = col;
                char ch = source[i];

                if (ch == '\n') { tokens.Add(new Token(TOK_NEWLINE, "\n", startLine, startCol)); Advance(); continue; }
                if (ch == '\r') { Advance(); continue; }
                if (ch == ' ' || ch == '\t') { int j = i; while (j < n && (source[j] == ' ' || source[j] == '\t')) j++; Advance(j - i); continue; }

                if (ch == '\'')
                {
                    int j = i; while (j < n && source[j] != '\n') j++; tokens.Add(new Token(TOK_COMMENT, source.Substring(i, j - i), startLine, startCol)); Advance(j - i); continue;
                }

                if (ch == '#')
                {
                    int j = i; while (j < n && source[j] != '\n') j++; tokens.Add(new Token(TOK_DIRECTIVE, source.Substring(i, j - i), startLine, startCol)); Advance(j - i); continue;
                }

                if (ch == '$' && i + 1 < n && source[i + 1] == '"')
                {
                    int j = i + 2;
                    while (j < n)
                    {
                        if (source[j] == '"' && j + 1 < n && source[j + 1] == '$') { j += 2; break; }
                        j++;
                    }
                    if (j > n) j = n;
                    tokens.Add(new Token(TOK_STRING, source.Substring(i, j - i), startLine, startCol)); Advance(j - i); continue;
                }

                if (ch == '"')
                {
                    int j = i + 1;
                    while (j < n)
                    {
                        if (source[j] == '"')
                        {
                            if (j + 1 < n && source[j + 1] == '"') { j += 2; continue; }
                            j++; break;
                        }
                        j++;
                    }
                    if (j > n) j = n;
                    tokens.Add(new Token(TOK_STRING, source.Substring(i, j - i), startLine, startCol)); Advance(j - i); continue;
                }

                if (char.IsDigit(ch))
                {
                    int j = i; while (j < n && (char.IsDigit(source[j]) || source[j] == '.')) j++;
                    if (j < n && (source[j] == 'L' || source[j] == 'F' || source[j] == 'f')) j++;
                    if (j < n && source[j] == '%' && j + 1 < n && (source[j + 1] == 'x' || source[j + 1] == 'X' || source[j + 1] == 'y' || source[j + 1] == 'Y')) j += 2;
                    tokens.Add(new Token(TOK_NUMBER, source.Substring(i, j - i), startLine, startCol)); Advance(j - i); continue;
                }

                if (char.IsLetter(ch) || ch == '_')
                {
                    int j = i; while (j < n && (char.IsLetterOrDigit(source[j]) || source[j] == '_')) j++;
                    var word = source.Substring(i, j - i);
                    bool isKw = KEYWORDS.Contains(word) || (word.Length > 0 && char.IsUpper(word[0]) && KEYWORDS.Contains(char.ToUpper(word[0]) + word.Substring(1).ToLower()));
                    tokens.Add(new Token(isKw ? TOK_KEYWORD : TOK_IDENT, word, startLine, startCol)); Advance(j - i); continue;
                }

                // two-char operators
                if (i + 1 < n)
                {
                    var two = source.Substring(i, 2);
                    if (Array.Exists(OPERATORS, op => op == two)) { tokens.Add(new Token(TOK_OPERATOR, two, startLine, startCol)); Advance(2); continue; }
                }
                if (Array.Exists(OPERATORS, op => op == ch.ToString())) { tokens.Add(new Token(TOK_OPERATOR, ch.ToString(), startLine, startCol)); Advance(); continue; }
                if (Array.Exists(PUNCTUATION, p => p == ch)) { tokens.Add(new Token(TOK_PUNCT, ch.ToString(), startLine, startCol)); Advance(); continue; }

                tokens.Add(new Token(TOK_UNKNOWN, ch.ToString(), startLine, startCol)); Advance();
            }

            tokens.Add(new Token(TOK_EOF, string.Empty, line, col));
            return tokens;
        }
    }
}
