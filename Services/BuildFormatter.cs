using System;
using System.Collections.Generic;
using System.Text;

namespace B4XContext.Services
{
    public static class BuildFormatter
    {
        public static string Format(Dictionary<string, object> buildResult)
        {
            if (buildResult == null) return string.Empty;
            bool success = buildResult.ContainsKey("success") && buildResult["success"] is bool b && b;
            if (success) return string.Empty;

            var sb = new StringBuilder();
            var platform = buildResult.TryGetValue("platform", out var pval) && pval != null ? pval.ToString() : "?";
            var version = buildResult.TryGetValue("version", out var vval) && vval != null ? vval.ToString() : "";
            sb.AppendLine($"## COMPILATION ERRORS ({platform} {version})\n");

            if (buildResult.TryGetValue("errors", out var errsObj) && errsObj is System.Collections.IEnumerable errs)
            {
                foreach (var o in errs)
                {
                    if (o is Dictionary<string, object> e)
                    {
                        var mod = e.TryGetValue("module", out var mval) && mval != null ? mval.ToString() : "(unknown module)";
                        var lineInfo = e.TryGetValue("b4x_line", out var lval) && lval != null ? $"line {lval}" : "";
                        sb.AppendLine($"### {mod} {lineInfo}".Trim());
                        if (e.TryGetValue("source_line", out var src) && src != null)
                        {
                            sb.AppendLine("```b4x");
                            sb.AppendLine(src.ToString());
                            sb.AppendLine("```");
                        }
                        var message = e.TryGetValue("message", out var mmsg) && mmsg != null ? mmsg.ToString() : string.Empty;
                        sb.AppendLine($"**{message}**");
                        if (e.TryGetValue("symbol", out var sym) && sym != null) sb.AppendLine($"- symbol: {sym}");
                        if (e.TryGetValue("location", out var loc) && loc != null) sb.AppendLine($"- location: {loc}");
                        sb.AppendLine();
                    }
                }
            }

            return sb.ToString();
        }
    }
}
