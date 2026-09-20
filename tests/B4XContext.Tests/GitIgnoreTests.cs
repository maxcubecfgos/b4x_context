using System.IO;
using B4XContext.Services;
using Xunit;

namespace B4XContext.Tests
{
    public class GitIgnoreTests
    {
        private static GitIgnoreMatcher MatcherWith(string gitignoreContent)
        {
            var dir = Path.Combine(Path.GetTempPath(), "gitignore_ut_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, ".gitignore");
            File.WriteAllText(file, gitignoreContent);
            var m = new GitIgnoreMatcher();
            m.AddGitIgnoreFile(file, "");
            return m;
        }

        [Fact]
        public void Simple_suffix_pattern_ignores_files()
        {
            var m = MatcherWith("*.pyc\n*.log\n");
            Assert.True(m.IsIgnored("a.pyc", false));
            Assert.True(m.IsIgnored("sub/deep/b.pyc", false));
            Assert.True(m.IsIgnored("sub/x.log", false));
            Assert.False(m.IsIgnored("a.py", false));
            Assert.False(m.IsIgnored("sub/x.txt", false));
        }

        [Fact]
        public void Negation_reincludes_after_ignore()
        {
            var m = MatcherWith("*.log\n!keep.log\n");
            Assert.True(m.IsIgnored("drop.log", false));
            Assert.False(m.IsIgnored("keep.log", false));
        }

        [Fact]
        public void Dir_only_pattern_matches_directory_and_descendants()
        {
            var m = MatcherWith("build/\ndist_new/\n");
            Assert.True(m.IsIgnored("build", true));
            Assert.False(m.IsIgnored("build", false));
            Assert.True(m.IsIgnored("dist_new", true));
            Assert.False(m.IsIgnored("built", true));
            Assert.True(m.IsIgnored("x/build/y.js", false));
        }

        [Fact]
        public void Non_anchored_component_matches_any_level()
        {
            var m = MatcherWith("temp\n");
            Assert.True(m.IsIgnored("a/temp", true));
            Assert.True(m.IsIgnored("x/y/temp/file.js", false));
            Assert.False(m.IsIgnored("templates/x.js", false));
        }

        [Fact]
        public void Double_star_matches_across_directories()
        {
            var m = MatcherWith("docs/**/*.md\n");
            Assert.True(m.IsIgnored("docs/a/b/c.md", false));
            Assert.True(m.IsIgnored("docs/readme.md", false));
            Assert.False(m.IsIgnored("other/readme.md", false));
        }

        [Fact]
        public void Nested_gitignore_scopes_rules_to_subtree()
        {
            var root = Path.Combine(Path.GetTempPath(), "gitignore_nested_" + System.Guid.NewGuid().ToString("N"));
            var a = Path.Combine(root, "a");
            var b = Path.Combine(root, "b");
            Directory.CreateDirectory(a);
            Directory.CreateDirectory(b);
            try
            {
                File.WriteAllText(Path.Combine(a, ".gitignore"), "*.secret\n");
                var m = new GitIgnoreMatcher();
                m.AddGitIgnoreFile(Path.Combine(a, ".gitignore"), "a");

                Assert.True(m.IsIgnored("a/data.secret", false));
                Assert.False(m.IsIgnored("b/data.secret", false));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Fact]
        public void Deeper_gitignore_negation_overrides_shallower_for_that_subtree()
        {
            var root = Path.Combine(Path.GetTempPath(), "gitignore_override_" + System.Guid.NewGuid().ToString("N"));
            var a = Path.Combine(root, "a");
            Directory.CreateDirectory(a);
            try
            {
                File.WriteAllText(Path.Combine(root, ".gitignore"), "*.log\n");
                var m = new GitIgnoreMatcher();
                m.AddGitIgnoreFile(Path.Combine(root, ".gitignore"), "");
                File.WriteAllText(Path.Combine(a, ".gitignore"), "!keep.log\n");
                m.AddGitIgnoreFile(Path.Combine(a, ".gitignore"), "a");

                Assert.True(m.IsIgnored("a/drop.log", false));
                Assert.False(m.IsIgnored("a/keep.log", false));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Fact]
        public void Blank_lines_and_comments_are_ignored()
        {
            var m = MatcherWith("\n# comment\n*.tmp\n\n");
            Assert.True(m.IsIgnored("x.tmp", false));
            Assert.False(m.IsIgnored("x.txt", false));
        }
    }
}