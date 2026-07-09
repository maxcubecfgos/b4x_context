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
        };

        public static string LocateBuilder(string projectRoot)
        {
            // Check config override
            try
            {
                var cfg = Path.Combine(projectRoot ?? ".", "b4x_context_config.json");
                if (File.Exists(cfg))
                {
                    var txt = File.ReadAllText(cfg);
                    using var doc = JsonDocument.Parse(txt);
                    if (doc.RootElement.TryGetProperty("builder_path", out var bp))
                    {
                        var path = bp.GetString();
                        if (!string.IsNullOrEmpty(path) && File.Exists(path))
                            return path;
                    }
                }
            }
            catch { }

            foreach (var p in CommonPaths)
            {
                if (File.Exists(p))
                    return p;
            }

            return null;
        }
    }
}
