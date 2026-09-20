using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using B4XContext.Models;

namespace B4XContext.Services
{
    public static class ProjectScanner
    {
        private static readonly string[] IgnoredFolders = new[]
        {
            "Objects", "bin", "gen", "obj", ".git", "node_modules", "target", "build", "dist", ".venv",
            "__pycache__", ".next", "vendor", "coverage", ".hg", ".svn", ".vscode", ".idea", ".cache",
            ".parcel-cache", ".turbo", ".nuxt", ".svelte-kit", ".astro", ".vite", ".vercel", ".netlify",
            ".expo", ".gradle", ".cxx", ".pytest_cache", ".mypy_cache", ".ruff_cache", ".tox",
            ".nyc_output", "__pypackages__", "tmp", "temp", "logs", "log", "venv", "bower_components",
            "jspm_packages", ".pnpm-store", ".yarn", "pods", "deriveddata", "out"
        };

        private static readonly string[] IgnoredFileNames = new[]
        {
            ".ds_store", "thumbs.db", "desktop.ini"
        };

        private static readonly string[] IgnoredFileSuffixes = new[]
        {
            ".png", ".jpg", ".jpeg", ".gif", ".webp", ".ico", ".bmp", ".tiff", ".svg", ".psd", ".ai", ".heic", ".avif",
            ".woff", ".woff2", ".ttf", ".eot", ".otf",
            ".exe", ".dll", ".so", ".dylib", ".bin", ".obj", ".o", ".a", ".lib", ".class", ".jar", ".war", ".ear", ".pdb", ".wasm", ".node",
            ".pdf", ".zip", ".tar", ".gz", ".tgz", ".bz2", ".xz", ".7z", ".rar", ".iso", ".dmg", ".pkg", ".deb", ".rpm",
            ".mp4", ".mov", ".mkv", ".avi", ".webm", ".wmv", ".mpg", ".mpeg",
            ".mp3", ".wav", ".flac", ".aac", ".m4a", ".ogg",
            ".csv", ".tsv", ".parquet", ".arrow", ".db", ".sqlite", ".sqlite3", ".duckdb", ".rdb", ".pkl", ".pickle",
            ".doc", ".docx", ".ppt", ".pptx", ".xls", ".xlsx", ".key", ".pages", ".numbers",
            ".log", ".map", ".cache", ".min.js", ".min.css", ".bak", ".lock", ".icns",
        };

        public static string FindProjectRoot(string startPath)
        {
            if (string.IsNullOrEmpty(startPath))
                return null;

            var dir = new DirectoryInfo(Path.GetDirectoryName(startPath) ?? startPath);
            while (dir != null)
            {
                var files = dir.GetFiles("*.b4a").Concat(dir.GetFiles("*.b4j")).Concat(dir.GetFiles("*.b4i"));
                if (files.Any())
                    return dir.FullName;
                dir = dir.Parent;
            }
            return null;
        }

        public static List<ProjectFile> ScanProject(string projectRoot)
        {
            var files = new List<ProjectFile>();
            if (string.IsNullOrEmpty(projectRoot) || !Directory.Exists(projectRoot))
                return files;

            var matcher = new GitIgnoreMatcher();
            var stack = new Stack<string>();
            stack.Push(projectRoot);

            while (stack.Count > 0)
            {
                var dir = stack.Pop();
                AddIgnoreRules(matcher, projectRoot, dir);

                foreach (var sub in Directory.EnumerateDirectories(dir))
                {
                    if (IsIgnoredDir(matcher, projectRoot, sub))
                        continue;
                    stack.Push(sub);
                }

                foreach (var f in Directory.EnumerateFiles(dir))
                {
                    if (IsIgnoredFile(matcher, projectRoot, f))
                        continue;

                    var name = Path.GetFileName(f);
                    var nameLower = name.ToLowerInvariant();
                    if (IgnoredFileNames.Contains(nameLower)
                        || IgnoredFileSuffixes.Any(s => nameLower.EndsWith(s)))
                        continue;

                    var ext = Path.GetExtension(f).TrimStart('.').ToLowerInvariant();
                    var pf = new ProjectFile(f)
                    {
                        Kind = ext,
                        RelativeDirectory = RelativeDir(projectRoot, f),
                        Size = new FileInfo(f).Length
                    };
                    files.Add(pf);
                }
            }

            return files
                .OrderBy(f => f.RelativeDirectory, StringComparer.OrdinalIgnoreCase)
                .ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static void AddIgnoreRules(GitIgnoreMatcher matcher, string projectRoot, string dir)
        {
            var gitIgnore = Path.Combine(dir, ".gitignore");
            if (!File.Exists(gitIgnore))
                return;

            var relDir = Path.GetRelativePath(projectRoot, dir).Replace('\\', '/');
            if (relDir == ".")
                relDir = "";
            matcher.AddGitIgnoreFile(gitIgnore, relDir);
        }

        private static bool IsIgnoredDir(GitIgnoreMatcher matcher, string projectRoot, string dir)
        {
            var info = new DirectoryInfo(dir);
            var name = info.Name;

            if (IgnoredFolders.Contains(name.ToLowerInvariant()) || name.StartsWith("."))
                return true;

            if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
                return true;

            var rel = Path.GetRelativePath(projectRoot, dir).Replace('\\', '/');
            return matcher.IsIgnored(rel, true);
        }

        private static bool IsIgnoredFile(GitIgnoreMatcher matcher, string projectRoot, string file)
        {
            var name = Path.GetFileName(file);
            if (name.StartsWith("."))
                return true;

            var rel = Path.GetRelativePath(projectRoot, file).Replace('\\', '/');
            return matcher.IsIgnored(rel, false);
        }

        private static string RelativeDir(string projectRoot, string fullPath)
        {
            var rel = Path.GetRelativePath(projectRoot, fullPath).Replace('\\', '/');
            return (Path.GetDirectoryName(rel) ?? "").Replace('\\', '/');
        }

        public static string FindProjectFile(string projectRoot)
        {
            if (string.IsNullOrEmpty(projectRoot) || !Directory.Exists(projectRoot))
                return null;

            foreach (var fn in Directory.GetFiles(projectRoot))
            {
                var low = Path.GetExtension(fn).ToLowerInvariant();
                if (low == ".b4a" || low == ".b4j" || low == ".b4i")
                    return fn;
            }
            return null;
        }
    }
}