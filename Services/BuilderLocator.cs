using System;
using System.IO;
using System.Text.Json;

namespace B4XContext.Services
{
    public static class BuilderLocator
    {
        private static readonly string[] CommonPaths = new[]
        {
            @"C:\Program Files (x86)\Anywhere Software\B4A\B4ABuilder.exe",
            @"C:\Program Files (x86)\Anywhere Software\B4J\B4JBuilder.exe",
            @"C:\Program Files\Anywhere Software\B4A\B4ABuilder.exe",
            @"C:\Program Files\Anywhere Software\B4J\B4JBuilder.exe",
        };

        public static string LocateBuilder(string projectPathOrRoot)
        {
            // Determine project directory and extension (if a project file was passed)
            string projectDir = projectPathOrRoot ?? ".";
            string projFile = null;
            string ext = null;
            try
            {
                if (File.Exists(projectPathOrRoot))
                {
                    projFile = projectPathOrRoot;
                    projectDir = Path.GetDirectoryName(projFile) ?? ".";
                    ext = Path.GetExtension(projFile)?.ToLowerInvariant();
                }
                else if (Directory.Exists(projectPathOrRoot))
                {
                    projectDir = projectPathOrRoot;
                    // Try find a project file in the directory
                    try
                    {
                        projFile = ProjectScanner.FindProjectFile(projectDir);
                        if (!string.IsNullOrEmpty(projFile)) ext = Path.GetExtension(projFile)?.ToLowerInvariant();
                    }
                    catch { }
                }
            }
            catch { }

            // Map extension to expected builder executable
            string expectedBuilderName = null;
            if (!string.IsNullOrEmpty(ext))
            {
                switch (ext)
                {
                    case ".b4j": expectedBuilderName = "B4JBuilder.exe"; break;
                    case ".b4a": expectedBuilderName = "B4ABuilder.exe"; break;
                    case ".b4i": expectedBuilderName = "B4iBuilder.exe"; break;
                }
            }

            // Check config override (must match expected builder if we know the project type)
            try
            {
                var cfg = Path.Combine(projectDir ?? ".", "b4x_context_config.json");
                if (File.Exists(cfg))
                {
                    var txt = File.ReadAllText(cfg);
                    using var doc = JsonDocument.Parse(txt);
                    if (doc.RootElement.TryGetProperty("builder_path", out var bp))
                    {
                        var path = bp.GetString();
                        if (!string.IsNullOrEmpty(path) && File.Exists(path))
                        {
                            if (expectedBuilderName != null)
                            {
                                if (path.EndsWith(expectedBuilderName, StringComparison.OrdinalIgnoreCase))
                                    return path;
                                // Config override does not match expected builder for this project type -> treat as not found
                                return null;
                            }
                            return path;
                        }
                    }
                }
            }
            catch { }

            // If we know the expected builder name, prefer paths that end with that name
            if (!string.IsNullOrEmpty(expectedBuilderName))
            {
                foreach (var p in CommonPaths)
                {
                    if (p.EndsWith(expectedBuilderName, StringComparison.OrdinalIgnoreCase) && File.Exists(p))
                        return p;
                }

                // As a fallback, if expected builder not found, return null to avoid running wrong builder
                return null;
            }

            // No explicit expected builder, fall back to first existing common path
            foreach (var p in CommonPaths)
            {
                if (File.Exists(p))
                    return p;
            }

            return null;
        }
    }
}
