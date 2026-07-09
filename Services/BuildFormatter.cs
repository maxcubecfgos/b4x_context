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
            var platform = buildResult.ContainsKey("platform") ? buildResult["platform"]?.ToString() : "?";
            var version = buildResult.ContainsKey("version") ? buildResult["version"]?.ToString() : "";
            sb.AppendLine($"## COMPILATION ERRORS ({platform} {version})\n");

            if (buildResult.TryGetValue("errors", out var errsObj) && errsObj is System.Collections.IEnumerable errs)
            {
                foreach (var o in errs)
                {
                    if (o is Dictionary<string, object> e)
                    {
                        var mod = e.ContainsKey("module") ? e["module"]?.ToString() ?? "(unknown module)" : "(unknown module)";
                        var lineInfo = e.ContainsKey("b4x_line") && e["b4x_line"] != null ? $"line {e["b4x_line"]}" : "";
                        sb.AppendLine($"### {mod} {lineInfo}".Trim());
                        if (e.ContainsKey("source_line") && e["source_line"] != null)
                        {
                            sb.AppendLine("```b4x");
                            sb.AppendLine(e["source_line"].ToString());
                            sb.AppendLine("```");
                        }
                        sb.AppendLine($"**{e.GetValueOrDefault("message", "").ToString()}**");
                        if (e.ContainsKey("symbol") && e["symbol"] != null) sb.AppendLine($"- symbol: {e["symbol"]}");
                        if (e.ContainsKey("location") && e["location"] != null) sb.AppendLine($"- location: {e["location"]}");
                        sb.AppendLine();
                    }
                }
            }

            return sb.ToString();
        }
    }
}
