using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace B4XContext.Services
{
    internal sealed class GitIgnoreRule
    {
        public string Glob;
        public bool Negate;
        public bool DirOnly;
        public bool Anchored;
        public string SourceDir;
    }

    public sealed class GitIgnoreMatcher
    {
        private readonly List<GitIgnoreRule> _rules = new List<GitIgnoreRule>();

        public void AddGitIgnoreFile(string absolutePath, string sourceDir)
        {
            string[] lines;
            try
            {
                lines = File.ReadAllLines(absolutePath);
            }
            catch
            {
                return;
            }

            foreach (var raw in lines)
            {
                var line = raw.TrimEnd();
                if (line.Length == 0 || line[0] == '#') continue;

                bool negate = false;
                if (line[0] == '!')
                {
                    negate = true;
                    line = line.Substring(1).TrimStart();
                }
                if (line.Length == 0) continue;

                bool dirOnly = false;
                if (line.EndsWith("/"))
                {
                    dirOnly = true;
                    line = line.TrimEnd('/').TrimEnd();
                }
                if (line.Length == 0) continue;

                bool anchored = line.Contains("/");
                var glob = line.TrimStart('/');
                if (glob.Length == 0) continue;

                _rules.Add(new GitIgnoreRule
                {
                    Glob = glob,
                    Negate = negate,
                    DirOnly = dirOnly,
                    Anchored = anchored,
                    SourceDir = sourceDir ?? ""
                });
            }
        }

        public bool IsIgnored(string relPath, bool isDir)
        {
            if (string.IsNullOrEmpty(relPath)) return false;

            bool ignored = false;
            foreach (var rule in _rules)
            {
                if (!IsUnderSourceDir(relPath, rule.SourceDir)) continue;

                var rel = ToSourceRelative(relPath, rule.SourceDir);
                if (rel.Length == 0) continue;

                bool matched = Matches(rule, rel, isDir);
                if (!matched && !isDir)
                {
                    var ancestor = ParentOf(rel);
                    while (ancestor != null)
                    {
                        if (Matches(rule, ancestor, true)) { matched = true; break; }
                        ancestor = ParentOf(ancestor);
                    }
                }

                if (matched) ignored = !rule.Negate;
            }
            return ignored;
        }

        private static bool IsUnderSourceDir(string relPath, string sourceDir)
        {
            if (sourceDir.Length == 0) return true;
            if (relPath == sourceDir) return true;
            return relPath.StartsWith(sourceDir + "/", StringComparison.Ordinal);
        }

        private static string ToSourceRelative(string relPath, string sourceDir)
        {
            if (sourceDir.Length == 0) return relPath;
            if (relPath == sourceDir) return "";
            return relPath.Substring(sourceDir.Length + 1);
        }

        private static string ParentOf(string path)
        {
            var idx = path.LastIndexOf('/');
            return idx <= 0 ? null : path.Substring(0, idx);
        }

        private static bool Matches(GitIgnoreRule rule, string path, bool isDir)
        {
            if (rule.DirOnly && !isDir) return false;
            if (path.Length == 0) return false;
            var regex = GlobToRegex(rule.Glob, rule.Anchored);
            return Regex.IsMatch(path, regex);
        }

        internal static string GlobToRegex(string glob, bool anchored)
        {
            var sb = new StringBuilder();
            int i = 0;
            while (i < glob.Length)
            {
                var c = glob[i];
                if (c == '*')
                {
                    bool doubleStar = i + 1 < glob.Length && glob[i + 1] == '*';
                    if (doubleStar)
                    {
                        if (i + 2 < glob.Length && glob[i + 2] == '/')
                        {
                            sb.Append("(?:.*/)?");
                            i += 3;
                            continue;
                        }
                        if (i > 0 && glob[i - 1] == '/')
                        {
                            sb.Append(".*");
                            i += 2;
                            continue;
                        }
                        sb.Append(".*");
                        i += 2;
                        continue;
                    }
                    sb.Append("[^/]*");
                    i += 1;
                    continue;
                }
                if (c == '?')
                {
                    sb.Append("[^/]");
                    i += 1;
                    continue;
                }
                sb.Append(Regex.Escape(c.ToString()));
                i += 1;
            }

            var body = sb.ToString();
            if (anchored) return "^" + body + "$";
            return "(^|/)" + body + "($|/)";
        }
    }
}