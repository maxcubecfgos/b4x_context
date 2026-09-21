using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using B4XContext.Engine;
using B4XContext.Models;
using B4XContext.Services;
using Xunit;
using FileMode = B4XContext.Models.FileMode;

namespace B4XContext.Tests
{
    /// <summary>Dart files expose the same granular "Custom" selection as B4X modules.</summary>
    public class DartItemExtractorTests
    {
        private const string Sample =
            "// Copyright 2014 The Flutter Authors. All rights reserved.\n" +   // 1
            "\n" +                                                              // 2
            "library;\n" +                                                      // 3
            "\n" +                                                              // 4
            "import 'package:flutter/material.dart';\n" +                       // 5
            "\n" +                                                              // 6
            "/// A demo widget.\n" +                                            // 7
            "class Demo extends StatefulWidget {\n" +                           // 8
            "  const Demo({super.key, required this.title});\n" +               // 9
            "\n" +                                                              // 10
            "  final String title;\n" +                                         // 11
            "\n" +                                                              // 12
            "  @override\n" +                                                   // 13
            "  State<Demo> createState() => _DemoState();\n" +                  // 14
            "}\n" +                                                             // 15
            "\n" +                                                              // 16
            "mixin Runner on Object {\n" +                                      // 17
            "  void run() {\n" +                                                // 18
            "    print('run');\n" +                                             // 19
            "  }\n" +                                                           // 20
            "}\n" +                                                             // 21
            "\n" +                                                              // 22
            "enum Mode {\n" +                                                   // 23
            "  light,\n" +                                                      // 24
            "  dark;\n" +                                                       // 25
            "\n" +                                                              // 26
            "  final bool isLight = false;\n" +                                 // 27
            "}\n" +                                                             // 28
            "\n" +                                                              // 29
            "typedef Callback = void Function(int value);\n" +                  // 30
            "\n" +                                                              // 31
            "final int topLevelCount = 3;\n" +                                  // 32
            "\n" +                                                              // 33
            "void main() {\n" +                                                 // 34
            "  print(topLevelCount);\n" +                                       // 35
            "}\n";                                                              // 36

        private static ModuleItem Find(List<ModuleItem> items, ModuleItemKind kind, string name, string container = null)
            => items.FirstOrDefault(i => i.Kind == kind && i.Name == name &&
                (container == null || i.Container == container));

        [Fact]
        public void Extracts_types_members_and_top_level_declarations()
        {
            var items = DartItemExtractor.ExtractItems(Sample);

            Assert.Equal(4, items.Count(i => i.Kind == ModuleItemKind.Type));       // Demo, Runner, Mode, Callback
            Assert.Equal(4, items.Count(i => i.Kind == ModuleItemKind.Sub));        // Demo(), createState, run, main
            Assert.Equal(3, items.Count(i => i.Kind == ModuleItemKind.Variable));   // title, isLight, topLevelCount
            Assert.Equal(11, items.Count);
            Assert.DoesNotContain(items, i => i.Name == "import" || i.Name == "library");

            var demo = Find(items, ModuleItemKind.Type, "Demo");
            Assert.NotNull(demo);
            Assert.Equal(7, demo.StartLine);   // includes the doc comment
            Assert.Equal(15, demo.EndLine);
            Assert.Equal("", demo.Container);
            Assert.Equal("class Demo extends StatefulWidget {", demo.Signature);

            var runner = Find(items, ModuleItemKind.Type, "Runner");
            Assert.Equal(17, runner.StartLine);
            Assert.Equal(21, runner.EndLine);

            var mode = Find(items, ModuleItemKind.Type, "Mode");
            Assert.Equal(23, mode.StartLine);
            Assert.Equal(28, mode.EndLine);

            var typedef = Find(items, ModuleItemKind.Type, "Callback");
            Assert.Equal(30, typedef.StartLine);
            Assert.Equal(30, typedef.EndLine);
            Assert.Equal("typedef Callback = void Function(int value);", typedef.Signature);

            var constructor = Find(items, ModuleItemKind.Sub, "Demo", "Demo");
            Assert.Equal(9, constructor.StartLine);
            Assert.Equal("const Demo({super.key, required this.title});", constructor.Signature);

            var createState = Find(items, ModuleItemKind.Sub, "createState", "Demo");
            Assert.Equal(13, createState.StartLine);   // the @override line is kept
            Assert.Equal(14, createState.EndLine);
            Assert.Equal("State<Demo> createState() => _DemoState();", createState.Signature);

            var run = Find(items, ModuleItemKind.Sub, "run", "Runner");
            Assert.Equal(18, run.StartLine);
            Assert.Equal(20, run.EndLine);

            var field = Find(items, ModuleItemKind.Variable, "title", "Demo");
            Assert.Equal(11, field.StartLine);

            var enumField = Find(items, ModuleItemKind.Variable, "isLight", "Mode");
            Assert.Equal(27, enumField.StartLine);

            var topVar = Find(items, ModuleItemKind.Variable, "topLevelCount");
            Assert.Equal(32, topVar.StartLine);
            Assert.Equal("", topVar.Container);

            var main = Find(items, ModuleItemKind.Sub, "main");
            Assert.Equal(34, main.StartLine);
            Assert.Equal(36, main.EndLine);
            Assert.Equal("void main() {", main.Signature);
        }

