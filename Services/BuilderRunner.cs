using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using B4XContext.Engine;

namespace B4XContext.Services
{
    public static class BuilderRunner
    {
        public static Dictionary<string, object> RunBuild(string builderPath, string projectFile, int timeoutSeconds = 300)
        {
            var result = new Dictionary<string, object> { { "success", false }, { "errors", new List<Dictionary<string, object>>() } };

            if (string.IsNullOrEmpty(builderPath) || !File.Exists(builderPath))
            {
                result["fatal_error"] = $"Builder not found at: {builderPath}";
                return result;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = builderPath,
                Arguments = $"-Task=build -Project=\"{projectFile}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(projectFile)
            };

            try
            {
                using var proc = Process.Start(startInfo);
                if (proc == null)
                {
                    result["fatal_error"] = "Failed to start builder process.";
                    return result;
                }

                var output = new StringBuilder();
                proc.OutputDataReceived += (s, e) => { if (e.Data != null) output.AppendLine(e.Data); };
                proc.ErrorDataReceived += (s, e) => { if (e.Data != null) output.AppendLine(e.Data); };
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
                if (!proc.WaitForExit(timeoutSeconds * 1000))
                {
                    try { proc.Kill(); } catch { }
                    result["fatal_error"] = $"Build timed out after {timeoutSeconds}s.";
                    return result;
                }

                var parsed = BuildOutputParser.Parse(output.ToString());
                return parsed;
            }
            catch (Exception ex)
            {
                result["fatal_error"] = ex.Message;
                return result;
            }
        }
    }
}
