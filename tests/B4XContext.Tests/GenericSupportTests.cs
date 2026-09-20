using System;
using System.Linq;
using B4XContext.Engine;
using B4XContext.Models;
using B4XContext.Services;
using Xunit;

namespace B4XContext.Tests
{
    public class GenericSupportTests
    {
        [Theory]
        [InlineData("bas")]
        [InlineData("b4a")]
        [InlineData("b4j")]
        [InlineData("b4i")]
        public void IsB4x_returns_true_for_b4x_kinds(string kind)
        {
            var pf = new ProjectFile(@"C:\proj\File." + kind) { Kind = kind };
            Assert.True(pf.IsB4x);
            Assert.Equal(pf.IsCodeFile, pf.IsB4x);
        }

        [Theory]
        [InlineData("cs")]
        [InlineData("py")]
        [InlineData("ts")]
        [InlineData("txt")]
        public void IsB4x_returns_false_for_non_b4x_kinds(string kind)
        {
            var pf = new ProjectFile(@"C:\proj\File." + kind) { Kind = kind };
            Assert.False(pf.IsB4x);
        }

        [Theory]
        [InlineData("py")]
        [InlineData("pyw")]
        [InlineData("pyi")]
        [InlineData("ts")]
        [InlineData("mts")]
        [InlineData("cts")]
        [InlineData("tsx")]
        [InlineData("js")]
        [InlineData("mjs")]
        [InlineData("cjs")]
        [InlineData("jsx")]
        [InlineData("rs")]
        [InlineData("go")]
        [InlineData("c")]
        [InlineData("h")]
        [InlineData("cs")]
        [InlineData("json")]
        [InlineData("jsonc")]
        [InlineData("css")]
        [InlineData("scss")]
        [InlineData("less")]
        [InlineData("html")]
        [InlineData("htm")]
        [InlineData("xaml")]
        [InlineData("csproj")]
        [InlineData("sln")]
        [InlineData("slnx")]
        public void IsGenericText_returns_true_for_generic_kinds(string kind)
        {
            var pf = new ProjectFile(@"C:\proj\File." + kind) { Kind = kind };
            Assert.True(pf.IsGenericText);
            Assert.False(pf.IsCodeFile);
        }

        [Theory]
        [InlineData("bas")]
        [InlineData("bal")]
        [InlineData("bjl")]
        [InlineData("bil")]
        [InlineData("b4a")]
        [InlineData("b4j")]
        [InlineData("b4i")]
        public void IsGenericText_returns_false_for_b4x_kinds(string kind)
        {
            var pf = new ProjectFile(@"C:\proj\File." + kind) { Kind = kind };
            Assert.False(pf.IsGenericText);
        }

[Theory]
        [InlineData("txt")]
        [InlineData("xml")]
        [InlineData("md")]
        [InlineData("mdx")]
        [InlineData("xyz")]
        public void IsGenericText_returns_true_for_any_non_b4x_kind(string kind)
        {
            var pf = new ProjectFile(@"C:\proj\File." + kind) { Kind = kind };
            Assert.True(pf.IsGenericText);
        }

        [Fact]
        public void IsGenericText_returns_true_for_extensionless_kind()
        {
            var pf = new ProjectFile(@"C:\proj\LICENSE") { Kind = "" };
            Assert.True(pf.IsGenericText);
        }

