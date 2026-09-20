using System.IO;
using System.Linq;
using B4XContext.Models;
using B4XContext.Services;
using Xunit;
using FileMode = B4XContext.Models.FileMode;

namespace B4XContext.Tests
{
    public class BundleBuilderTests
    {
        [Fact]
        public void BuildMarkdown_custom_mode_emits_granular_bundle()
        {
            var dir = Path.Combine(Path.GetTempPath(), "b4x_ctx_test_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "Mod.bas");
            File.WriteAllText(path, "'Header\nSub One\n\tLog(1)\nEnd Sub\n\nSub Two\n\tLog(2)\nEnd Sub\n");

            try
            {
                var pf = new ProjectFile(path)
                {
                    Kind = "bas",
                    Mode = FileMode.Custom,
                    Included = true
                };
                pf.Items.Add(new ModuleItem { Kind = ModuleItemKind.Sub, Name = "One", Signature = "Sub One", StartLine = 2, EndLine = 4 });
                pf.Items.Add(new ModuleItem { Kind = ModuleItemKind.Sub, Name = "Two", Signature = "Sub Two", StartLine = 6, EndLine = 8, IsSelected = true });

                var md = BundleBuilder.BuildMarkdown("", "", new[] { pf });

                Assert.Contains("### Mod.bas   (Custom)", md);
                Assert.Contains("2 items (1 selected)", md);
                Assert.Contains("Log(2)", md);
                Assert.DoesNotContain("Log(1)", md);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Fact]
        public void BuildMarkdown_generic_python_skeleton_uses_py_fence()
        {
            var dir = Path.Combine(Path.GetTempPath(), "b4x_ctx_test_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "app.py");
            File.WriteAllText(path, "import os\n\ndef run():\n    print(os.name)\n    return 1\n");

            try
            {
                var pf = new ProjectFile(path) { Kind = "py", Mode = FileMode.Skeleton, Included = true };
                var md = BundleBuilder.BuildMarkdown("", "", new[] { pf });

                Assert.Contains("### app.py   (Skeleton)", md);
                Assert.Contains("```py", md);
                Assert.Contains("def run():", md);
                Assert.Contains("import os", md);
                Assert.DoesNotContain("print(os.name)", md);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Fact]
        public void BuildMarkdown_generic_python_full_uses_py_fence_and_full_body()
        {
            var dir = Path.Combine(Path.GetTempPath(), "b4x_ctx_test_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "app.py");
            File.WriteAllText(path, "import os\n\ndef run():\n    print(os.name)\n    return 1\n");

            try
            {
                var pf = new ProjectFile(path) { Kind = "py", Mode = FileMode.Full, Included = true };
                var md = BundleBuilder.BuildMarkdown("", "", new[] { pf });

                Assert.Contains("```py", md);
                Assert.Contains("print(os.name)", md);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Fact]
        public void BuildMarkdown_generic_json_uses_json_fence_and_passes_through()
        {
            var dir = Path.Combine(Path.GetTempPath(), "b4x_ctx_test_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "cfg.json");
            File.WriteAllText(path, "{\n  \"a\": 1\n}\n");

            try
            {
                var pf = new ProjectFile(path) { Kind = "json", Mode = FileMode.Skeleton, Included = true };
                var md = BundleBuilder.BuildMarkdown("", "", new[] { pf });

                Assert.Contains("```json", md);
                Assert.Contains("\"a\": 1", md);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Fact]
        public void BuildMarkdown_mixed_project_b4x_and_generic_keeps_b4x_intact()
        {
            var dir = Path.Combine(Path.GetTempPath(), "b4x_ctx_test_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var bas = Path.Combine(dir, "Main.bas");
            File.WriteAllText(bas, "'Main\nSub Hello\n\tLog(1)\nEnd Sub\n");
            var py = Path.Combine(dir, "lib.py");
            File.WriteAllText(py, "def helper():\n    return 2\n");

            try
            {
                var basPf = new ProjectFile(bas) { Kind = "bas", Mode = FileMode.Skeleton, Included = true };
                var pyPf = new ProjectFile(py) { Kind = "py", Mode = FileMode.Skeleton, Included = true };
                var md = BundleBuilder.BuildMarkdown("", "", new[] { basPf, pyPf });

                Assert.Contains("```b4x", md);
                Assert.Contains("Sub Hello", md);
                Assert.DoesNotContain("Log(1)", md);
                Assert.Contains("```py", md);
                Assert.Contains("def helper():", md);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }
    }
}