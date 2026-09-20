using System;
using B4XContext.Engine;
using Xunit;

namespace B4XContext.Tests
{
    public class MultiLangSkeletonizerTests
    {
        private const string Python = "import os\n" +
            "\n" +
            "def hello(name):\n" +
            "    print(name)\n" +
            "    return name\n" +
            "\n" +
            "class Calculator:\n" +
            "    def add(self, a, b):\n" +
            "        return a + b\n";

        [Fact]
        public void Python_keeps_imports_and_decl_headers()
        {
            var r = MultiLangSkeletonizer.Skeletonize(Python, "py");
            Assert.Contains("import os", r.Skeleton);
            Assert.Contains("def hello(name):", r.Skeleton);
            Assert.Contains("class Calculator:", r.Skeleton);
            Assert.Contains("def add(self, a, b):", r.Skeleton);
            Assert.Contains("# ... (", r.Skeleton);
            Assert.DoesNotContain("print(name)", r.Skeleton);
            Assert.DoesNotContain("return a + b", r.Skeleton);
            Assert.True(r.SkeletonLines < r.OriginalLines);
            Assert.True(r.SkeletonLines > 0);
        }

        private const string Csharp = "using System;\n" +
            "using System.Linq;\n" +
            "\n" +
            "namespace Demo\n" +
            "{\n" +
            "    public class Greeter\n" +
            "    {\n" +
            "        public void Say(string what)\n" +
            "        {\n" +
            "            Console.WriteLine(what);\n" +
            "        }\n" +
            "    }\n" +
            "}\n";

        [Fact]
        public void Csharp_keeps_usings_namespace_class_and_methods()
        {
            var r = MultiLangSkeletonizer.Skeletonize(Csharp, "cs");
            Assert.Contains("using System;", r.Skeleton);
            Assert.Contains("using System.Linq;", r.Skeleton);
            Assert.Contains("namespace Demo", r.Skeleton);
            Assert.Contains("public class Greeter", r.Skeleton);
            Assert.Contains("public void Say(string what)", r.Skeleton);
            Assert.Contains("// ... (", r.Skeleton);
            Assert.DoesNotContain("Console.WriteLine", r.Skeleton);
        }

        private const string Go = "package main\n" +
            "\n" +
            "import \"fmt\"\n" +
            "\n" +
            "func main() {\n" +
            "\tfmt.Println(1)\n" +
            "}\n" +
            "\n" +
            "type T struct {\n" +
            "\ta int\n" +
            "}\n";

        [Fact]
        public void Go_keeps_functions_and_types()
        {
            var r = MultiLangSkeletonizer.Skeletonize(Go, "go");
            Assert.Contains("package main", r.Skeleton);
            Assert.Contains("import \"fmt\"", r.Skeleton);
            Assert.Contains("func main()", r.Skeleton);
            Assert.Contains("type T struct", r.Skeleton);
            Assert.DoesNotContain("fmt.Println", r.Skeleton);
        }

        private const string Css = "@media (max-width: 600px) {\n" +
            "    .box {\n" +
            "        color: red;\n" +
            "        background: blue;\n" +
            "    }\n" +
            "}\n";

        [Fact]
        public void Css_header_only_keeps_selectors()
        {
            var r = MultiLangSkeletonizer.Skeletonize(Css, "css");
            Assert.Contains("@media (max-width: 600px) {", r.Skeleton);
            Assert.Contains(".box {", r.Skeleton);
            Assert.Contains("/* ... (", r.Skeleton);
            Assert.DoesNotContain("color: red", r.Skeleton);
        }

        [Fact]
        public void Json_is_returned_unchanged()
        {
            var src = "{ \"a\": 1, \"b\": [1, 2] }\n";
            var r = MultiLangSkeletonizer.Skeletonize(src, "json");
            Assert.Equal(src, r.Skeleton);
            Assert.Equal(r.OriginalLines, r.SkeletonLines);
        }

        [Fact]
        public void Leading_comment_above_function_is_kept()
        {
            var src = "def a():\n    pass\n\n\n# doc for b\ndef b():\n    pass\n";
            var r = MultiLangSkeletonizer.Skeletonize(src, "py");
            Assert.Contains("# doc for b", r.Skeleton);
            Assert.Contains("def b():", r.Skeleton);
        }

        [Fact]
        public void Unknown_extension_uses_structural_compression()
        {
            var src = "import a\n\nx = 1\n\nimport b\nfunction go() {\n  body\n}\nplain\n";
            var r = MultiLangSkeletonizer.Skeletonize(src, "txt");
            Assert.Contains("import a", r.Skeleton);
            Assert.Contains("import b", r.Skeleton);
            Assert.Contains("function go() {", r.Skeleton);
            Assert.DoesNotContain("x = 1", r.Skeleton);
            Assert.DoesNotContain("body", r.Skeleton);
            Assert.DoesNotContain("plain", r.Skeleton);
        }

        [Fact]
        public void Lock_extension_yields_empty_skeleton()
        {
            var r = MultiLangSkeletonizer.Skeletonize("some\nlines\n", "lock");
            Assert.Equal("", r.Skeleton);
        }

        [Fact]
        public void Markdown_structural_keeps_headings_fences_and_list_items()
        {
            var src = "# Title\nplain text\n```\ncode\n```\n- item\n";
            var r = MultiLangSkeletonizer.Skeletonize(src, "md");
            Assert.Contains("# Title", r.Skeleton);
            Assert.DoesNotContain("plain text", r.Skeleton);
            Assert.Contains("```", r.Skeleton);
            Assert.Contains("- item", r.Skeleton);
        }

        [Fact]
        public void Config_structural_keeps_assignment_lines()
        {
            var src = "# comment\n[section]\nkey = value\n\n[other]\n";
            var r = MultiLangSkeletonizer.Skeletonize(src, "toml");
            Assert.DoesNotContain("# comment", r.Skeleton);
            Assert.Contains("[section]", r.Skeleton);
            Assert.Contains("key = value", r.Skeleton);
            Assert.Contains("[other]", r.Skeleton);
        }

        [Fact]
        public void Structural_long_lines_are_truncated_to_200_chars()
        {
            var longLine = "class " + new string('a', 300) + " {";
            var r = MultiLangSkeletonizer.Skeletonize(longLine + "\n", "txt");
            Assert.Contains("...", r.Skeleton);
            Assert.True(r.Skeleton.Length < 220);
        }

        [Fact]
        public void Empty_source_returns_empty_skeleton()
        {
            var r = MultiLangSkeletonizer.Skeletonize("", "py");
            Assert.Equal("", r.Skeleton);
        }
    }
}