        [Fact]
        public void ScanProject_ignores_generic_noise_folders()
        {
            var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "b4x_ctx_gen_" + System.Guid.NewGuid().ToString("N"));
            var ignored = new[] { "node_modules", "target", "build", "dist", ".venv", "__pycache__", ".next", "vendor" };
            try
            {
                foreach (var sub in ignored.Concat(new[] { "src", "bin" }))
                    System.IO.Directory.CreateDirectory(System.IO.Path.Combine(dir, sub));
                System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "src", "main.py"), "x = 1\n");
                System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "node_modules", "junk.ts"), "x\n");
                System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "target", "junk.rs"), "x\n");
                System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "__pycache__", "junk.py"), "x\n");
                System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "bin", "junk.bas"), "x\n");

                var files = ProjectScanner.ScanProject(dir);
                Assert.Single(files);
                Assert.Equal("main.py", files[0].Name);
            }
            finally
            {
                System.IO.Directory.Delete(dir, true);
            }
        }

        [Fact]
        public void ScanProject_picks_up_generic_files_with_correct_kind()
        {
            var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "b4x_ctx_gen2_" + System.Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(dir);
            try
            {
                System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "Main.bas"), "'x\n");
                System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "app.ts"), "x\n");
                System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "style.css"), "x\n");
                System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "note.txt"), "x\n");

                var files = ProjectScanner.ScanProject(dir);
                Assert.Equal(4, files.Count);
                Assert.All(files, f => Assert.True(f.IsGenericText || f.IsB4x));
                Assert.Equal(1, files.Count(f => f.Kind == "ts"));
                Assert.Equal(1, files.Count(f => f.Kind == "css"));
                Assert.Equal(1, files.Count(f => f.Kind == "txt"));
            }
            finally
            {
                System.IO.Directory.Delete(dir, true);
            }
        }
    }

    public class LangSupportTests
    {
        [Theory]
        [InlineData("py", "python", "py", "# ")]
        [InlineData("pyw", "python", "py", "# ")]
        [InlineData("pyi", "python", "py", "# ")]
        [InlineData("ts", "typescript", "ts", "// ")]
        [InlineData("mts", "typescript", "ts", "// ")]
        [InlineData("cts", "typescript", "ts", "// ")]
        [InlineData("tsx", "tsx", "tsx", "// ")]
        [InlineData("js", "javascript", "js", "// ")]
        [InlineData("mjs", "javascript", "js", "// ")]
        [InlineData("cjs", "javascript", "js", "// ")]
        [InlineData("jsx", "javascript", "jsx", "// ")]
        [InlineData("rs", "rust", "rs", "// ")]
        [InlineData("go", "go", "go", "// ")]
        [InlineData("c", "c", "c", "// ")]
        [InlineData("h", "c", "c", "// ")]
        [InlineData("cs", "csharp", "cs", "// ")]
        [InlineData("json", "json", "json", "/* ")]
        [InlineData("jsonc", "json", "json", "/* ")]
        [InlineData("css", "css", "css", "/* ")]
        [InlineData("scss", null, "scss", "/* ")]
        [InlineData("less", null, "less", "/* ")]
        [InlineData("html", "html", "html", "<!-- ")]
        [InlineData("htm", "html", "html", "<!-- ")]
        [InlineData("xaml", null, "xml", "//")]
        [InlineData("csproj", null, "xml", "//")]
        [InlineData("slnx", null, "xml", "//")]
        [InlineData("sln", null, "text", "//")]
        public void FromExtension_maps_grammar_fence_and_comment(string ext, string grammar, string fence, string comment)
        {
            var p = LangSupport.FromExtension(ext);
            Assert.Equal(grammar, p.GrammarId);
            Assert.Equal(fence, p.FenceTag);
            Assert.Equal(comment, p.CommentPrefix);
            Assert.Equal(fence, LangSupport.FenceTagFor(ext));
        }

        [Theory]
        [InlineData("PY")]
        [InlineData("Ts")]
        [InlineData("HTML")]
        public void FromExtension_is_case_insensitive(string ext)
        {
            Assert.NotNull(LangSupport.FromExtension(ext));
        }

        [Theory]
        [InlineData("txt")]
        [InlineData("xml")]
        [InlineData("zmk")]
        [InlineData("")]
        public void Unknown_extensions_get_structural_default_profile(string ext)
        {
            var p = LangSupport.FromExtension(ext);
            Assert.Null(p.GrammarId);
            Assert.False(p.IsRawOnly);
            Assert.Equal(StructuralFamily.Generic, p.Family);
        }

        [Theory]
        [InlineData("toml")]
        [InlineData("ini")]
        [InlineData("cfg")]
        [InlineData("conf")]
        [InlineData("env")]
        [InlineData("properties")]
        public void Config_extensions_map_to_config_family(string ext)
        {
            Assert.Equal(StructuralFamily.Config, LangSupport.FromExtension(ext).Family);
        }

        [Theory]
        [InlineData("md")]
        [InlineData("markdown")]
        public void Markdown_extensions_map_to_markdown_family(string ext)
        {
            Assert.Equal(StructuralFamily.Markdown, LangSupport.FromExtension(ext).Family);
        }

        [Theory]
        [InlineData("json")]
        [InlineData("jsonc")]
        public void Json_is_raw_only(string ext)
        {
            Assert.True(LangSupport.FromExtension(ext).IsRawOnly);
        }

        [Theory]
        [InlineData("xaml")]
        [InlineData("csproj")]
        [InlineData("sln")]
        [InlineData("slnx")]
        public void Xml_project_files_are_structural_not_raw(string ext)
        {
            var p = LangSupport.FromExtension(ext);
            Assert.False(p.IsRawOnly);
            Assert.Null(p.GrammarId);
            Assert.Equal(StructuralFamily.Generic, p.Family);
        }

        [Theory]
        [InlineData("css")]
        [InlineData("scss")]
        [InlineData("less")]
        [InlineData("html")]
        [InlineData("htm")]
        public void Style_markup_languages_are_header_only(string ext)
        {
            var p = LangSupport.FromExtension(ext);
            Assert.True(p.HeaderOnly);
            Assert.False(p.IsRawOnly);
        }

        [Theory]
        [InlineData("py")]
        [InlineData("ts")]
        [InlineData("rs")]
        [InlineData("cs")]
        [InlineData("go")]
        public void Code_languages_are_neither_header_only_nor_raw(string ext)
        {
            var p = LangSupport.FromExtension(ext);
            Assert.False(p.HeaderOnly);
            Assert.False(p.IsRawOnly);
        }

        [Fact]
        public void Python_profile_has_expected_node_kinds()
        {
            var p = LangSupport.FromExtension("py");
            Assert.Contains("function_definition", p.DeclKinds);
            Assert.Contains("class_definition", p.DeclKinds);
            Assert.Contains("import_statement", p.ImportKinds);
            Assert.Contains("import_from_statement", p.ImportKinds);
            Assert.Contains("class_definition", p.ContainerDeclKinds);
            Assert.Contains("block", p.BodyKinds);
            Assert.DoesNotContain("import_statement", p.DeclKinds);
        }

        [Fact]
        public void Csharp_profile_maps_grammar_and_node_kinds()
        {
            var p = LangSupport.FromExtension("cs");
            Assert.Equal("csharp", p.GrammarId);
            Assert.Contains("namespace_declaration", p.DeclKinds);
            Assert.Contains("class_declaration", p.DeclKinds);
            Assert.Contains("method_declaration", p.DeclKinds);
            Assert.Contains("using_directive", p.ImportKinds);
            Assert.Contains("namespace_declaration", p.ContainerDeclKinds);
        }

        [Fact]
        public void C_profile_treats_h_as_c()
        {
            var c = LangSupport.FromExtension("c");
            var h = LangSupport.FromExtension("h");
            Assert.Equal(c.GrammarId, h.GrammarId);
            Assert.Equal(c.DeclKinds, h.DeclKinds);
        }
    }
}