        [Fact]
        public void Enum_constants_are_not_separate_items()
        {
            var items = DartItemExtractor.ExtractItems(Sample);
            Assert.DoesNotContain(items, i => i.Name == "light");
            Assert.DoesNotContain(items, i => i.Name == "dark");
            Assert.Single(items, i => i.Kind == ModuleItemKind.Type && i.Name == "Mode");
            Assert.Single(items, i => i.Kind == ModuleItemKind.Variable && i.Name == "isLight");
        }

        [Fact]
        public void Signature_is_the_line_that_declares_the_name()
        {
            var src =
                "class A {\n" +
                "  @override\n" +
                "  @mustCallSuper\n" +
                "  void dispose() {\n" +
                "    super.dispose();\n" +
                "  }\n" +
                "}\n";

            var dispose = Find(DartItemExtractor.ExtractItems(src), ModuleItemKind.Sub, "dispose", "A");
            Assert.Equal("void dispose() {", dispose.Signature);
            Assert.Equal(2, dispose.StartLine);
            Assert.Equal(6, dispose.EndLine);
        }

        [Fact]
        public void Operator_equal_is_named_correctly()
        {
            var src =
                "class Key {\n" +
                "  final int payload;\n" +
                "  bool operator ==(Object other) {\n" +
                "    return other is Key && other.payload == payload;\n" +
                "  }\n" +
                "  Key operator [](int i) {\n" +
                "    return this;\n" +
                "  }\n" +
                "}\n";

            var items = DartItemExtractor.ExtractItems(src);
            var equals = Find(items, ModuleItemKind.Sub, "operator ==", "Key");
            Assert.NotNull(equals);
            Assert.Equal(3, equals.StartLine);
            Assert.Equal(5, equals.EndLine);

            var indexer = Find(items, ModuleItemKind.Sub, "operator []", "Key");
            Assert.NotNull(indexer);
            Assert.Equal(6, indexer.StartLine);
            Assert.Equal(8, indexer.EndLine);

            Assert.Single(items, i => i.Name == "payload");
        }

        [Fact]
        public void Factory_getter_setter_and_named_constructor_are_subs()
        {
            var src =
                "class Point {\n" +
                "  final int x;\n" +
                "  Point.from(int value) : x = value;\n" +
                "  factory Point.zero() => Point(0);\n" +
                "  int get doubled => x * 2;\n" +
                "  set doubled(int value) {\n" +
                "    print(value);\n" +
                "  }\n" +
                "}\n";

            var items = DartItemExtractor.ExtractItems(src);
            Assert.Equal("Point.from", Find(items, ModuleItemKind.Sub, "Point.from", "Point").Name);
            Assert.Equal("Point.zero", Find(items, ModuleItemKind.Sub, "Point.zero", "Point").Name);
            Assert.NotNull(Find(items, ModuleItemKind.Sub, "doubled", "Point"));
            Assert.Equal(1, items.Count(i => i.Kind == ModuleItemKind.Variable));   // x
            Assert.Equal(4, items.Count(i => i.Kind == ModuleItemKind.Sub));
        }

        [Fact]
        public void Item_range_matches_the_source_lines()
        {
            var lines = Sample.Split('\n');
            var items = DartItemExtractor.ExtractItems(Sample);

            foreach (var it in items)
            {
                Assert.InRange(it.StartLine, 1, lines.Length);
                Assert.InRange(it.EndLine, it.StartLine, lines.Length);
                var block = string.Join("\n", lines.Skip(it.StartLine - 1).Take(it.EndLine - it.StartLine + 1));
                Assert.Contains(it.Name.Replace("operator ", ""), block);
            }
        }

        [Fact]
        public void Generic_signatures_and_parameters_named_like_keywords_are_handled()
        {
            // `extension` is a legal parameter name; the function must not be mistaken for a container,
            // and generic parameter lists must not hide the declared name.
            var src =
                "File _pubspec(String extension) {\n" +
                "  return _root.childDirectory('packages');\n" +
                "}\n" +
                "\n" +
                "void _verify<T extends KeyEvent>(String? character, T event) {\n" +
                "  expect(event.character, character);\n" +
                "}\n" +
                "\n" +
                "class Box<T> {\n" +
                "  T map<R>(R Function(T) transform) => transform(value);\n" +
                "  final T value;\n" +
                "}\n";

            var items = DartItemExtractor.ExtractItems(src);

            Assert.Equal(5, items.Count);
            Assert.NotNull(Find(items, ModuleItemKind.Sub, "_pubspec"));
            Assert.Equal("File _pubspec(String extension) {", Find(items, ModuleItemKind.Sub, "_pubspec").Signature);
            Assert.NotNull(Find(items, ModuleItemKind.Sub, "_verify"));
            Assert.Null(Find(items, ModuleItemKind.Sub, "character"));
            Assert.Null(Find(items, ModuleItemKind.Sub, "childDirectory"));

            var box = Find(items, ModuleItemKind.Type, "Box");
            Assert.Equal(9, box.StartLine);
            Assert.NotNull(Find(items, ModuleItemKind.Sub, "map", "Box"));
            Assert.NotNull(Find(items, ModuleItemKind.Variable, "value", "Box"));
        }

