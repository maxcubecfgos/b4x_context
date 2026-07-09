using System;
using System.IO;
using System.Linq;
using System.Text;

namespace B4XContext.Services
{
    public static class CodeUtils
    {
        private static readonly string[] EncodingsToTry = new[] { "utf-8-sig", "utf-8", "windows-1252", "iso-8859-1" };
        private const string DESIGN_TEXT_MARKER = "@EndOfDesignText@";

        public static string ReadTextSafely(string path)
        {
            var raw = File.ReadAllBytes(path);
            // Strip BOM if present
            if (raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF)
            {
                raw = raw.Skip(3).ToArray();
            }

            string text = null;
            foreach (var enc in EncodingsToTry)
            {
                try
                {
                    var encoding = Encoding.GetEncoding(enc);
                    text = encoding.GetString(raw);
                    break;
                }
                catch { }
            }

            if (text == null)
            {
                text = Encoding.Latin1.GetString(raw);
            }

            int idx = text.IndexOf(DESIGN_TEXT_MARKER, StringComparison.Ordinal);
            if (idx >= 0)
            {
                text = text.Substring(idx + DESIGN_TEXT_MARKER.Length);
                // strip leading newlines
                text = text.TrimStart('\r', '\n');
            }

            return text;
        }

        public static string ExtractSub(string source, int startLine, int endLine)
        {
            if (string.IsNullOrEmpty(source)) return string.Empty;
            var lines = source.Split(new[] { '\n' });
            int n = lines.Length;
            int s = Math.Max(1, Math.Min(startLine, n)) - 1;
            int e = Math.Max(s, Math.Min(endLine, n)) - 1;
            return string.Join("\n", lines.Skip(s).Take(e - s + 1));
        }

        // Extract sub given that source may be only the selection that contains the sub.
        // This attempts to extract the lines relative to the provided source text.
        public static string ExtractSubBySourceText(string sourceText, int startLine, int endLine)
        {
            return ExtractSub(sourceText, startLine, endLine);
        }
    }
}
