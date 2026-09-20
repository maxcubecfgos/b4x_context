using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using B4XContext.Models;

namespace B4XContext.Services
{
    public static class AutoPreambleGenerator
    {
        private static readonly string[] StackKeywords = new[]
        {
            "react", "vue", "svelte", "next", "nuxt", "tailwindcss", "typescript", "vite",
            "tauri", "electron", "express", "fastify", "nestjs"
        };

        private static readonly HashSet<string> SkippedProfileExts = new HashSet<string>
        {
            "png", "jpg", "jpeg", "svg", "ico", "lock", "json", "map"
        };

        public static string Generate(IEnumerable<ProjectFile> files, Func<string, string> readText)
        {
            var all = files.ToList();
            var rootFiles = all.Where(f => string.IsNullOrEmpty(f.RelativeDirectory)).ToList();
            var parts = new List<string>();

            var manifest = ScanManifests(rootFiles, readText);
            if (manifest != null) parts.Add(manifest);

            var readme = ScanReadme(rootFiles, readText);
            if (readme != null) parts.Add(readme);

            var stats = CalculateStats(all);
            if (!string.IsNullOrEmpty(stats)) parts.Add(stats);

            return string.Join("\n\n", parts);
        }

        private static string ScanManifests(List<ProjectFile> rootFiles, Func<string, string> readText)
        {
            var output = "";

            var pkgJson = rootFiles.FirstOrDefault(f => string.Equals(f.Name, "package.json", StringComparison.OrdinalIgnoreCase));
            if (pkgJson != null)
            {
                try
                {
                    var content = readText(pkgJson.Path);
                    if (string.IsNullOrWhiteSpace(content))
                        return null;
                    using var doc = JsonDocument.Parse(content);
                    var root = doc.RootElement;
                    if (root.ValueKind != JsonValueKind.Object)
                        return null;

                    var name = SafeString(root, "name") ?? "Untitled";
                    output += $"Project: {name}\n";
                    var desc = SafeString(root, "description");
                    if (desc != null) output += $"Description: {desc}\n";

                    var deps = new List<string>();
                    if (root.TryGetProperty("dependencies", out var depEl) && depEl.ValueKind == JsonValueKind.Object)
                        foreach (var p in depEl.EnumerateObject())
                            deps.Add(p.Name);
                    if (root.TryGetProperty("devDependencies", out var devEl) && devEl.ValueKind == JsonValueKind.Object)
                        foreach (var p in devEl.EnumerateObject())
                            deps.Add(p.Name);

                    var important = deps.Where(k => StackKeywords.Any(s => k.ToLowerInvariant().Contains(s)))
                        .Take(10).ToList();
                    if (important.Count > 0)
                        output += $"Key Stack (Node): {string.Join(", ", important)}\n";
                }
                catch
                {
                }
            }

            var cargoToml = rootFiles.FirstOrDefault(f => string.Equals(f.Name, "Cargo.toml", StringComparison.OrdinalIgnoreCase));
            if (cargoToml != null)
            {
                try
                {
                    var content = readText(cargoToml.Path);
                    if (!string.IsNullOrWhiteSpace(content))
                    {
                        var nameMatch = System.Text.RegularExpressions.Regex.Match(content, @"^\s*name\s*=\s*['""]([^'""]+)['""]", System.Text.RegularExpressions.RegexOptions.Multiline);
                        var descMatch = System.Text.RegularExpressions.Regex.Match(content, @"^\s*description\s*=\s*['""]([^'""]+)['""]", System.Text.RegularExpressions.RegexOptions.Multiline);

                        var name = nameMatch.Success ? nameMatch.Groups[1].Value : "";
                        var desc = descMatch.Success ? descMatch.Groups[1].Value : "";

                        if (name.Length > 0 && !output.Contains(name))
                            output += $"Project (Rust): {name}\n";
                        if (desc.Length > 0 && !output.Contains(desc))
                            output += $"Description: {desc}\n";

                        if (!string.IsNullOrEmpty(output) && !output.Contains("Key Stack (Node)"))
                            output += "Stack Hint: Rust Project detected.\n";
                    }
                }
                catch
                {
                }
            }

            return string.IsNullOrEmpty(output.Trim()) ? null : output.Trim();
        }

        private static string ScanReadme(List<ProjectFile> rootFiles, Func<string, string> readText)
        {
            var readme = rootFiles.FirstOrDefault(f => string.Equals(f.Name, "README.md", StringComparison.OrdinalIgnoreCase));
            if (readme == null) return null;

            try
            {
                var content = readText(readme.Path);
                var lines = content.Split('\n');

                int startIdx = -1;
                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i].ToLowerInvariant();
                    if (line.Contains("architecture") || line.Contains("flow")
                        || lines[i].Contains("\u250c") || lines[i].Contains("\u2554"))
                    {
                        startIdx = i;
                        break;
                    }
                }

                if (startIdx != -1)
                {
                    int endIdx = -1;
                    for (int j = startIdx + 1; j < lines.Length && j < startIdx + 25; j++)
                    {
                        if (lines[j].Trim() == "" && (j + 1 < lines.Length) && lines[j + 1].Trim() == "")
                        {
                            endIdx = j;
                            break;
                        }
                        if (lines[j].StartsWith("#") && j > startIdx + 5)
                        {
                            endIdx = j;
                            break;
                        }
                        endIdx = j;
                    }

                    if (endIdx == -1)
                        endIdx = Math.Min(lines.Length, startIdx + 25);
                    int blockStart = Math.Max(0, startIdx - 1);
                    var block = string.Join("\n", lines.Skip(blockStart).Take(Math.Max(0, endIdx - blockStart)));
                    return $"Project Context:\n{block}";
                }

                var summary = string.Join("\n", lines.Take(15).Where(l => !l.TrimStart().StartsWith("[!")));
                return $"Project Overview:\n{summary}";
            }
            catch
            {
                return null;
            }
        }

        private static string CalculateStats(List<ProjectFile> files)
        {
            var stats = new Dictionary<string, int>();
            int total = 0;
            foreach (var f in files)
            {
                var name = f.Name;
                var ext = name.Contains('.') ? Path.GetExtension(name).TrimStart('.').ToLowerInvariant() : name;
                if (ext.Length == 0) ext = "no-ext";
                if (SkippedProfileExts.Contains(ext)) continue;
                stats.TryGetValue(ext, out var c);
                stats[ext] = c + 1;
                total++;
            }

            var sorted = stats.OrderByDescending(kv => kv.Value)
                .Take(5)
                .Select(kv => $"{kv.Key} ({Math.Round((double)kv.Value * 100.0 / total)}%)");
            var joined = string.Join(", ", sorted);
            return joined.Length == 0 ? "" : $"Codebase Profile: {joined}";
        }

        private static string SafeString(JsonElement el, string prop)
        {
            if (el.TryGetProperty(prop, out var value) && value.ValueKind == JsonValueKind.String)
                return value.GetString();
            return null;
        }
    }
}