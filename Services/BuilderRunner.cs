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
                // Log the exact invocation details to a temporary log file for debug (developer only)
                try
                {
                    var debugLog = Path.Combine(Path.GetTempPath(), "b4x_builder_invoke.log");
                    File.AppendAllText(debugLog, $"\n--- Invocation [{DateTime.Now}] ---\n");
                    File.AppendAllText(debugLog, $"WorkingDirectory: {startInfo.WorkingDirectory}\n");
                    File.AppendAllText(debugLog, $"FileName: {startInfo.FileName}\n");
                    File.AppendAllText(debugLog, $"Arguments: {startInfo.Arguments}\n");
                }
                catch { }

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

                // After process exit, log exit code and raw output for debugging
                try
                {
                    var debugLog = Path.Combine(Path.GetTempPath(), "b4x_builder_invoke.log");
                    File.AppendAllText(debugLog, $"ExitCode: {proc.ExitCode}\n");
                    File.AppendAllText(debugLog, "--- Raw Output Start ---\n");
                    File.AppendAllText(debugLog, output.ToString());
                    File.AppendAllText(debugLog, "\n--- Raw Output End ---\n");
                }
                catch { }

                // If exit code indicates failure and there's no output, surface a clear fatal error
                if (proc.ExitCode != 0 && output.Length == 0)
                {
                    result["fatal_error"] = $"Builder exited with code {proc.ExitCode} and produced no output.";
                    return result;
                }

                var parsed = BuildOutputParser.Parse(output.ToString());
                return parsed;
            }
            catch (System.ComponentModel.Win32Exception wex)
            {
                // File not found or similar OS-level failure starting the process
                result["fatal_error"] = $"Failed to start builder process: {wex.Message}";
                return result;
            }
            catch (Exception ex)
            {
                result["fatal_error"] = ex.Message;
                return result;
            }
        }
    }
}
