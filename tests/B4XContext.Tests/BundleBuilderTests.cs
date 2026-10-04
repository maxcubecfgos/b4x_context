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

        [Fact]
        public void BuildMarkdown_emits_preamble_task_and_bundle_header()
        {
            var md = BundleBuilder.BuildMarkdown("PRE EMPTY\n\nEXTRA", "TASK LINE", ArrayEmpty(), includeFileTree: false);

            Assert.Contains("# Context Bundle", md);
            Assert.Contains("## PREAMBLE / CONTEXT", md);
            Assert.Contains("PRE EMPTY", md);
            Assert.Contains("## TASK / QUERY", md);
            Assert.Contains("TASK LINE", md);
            Assert.DoesNotContain("(none)", md);
        }

        [Fact]
        public void BuildMarkdown_shows_none_placeholders_when_blank()
        {
            var md = BundleBuilder.BuildMarkdown("", "", ArrayEmpty(), includeFileTree: false);

            Assert.Contains("(none)", md);
        }

        [Fact]
        public void BuildMarkdown_emits_compile_errors_block()
        {
            var md = BundleBuilder.BuildMarkdown("", "", ArrayEmpty(), includeFileTree: false,
                compileErrors: "## COMPILATION ERRORS (B4A 12.50)");

            Assert.Contains("## COMPILATION ERRORS (B4A 12.50)", md);
            Assert.Contains("---", md);
        }

        [Fact]
        public void BuildMarkdown_tree_truncates_above_4000_lines()
        {
            var files = new System.Collections.Generic.List<ProjectFile>();
            for (int i = 0; i < 4200; i++)
                files.Add(new ProjectFile(System.IO.Path.Combine(@"C:\x", $"f{i}.txt"))
                {
                    Kind = "txt",
                    RelativeDirectory = ""
                });

            var md = BundleBuilder.BuildMarkdown("", "", files);

            Assert.Contains("... tree truncated ...", md);
        }

        [Fact]
        public void BuildMarkdown_decodes_bal_layout_full_as_json()
        {
            var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "b4x_ctx_bal_" + System.Guid.NewGuid().ToString("N"));
            try
            {
                var pf = new ProjectFile(BalFixture.WriteTempBal(dir))
                {
                    Kind = "bal",
                    Mode = FileMode.Full,
                    Included = true
                };
                var md = BundleBuilder.BuildMarkdown("", "", new[] { pf });

                Assert.Contains("### Layout.bal   (Full)", md);
                Assert.Contains("```json", md);
                Assert.Contains("\"name\": \"Main\"", md);
                Assert.Contains("Button1", md);
            }
            finally
            {
                System.IO.Directory.Delete(dir, true);
            }
        }

        [Fact]
        public void BuildMarkdown_decodes_bal_layout_skeleton_as_text_outline()
        {
            var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "b4x_ctx_bal_" + System.Guid.NewGuid().ToString("N"));
            try
            {
                var pf = new ProjectFile(BalFixture.WriteTempBal(dir))
                {
                    Kind = "bal",
                    Mode = FileMode.Skeleton,
                    Included = true
                };
                var md = BundleBuilder.BuildMarkdown("", "", new[] { pf });

                Assert.Contains("### Layout.bal   (Skeleton)", md);
                Assert.Contains("```text", md);
                Assert.Contains("- Button1 (android.widget.Button) 100x40 @ (10,20)", md);
                Assert.DoesNotContain("```json", md);
            }
            finally
            {
                System.IO.Directory.Delete(dir, true);
            }
        }

        [Fact]
        public void BuildMarkdown_compact_layout_pins_task_and_rules_at_top()
        {
            var md = BundleBuilder.BuildMarkdown("MY CONTEXT", "MY TASK", ArrayEmpty(), includeFileTree: false);

            int task = md.IndexOf("## TASK / QUERY");
            int rules = md.IndexOf("## RESPONSE RULES");
            int preamble = md.IndexOf("## PREAMBLE / CONTEXT");
            int files = md.IndexOf("## FILES");

            Assert.True(task >= 0 && rules > task && preamble > rules && files > preamble,
                $"expected TASK < RULES < PREAMBLE < FILES, got {task}, {rules}, {preamble}, {files}");
            Assert.Contains("Do not invent APIs", md);
            Assert.Contains("ask one short question", md);
            Assert.Contains("MY TASK", md);
        }

        [Fact]
        public void BuildMarkdown_compact_rules_can_be_disabled()
        {
            var md = BundleBuilder.BuildMarkdown("PRE", "TASK", ArrayEmpty(), includeFileTree: false,
                compactRules: false);

            Assert.DoesNotContain("## RESPONSE RULES", md);
            Assert.Contains("## TASK / QUERY", md);
        }

        [Fact]
        public void BuildMarkdown_uses_summary_instead_of_source_when_compacted()
        {
            var dir = Path.Combine(Path.GetTempPath(), "b4x_ctx_sum_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "Mod.bas");
            File.WriteAllText(path, "Sub One\n\tLog(1)\nEnd Sub\n");

            try
            {
                var pf = new ProjectFile(path)
                {
                    Kind = "bas",
                    Mode = FileMode.Full,
                    Included = true,
                    Summary = "## Purpose\n- does one thing",
                    SummaryTokens = 42,
                    UseSummary = true
                };

                var md = BundleBuilder.BuildMarkdown("", "", new[] { pf });

                Assert.Contains("### Mod.bas   (Summary)", md);
                Assert.Contains("## Purpose", md);
                Assert.DoesNotContain("Log(1)", md);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Fact]
        public void BuildMarkdown_summary_is_skipped_when_not_used()
        {
            var dir = Path.Combine(Path.GetTempPath(), "b4x_ctx_nosum_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "Mod.bas");
            File.WriteAllText(path, "Sub One\n\tLog(1)\nEnd Sub\n");

            try
            {
                var pf = new ProjectFile(path)
                {
                    Kind = "bas",
                    Mode = FileMode.Skeleton,
                    Included = true,
                    Summary = "stale summary",
                    UseSummary = false
                };

                var md = BundleBuilder.BuildMarkdown("", "", new[] { pf });

                Assert.Contains("### Mod.bas   (Skeleton)", md);
                Assert.DoesNotContain("stale summary", md);
                Assert.Contains("Sub One", md);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        private static ProjectFile[] ArrayEmpty() => System.Array.Empty<ProjectFile>();
    }
}