using System.Collections.Generic;
using System.IO;
using System.Linq;
using B4XContext.Models;
using B4XContext.Services;
using Xunit;

namespace B4XContext.Tests
{
    public class AutoPreambleTests
    {
        private static ProjectFile F(string name, string relativeDir)
        {
            return new ProjectFile(Path.Combine(@"C:\x", relativeDir.Replace('/', '\\'), name))
            {
                RelativeDirectory = relativeDir,
                Kind = Path.GetExtension(name).TrimStart('.')
            };
        }

        [Fact]
        public void Generate_includes_package_json_context()
        {
            var files = new List<ProjectFile>
            {
                F("package.json", ""),
                F("main.js", "src")
            };

            var gen = AutoPreambleGenerator.Generate(files, path =>
                "{\"name\":\"demo-app\",\"description\":\"A demo\",\"dependencies\":{\"react\":\"1.0.0\",\"lodash\":\"1.0.0\"}}");

            Assert.Contains("Project: demo-app", gen);
            Assert.Contains("Description: A demo", gen);
            Assert.Contains("Key Stack (Node): react", gen);
            Assert.DoesNotContain("lodash", gen);
        }

        [Fact]
        public void Generate_includes_cargo_toml_context_when_no_package_json()
        {
            var files = new List<ProjectFile>
            {
                F("Cargo.toml", ""),
                F("main.rs", "src")
            };

            var gen = AutoPreambleGenerator.Generate(files, path =>
                "[package]\nname = \"rusty\"\ndescription = \"A rust tool\"\n");

            Assert.Contains("Project (Rust): rusty", gen);
            Assert.Contains("Description: A rust tool", gen);
            Assert.Contains("Stack Hint: Rust Project detected.", gen);
        }

        [Fact]
        public void Generate_readme_fallback_takes_first_15_lines_and_filters_badges()
        {
            var files = new List<ProjectFile> { F("README.md", "") };

            var gen = AutoPreambleGenerator.Generate(files, path =>
                "[![badge](x)]\n# demo\nLine two\nLine three\n");

            Assert.Contains("Project Overview:", gen);
            Assert.Contains("# demo", gen);
            Assert.DoesNotContain("badge", gen);
        }

        [Fact]
        public void Generate_readme_uses_ascii_block_when_present()
        {
            var files = new List<ProjectFile> { F("README.md", "") };

            var gen = AutoPreambleGenerator.Generate(files, path =>
                "# demo\nplain line\n\u250c\u2500\u2500\u252c\u2500\u2500\u2510\ntable\n\u2514\u2500\u2500\u2534\u2500\u2500\u2518\n");

            Assert.Contains("Project Context:", gen);
            Assert.Contains("\u250c", gen);
        }

        [Fact]
        public void Generate_includes_codebase_profile_stats()
        {
            var files = new List<ProjectFile>
            {
                F("a.py", ""), F("b.py", ""), F("c.py", ""), F("d.py", ""),
                F("e.ts", ""),
                F("img.png", ""), F("data.json", "")
            };

            var gen = AutoPreambleGenerator.Generate(files, path => "");

            Assert.Contains("Codebase Profile:", gen);
            Assert.Contains("py (80%)", gen);
            Assert.Contains("ts (20%)", gen);
            Assert.DoesNotContain("png", gen);
        }

        [Fact]
        public void Generate_returns_only_codebase_profile_when_no_manifests_or_readme()
        {
            var files = new List<ProjectFile> { F("main.py", ""), F("util.py", "") };

            var gen = AutoPreambleGenerator.Generate(files, path => "");

            Assert.Equal("Codebase Profile: py (100%)", gen);
        }

        [Fact]
        public void Generate_returns_empty_when_no_files()
        {
            Assert.Equal("", AutoPreambleGenerator.Generate(Enumerable.Empty<ProjectFile>(), path => ""));
        }
    }
}