        [Fact]
        public void Function_typed_fields_are_named_after_the_field()
        {
            var src =
                "class Fake {\n" +
                "  final Iterable<String> Function()? allCandidatesCallback;\n" +
                "  final void Function() onPressed;\n" +
                "}\n";

            var items = DartItemExtractor.ExtractItems(src);
            Assert.Equal(3, items.Count);
            Assert.NotNull(Find(items, ModuleItemKind.Variable, "allCandidatesCallback", "Fake"));
            Assert.NotNull(Find(items, ModuleItemKind.Variable, "onPressed", "Fake"));
        }

        [Fact]
        public void Crlf_sources_produce_the_same_items()
        {
            var items = DartItemExtractor.ExtractItems(Sample.Replace("\n", "\r\n"));
            Assert.Equal(11, items.Count);
            Assert.Equal(7, Find(items, ModuleItemKind.Type, "Demo").StartLine);
        }

        [Fact]
        public void Empty_source_has_no_items()
        {
            Assert.Empty(DartItemExtractor.ExtractItems(""));
            Assert.Empty(DartItemExtractor.ExtractItems("   \n\n"));
        }

        // ------------------------------------------------------------- Custom bundle

        [Fact]
        public void Granular_builder_emits_directives_and_only_the_selected_items()
        {
            var items = DartItemExtractor.ExtractItems(Sample);
            Find(items, ModuleItemKind.Sub, "main").IsSelected = true;

            var (code, _) = B4xGranularBuilder.BuildCustom(Sample, items, "demo.dart", int.MaxValue);

            Assert.Contains("library;", code);
            Assert.Contains("import 'package:flutter/material.dart';", code);
            Assert.Contains("void main() {", code);
            Assert.Contains("print(topLevelCount);", code);   // Custom keeps the full selected block
            Assert.Contains("11 items (1 selected)", code);
            Assert.DoesNotContain("class Demo", code);
            Assert.DoesNotContain("mixin Runner", code);
        }

        [Fact]
        public void Selecting_a_type_covers_its_members()
        {
            var items = DartItemExtractor.ExtractItems(Sample);
            Find(items, ModuleItemKind.Type, "Runner").IsSelected = true;
            Find(items, ModuleItemKind.Sub, "run", "Runner").IsSelected = true;

            var (code, _) = B4xGranularBuilder.BuildCustom(Sample, items, "demo.dart", int.MaxValue);

            Assert.Contains("mixin Runner on Object {", code);
            Assert.Contains("print('run');", code);
            // the member is inside the selected mixin range: emitted once, not twice
            Assert.Equal(1, CountOccurrences(code, "print('run');"));
            Assert.DoesNotContain("class Demo", code);
        }

        private static int CountOccurrences(string text, string needle)
        {
            int count = 0, idx = 0;
            while ((idx = text.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0)
            {
                count++;
                idx += needle.Length;
            }
            return count;
        }

        [Fact]
        public void BundleBuilder_renders_dart_custom_with_dart_fence()
        {
            var dir = Path.Combine(Path.GetTempPath(), "b4x_ctx_dart_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "demo.dart");
            File.WriteAllText(path, Sample);

            try
            {
                var pf = new ProjectFile(path) { Kind = "dart", Mode = FileMode.Custom, Included = true };
                var items = BundleBuilder.GetItems(pf, Sample);
                Assert.Equal(11, items.Count);
                Find(items, ModuleItemKind.Type, "Runner").IsSelected = true;

                var md = BundleBuilder.BuildMarkdown("", "", new[] { pf });

                Assert.Contains("### demo.dart   (Custom)", md);
                Assert.Contains("```dart", md);
                Assert.Contains("mixin Runner on Object {", md);   // directives + selected block
                Assert.Contains("library;", md);
                Assert.DoesNotContain("class Demo extends StatefulWidget", md);
                Assert.Contains("SUBS", string.Join(",", items.Select(i => i.Group).Distinct()));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Fact]
        public void Dart_files_support_granular_mode()
        {
            var dart = new ProjectFile(@"C:\proj\main.dart") { Kind = "dart" };
            Assert.False(dart.IsCodeFile);
            Assert.True(dart.IsGenericText);
            Assert.True(dart.SupportsGranular);

            var b4x = new ProjectFile(@"C:\proj\Main.bas") { Kind = "bas" };
            Assert.True(b4x.SupportsGranular);

            var py = new ProjectFile(@"C:\proj\app.py") { Kind = "py" };
            Assert.False(py.SupportsGranular);
        }
    }
}
