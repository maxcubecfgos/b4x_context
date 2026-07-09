using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace B4XContext.Engine
{
    public static class BuildOutputParser
    {
        private static readonly Regex VERSION_RE = new Regex("(?i)^(?:Version\\s+)?(B4A|B4J|B4i)(?:\\s+Version)?:\\s*([\\d.]+)", RegexOptions.Compiled);
        private static readonly Regex JAVA_VERSION_RE = new Regex("(?i)^Java Version:\\s*([\\d.]+)", RegexOptions.Compiled);
        private static readonly Regex ERROR_LINE_RE = new Regex("(?i)^Error\\s*(B4A|B4J|B4i)?\\s*line:\\s*(\\d+)\\s*$", RegexOptions.Compiled);
        private static readonly Regex ERROR_DESC_RE = new Regex("(?i)^Error description:\\s*(.+)$", RegexOptions.Compiled);
        private static readonly Regex ERROR_OCCURRED_RE = new Regex("(?i)^Error occurred on line:\\s*(\\d+)\\s*$", RegexOptions.Compiled);
        private static readonly Regex WORD_RE = new Regex("(?i)^Word:\\s*(.+)$", RegexOptions.Compiled);
        // Javac error pattern: file.java:line: error: message
        private static readonly Regex JAVAC_ERROR_RE = new Regex(@"^(.*?\.java):(\d+):\s*error:\s*(.+)$", RegexOptions.Compiled);

        // Note: The JAVAC_ERROR_RE above is a common-case pattern; keep a fallback as well
        private static readonly Regex JAVAC_ERROR_RE_FALLBACK = new Regex("^(.*?\\.java):(\\d+):\\s*error:\\s*(.+)$", RegexOptions.Compiled);
        private static readonly Regex SYMBOL_RE = new Regex("(?i)^symbol:\\s*(.+)$", RegexOptions.Compiled);
        private static readonly Regex LOCATION_RE = new Regex("(?i)^location:\\s*(.+)$", RegexOptions.Compiled);

        public static Dictionary<string, object> Parse(string output)
        {
            var lines = (output ?? string.Empty).Replace("\r\n", "\n").Split('\n');
            var result = new Dictionary<string, object>
            {
                { "platform", null },
                { "version", null },
                { "java_version", null },
                { "phases", new List<Dictionary<string, object>>() },
                { "errors", new List<Dictionary<string, object>>() },
                { "success", true }
            };

            int i = 0, n = lines.Length;
            while (i < n)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) { i++; continue; }

                var m = VERSION_RE.Match(line);
                if (m.Success)
                {
                    result["platform"] = m.Groups[1].Value.ToUpperInvariant();
                    result["version"] = m.Groups[2].Value;
                    i++; continue;
                }

                m = JAVA_VERSION_RE.Match(line);
                if (m.Success)
                {
                    result["java_version"] = m.Groups[1].Value; i++; continue;
                }

                if (line.StartsWith("Error", StringComparison.OrdinalIgnoreCase))
                {
                    // Use TryGetValue to avoid KeyNotFoundException if 'platform' is missing
                    string platformStr = null;
                    if (result.TryGetValue("platform", out var pObj) && pObj != null) platformStr = pObj.ToString();

                    var tuple = ParseErrorBlock(lines, i, platformStr);
                    var err = tuple.Item1;
                    i = tuple.Item2;

                    // Add to errors list defensively
                    if (result.TryGetValue("errors", out var errsObj) && errsObj is List<Dictionary<string, object>> errsList)
                    {
                        errsList.Add(err);
                    }
                    else
                    {
                        // If the errors list isn't present for some reason, create one
                        result["errors"] = new List<Dictionary<string, object>> { err };
                    }

                    result["success"] = false;
                    continue;
                }

                // phases not strictly parsed here
                i++;
            }

            return result;
        }

        private static Tuple<Dictionary<string, object>, int> ParseErrorBlock(string[] lines, int start, string platform)
        {
            int i = start;
            string firstLine = lines[i].Trim();
            i++;
            var error = new Dictionary<string, object>
            {
                { "kind", "unknown" }, { "platform", platform }, { "module", null }, { "b4x_line", null },
                { "source_line", null }, { "message", "" }, { "java_file", null }, { "java_line", null },
                { "symbol", null }, { "location", null }, { "raw", "" }
            };

            var m = ERROR_LINE_RE.Match(firstLine);
            if (m.Success)
            {
                if (!string.IsNullOrEmpty(m.Groups[1].Value)) error["platform"] = m.Groups[1].Value.ToUpperInvariant();
                error["b4x_line"] = int.Parse(m.Groups[2].Value);

                if (i < lines.Length && !string.IsNullOrWhiteSpace(lines[i]) && !ERROR_DESC_RE.IsMatch(lines[i]))
                {
                    error["source_line"] = lines[i].Trim(); i++;
                }

                if (i < lines.Length)
                {
                    var jm = JAVAC_ERROR_RE_FALLBACK.Match(lines[i].Trim());
                    if (jm.Success)
                    {
                        error["kind"] = "javac";
                        error["java_file"] = jm.Groups[1].Value;
                        error["module"] = InferModuleName(jm.Groups[1].Value);
                        error["java_line"] = int.Parse(jm.Groups[2].Value);
                        error["message"] = jm.Groups[3].Value.Trim();
                        i++;
                        while (i < lines.Length)
                        {
                            var l = lines[i].Trim();
                            if (string.IsNullOrEmpty(l) || l == "^") { i++; continue; }
                            var sm = SYMBOL_RE.Match(l);
                            if (sm.Success) { error["symbol"] = sm.Groups[1].Value.Trim(); i++; continue; }
                            var lm = LOCATION_RE.Match(l);
                            if (lm.Success) { error["location"] = lm.Groups[1].Value.Trim(); i++; continue; }
                            if (Regex.IsMatch(l, "^\\d+\\s+errors?$", RegexOptions.IgnoreCase) || Regex.IsMatch(l, "(?i)^only showing the first")) { i++; break; }
                            i++;
                        }
                    }
                    else
                    {
                        error["kind"] = "syntax";
                        error["message"] = lines[i].Trim(); i++;
                    }
                }
            }
            else
            {
                error["kind"] = "syntax";
                while (i < lines.Length)
                {
                    var l = lines[i].Trim();
                    var dm = ERROR_DESC_RE.Match(l);
                    if (dm.Success) { error["message"] = dm.Groups[1].Value.Trim(); i++; continue; }
                    var lm = ERROR_OCCURRED_RE.Match(l);
                    if (lm.Success) { error["b4x_line"] = int.Parse(lm.Groups[1].Value); i++; continue; }
                    var wm = WORD_RE.Match(l);
                    if (wm.Success) { error["message"] = ((string)error["message"]) + $" (token: {wm.Groups[1].Value.Trim()})"; i++; continue; }
                    if (string.IsNullOrEmpty(l)) { i++; break; }
                    if (error["source_line"] == null) error["source_line"] = l;
                    i++;
                }
            }

            error["raw"] = string.Join("\n", SubArray(lines, start, i - start));
            return Tuple.Create(error, i);
        }

        private static string[] SubArray(string[] arr, int index, int length)
        {
            var res = new string[length];
            Array.Copy(arr, index, res, 0, length);
            return res;
        }

        private static string InferModuleName(string javaPath)
        {
            var parts = javaPath.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            var baseName = parts[^1];
            var withoutExt = Regex.Replace(baseName, "(?i)\\.java$", "");
            return Regex.Replace(withoutExt, "(?i)_subs_\\d+$", "");
        }
    }
}
