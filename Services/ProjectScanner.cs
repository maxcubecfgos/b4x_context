using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using B4XContext.Models;

namespace B4XContext.Services
{
    public static class ProjectScanner
    {
        private static readonly string[] IgnoredFolders = new[] { "Objects", "bin", "gen", "obj", ".git" };

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

            foreach (var f in Directory.EnumerateFiles(projectRoot, "*.*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(projectRoot, f);
                if (IgnoredFolders.Any(x => rel.Split(Path.DirectorySeparatorChar).Contains(x)))
                    continue;

                var ext = Path.GetExtension(f).ToLowerInvariant();
                if (ext == ".bas" || ext == ".bal" || ext == ".bjl" || ext == ".bil" || ext == ".b4a" || ext == ".b4j" || ext == ".b4i")
                {
                    var pf = new ProjectFile(f)
                    {
                        Kind = ext.TrimStart('.')
                    };
                    files.Add(pf);
                }
            }

            return files.OrderBy(p => p.Name).ToList();
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
