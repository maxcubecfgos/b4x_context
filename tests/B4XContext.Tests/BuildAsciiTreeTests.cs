using B4XContext.Models;
using B4XContext.Services;
using Xunit;

namespace B4XContext.Tests
{
    public class BuildAsciiTreeTests
    {
        [Fact]
        public void BuildAsciiTree_renders_hierarchical_tree_sorted_interleaved_with_stats()
        {
            var files = new[]
            {
                Pf("models/user.py", "models", 2048, 42),
                Pf("models/user.ts", "models", 1024, 13),
                Pf("models/views/home.py", "models/views", 500, 5),
                Pf("server.py", "", 31, 3)
            };

            var tree = BundleBuilder.BuildAsciiTree(files).Replace("\r\n", "\n");

            var expected = "|- models\n" +
                           "|  |- user.py (2 KB, 42 lines)\n" +
                           "|  |- user.ts (1 KB, 13 lines)\n" +
                           "|  \\- views\n" +
                           "|     \\- home.py (500 B, 5 lines)\n" +
                           "\\- server.py (31 B, 3 lines)";
            Assert.Equal(expected, tree);
        }

        [Fact]
        public void BuildAsciiTree_normalizes_backslashes()
        {
            var files = new[]
            {
                Pf("a/b/c.py", "a/b", 100, 1),
                Pf("a/b/d.py", "a/b", 100, 1)
            };
            var tree = BundleBuilder.BuildAsciiTree(files).Replace("\r\n", "\n");
            Assert.Contains("\\- a\n   \\- b\n      |- c.py (100 B, 1 lines)\n      \\- d.py (100 B, 1 lines)", tree);
        }

        private static ProjectFile Pf(string name, string relativeDir, long size, int lines)
        {
            return new ProjectFile(@"C:\x\" + relativeDir.Replace('/', '\\') + (relativeDir.Length == 0 ? "" : "\\") + name)
            {
                Kind = System.IO.Path.GetExtension(name).TrimStart('.'),
                RelativeDirectory = relativeDir,
                Size = size,
                LineCount = lines
            };
        }

        [Fact]
        public void DisplayPath_uses_name_for_root_and_relative_dir_otherwise()
        {
            Assert.Equal("server.py", BundleBuilder.DisplayPath(new ProjectFile(@"C:\x\server.py") { RelativeDirectory = "" }));
            Assert.Equal("models/user.py", BundleBuilder.DisplayPath(new ProjectFile(@"C:\x\models\user.py") { RelativeDirectory = "models" }));
        }

        [Fact]
        public void BuildMarkdown_file_tree_uses_relative_paths()
        {
            var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "b4x_ctx_tree_" + System.Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(dir, "models"));
            try
            {
                var path = System.IO.Path.Combine(dir, "models", "user.py");
                System.IO.File.WriteAllText(path, "def run():\n    return 1\n");
                var pf = new ProjectFile(path) { Kind = "py", RelativeDirectory = "models", Included = true };
                var md = BundleBuilder.BuildMarkdown("", "", new[] { pf });

                Assert.Contains("## FILE TREE", md);
                Assert.Contains("\\- models", md);
                Assert.Contains("\\- user.py", md);
                Assert.DoesNotContain(path, md);
                Assert.DoesNotContain("models/", md);
                Assert.DoesNotContain("return 1", md);
            }
            finally
            {
                System.IO.Directory.Delete(dir, true);
            }
        }
    }
}