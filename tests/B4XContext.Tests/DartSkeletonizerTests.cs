using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using B4XContext.Engine;
using Xunit;

namespace B4XContext.Tests
{
    /// <summary>Dart has no tree-sitter grammar, so it is parsed by Engine/DartSkeletonizer.</summary>
    public class DartSkeletonizerTests
    {
        private static MultiLangSkeleton Sk(string source) => MultiLangSkeletonizer.Skeletonize(source, "dart");

        private static readonly string[] Lines =
        {
            "line1",
            "line2",
            "line3",
        };

        // ------------------------------------------------------------ directives

        [Fact]
        public void Directives_are_kept()
        {
            var src =
                "// Copyright 2014 The Flutter Authors. All rights reserved.\n" +
                "\n" +
                "/// @docImport 'scroll_delegate.dart';\n" +
                "library;\n" +
                "\n" +
                "import 'dart:async';\n" +
                "import 'package:flutter/foundation.dart';\n" +
                "import 'stub.dart' if (dart.library.io) 'io.dart';\n" +
                "export 'src/widgets.dart';\n" +
                "part 'widgets.g.dart';\n" +
                "\n" +
                "class A {}\n";

            var r = Sk(src);
            Assert.Contains("library;", r.Skeleton);
            Assert.Contains("import 'dart:async';", r.Skeleton);
            Assert.Contains("import 'package:flutter/foundation.dart';", r.Skeleton);
            Assert.Contains("import 'stub.dart' if (dart.library.io) 'io.dart';", r.Skeleton);
            Assert.Contains("export 'src/widgets.dart';", r.Skeleton);
            Assert.Contains("part 'widgets.g.dart';", r.Skeleton);
            Assert.Contains("// Copyright 2014 The Flutter Authors.", r.Skeleton);
        }

        [Fact]
        public void Part_of_file_keeps_directives_and_declarations()
        {
            var src =
                "part of 'widgets.dart';\n" +
                "\n" +
                "class _Inner {\n" +
                "  int get value => 1;\n" +
                "}\n";

            var r = Sk(src);
            Assert.Contains("part of 'widgets.dart';", r.Skeleton);
            Assert.Contains("class _Inner {", r.Skeleton);
            Assert.Contains("int get value => 1;", r.Skeleton);
        }

        // ------------------------------------------------------------ containers

        [Fact]
        public void Class_header_and_members_are_kept_with_bodies_omitted()
        {
            var src =
                "abstract class Animal<T> extends Base implements Comparable<Animal<T>> {\n" +
                "  final String name;\n" +
                "  static const int legs = 4;\n" +
                "\n" +
                "  Animal(this.name);\n" +
                "\n" +
                "  Animal.named({required this.name, int extra = 0});\n" +
                "\n" +
                "  @override\n" +
                "  void speak() {\n" +
                "    print('noise');\n" +
                "  }\n" +
                "\n" +
                "  String describe() => 'a ${name}';\n" +
                "\n" +
                "  void walk() {\n" +
                "    legs; \n" +
                "    speak();\n" +
                "  }\n" +
                "}\n";

            var r = Sk(src);
            Assert.Contains("abstract class Animal<T> extends Base implements Comparable<Animal<T>> {", r.Skeleton);
            Assert.Contains("final String name;", r.Skeleton);
            Assert.Contains("static const int legs = 4;", r.Skeleton);
            Assert.Contains("Animal(this.name);", r.Skeleton);
            Assert.Contains("Animal.named({required this.name, int extra = 0});", r.Skeleton);
            Assert.Contains("@override", r.Skeleton);
            Assert.Contains("void speak() {", r.Skeleton);
            Assert.Contains("String describe() => 'a ${name}';", r.Skeleton);
            Assert.Contains("void walk() {", r.Skeleton);
            Assert.Contains("// ... (", r.Skeleton);
            Assert.DoesNotContain("print('noise')", r.Skeleton);
            Assert.DoesNotContain("speak();", r.Skeleton);
        }

        [Fact]
        public void Mixins_extensions_extension_types_and_typedefs_are_kept()
        {
            var src =
                "mixin Runner on Base {\n" +
                "  void run() {\n" +
                "    print('run');\n" +
                "  }\n" +
                "}\n" +
                "\n" +
                "extension StringExtras on String {\n" +
                "  String get shout => toUpperCase();\n" +
                "}\n" +
                "\n" +
                "extension type Meters(double value) {\n" +
                "  double get km => value / 1000;\n" +
                "}\n" +
                "\n" +
                "typedef GestureDragUpdateCallback = void Function(\n" +
                "  DragUpdateDetails details,\n" +
                "  BuildContext context,\n" +
                ");\n" +
                "\n" +
                "sealed class Shape {}\n" +
                "final class Square extends Shape {}\n";

            var r = Sk(src);
            Assert.Contains("mixin Runner on Base {", r.Skeleton);
            Assert.Contains("void run() {", r.Skeleton);
            Assert.Contains("extension StringExtras on String {", r.Skeleton);
            Assert.Contains("String get shout => toUpperCase();", r.Skeleton);
            Assert.Contains("extension type Meters(double value) {", r.Skeleton);
            Assert.Contains("double get km => value / 1000;", r.Skeleton);
            Assert.Contains("typedef GestureDragUpdateCallback = void Function(", r.Skeleton);
            Assert.Contains("DragUpdateDetails details,", r.Skeleton);
            Assert.Contains(");", r.Skeleton);
            Assert.Contains("sealed class Shape {}", r.Skeleton);
            Assert.Contains("final class Square extends Shape {}", r.Skeleton);
            Assert.DoesNotContain("print('run')", r.Skeleton);
        }

        [Fact]
        public void Enum_constants_are_kept_verbatim_together_with_members()
        {
            var src =
                "enum Color {\n" +
                "  red,\n" +
                "  green,\n" +
                "  blue(3);\n" +
                "\n" +
                "  const Color([this.weight = 1]);\n" +
                "  final int weight;\n" +
                "\n" +
                "  bool get isBright => weight > 2;\n" +
                "}\n";

            var r = Sk(src);
            Assert.Contains("enum Color {", r.Skeleton);
            Assert.Contains("red,", r.Skeleton);
            Assert.Contains("green,", r.Skeleton);
            Assert.Contains("blue(3);", r.Skeleton);
            Assert.Contains("const Color([this.weight = 1]);", r.Skeleton);
            Assert.Contains("final int weight;", r.Skeleton);
            Assert.Contains("bool get isBright => weight > 2;", r.Skeleton);
        }

        [Fact]
        public void Single_line_enum_body_is_kept()
        {
            var r = Sk("enum Axis { horizontal, vertical }\n");
            Assert.Contains("enum Axis { horizontal, vertical }", r.Skeleton);
        }

        // ------------------------------------------------------------- comments

        [Fact]
        public void Doc_comments_are_kept_and_capped()
        {
            var src =
                "/// Summary line.\n" +
                "///\n" +
                "/// Detail 1.\n" +
                "/// Detail 2.\n" +
                "/// Detail 3.\n" +
                "/// Detail 4.\n" +
                "/// Detail 5.\n" +
                "/// Detail 6.\n" +
                "class A {\n" +
                "  void f() {}\n" +
                "}\n";

            var r = Sk(src);
            Assert.Contains("/// Summary line.", r.Skeleton);
            Assert.Contains("/// Detail 3.", r.Skeleton);
            Assert.DoesNotContain("/// Detail 6.", r.Skeleton);
            Assert.Contains("// ... (", r.Skeleton);
            Assert.Contains("class A {", r.Skeleton);
        }

        [Fact]
        public void Doc_comments_are_capped_per_declaration_kind()
        {
            var src =
                "/// Type 1.\n" +
                "/// Type 2.\n" +
                "/// Type 3.\n" +
                "/// Type 4.\n" +
                "/// Type 5.\n" +
                "/// Type 6.\n" +
                "/// Type 7.\n" +
                "class A {\n" +
                "  /// Member 1.\n" +
                "  /// Member 2.\n" +
                "  /// Member 3.\n" +
                "  /// Member 4.\n" +
                "  /// Member 5.\n" +
                "  void f() {\n" +
                "    print(1);\n" +
                "  }\n" +
                "}\n";

            var r = Sk(src);

            Assert.Contains("/// Type 6.", r.Skeleton);          // types keep 6 lines
            Assert.DoesNotContain("/// Type 7.", r.Skeleton);
            Assert.Contains("/// Member 3.", r.Skeleton);        // members keep 3 lines
            Assert.DoesNotContain("/// Member 4.", r.Skeleton);
            Assert.Contains("void f() {", r.Skeleton);
            Assert.DoesNotContain("print(1);", r.Skeleton);
            // type doc marker + member doc marker + f() body marker
            Assert.Equal(3, r.Skeleton.Split('\n').Count(l => l.StartsWith("// ...")));
        }

        [Fact]
        public void Top_level_comments_keep_the_header_budget()
        {
            var src =
                "// Header 1.\n" +
                "// Header 2.\n" +
                "// Header 3.\n" +
                "// Header 4.\n" +
                "// Header 5.\n" +
                "// Header 6.\n" +
                "// Header 7.\n" +
                "final int value = 1;\n";

            var r = Sk(src);
            Assert.Contains("// Header 6.", r.Skeleton);
            Assert.DoesNotContain("// Header 7.", r.Skeleton);
            Assert.Contains("final int value = 1;", r.Skeleton);
        }

        // ------------------------------------------------- implementation leak guard

        private static string Truncate(string line) =>
            line.Length <= 200 ? line : line.Substring(0, 200) + "...";

        /// <summary>
        /// The skeleton may only quote source lines that are NOT inside an implementation body
        /// (function/constructor/closure bodies). <see cref="DartSkeletonizer.ImplementationRanges"/> gives
        /// the exact ranges, so this catches any body line that leaks into the skeleton.
        /// </summary>
        private static void AssertNoImplementationLeak(string source)
        {
            var skeleton = Sk(source).Skeleton;
            var lines = source.Replace("\r\n", "\n").Split('\n');

            var inBody = new bool[lines.Length];
            foreach (var b in DartSkeletonizer.ImplementationRanges(source))
                for (int i = b.StartLine - 1; i <= b.EndLine - 1 && i < inBody.Length; i++)
                    if (i >= 0) inBody[i] = true;

            var bodyTexts = new HashSet<string>();
            var headerTexts = new HashSet<string>();
            for (int i = 0; i < lines.Length; i++)
            {
                var text = Truncate(lines[i]).Trim();
                if (text.Length == 0) continue;
                (inBody[i] ? bodyTexts : headerTexts).Add(text);
            }

            foreach (var line in skeleton.Split('\n'))
            {
                var t = line.Trim();
                if (t.Length == 0 || t.StartsWith("// ...")) continue;
                Assert.False(bodyTexts.Contains(t) && !headerTexts.Contains(t),
                    $"implementation line leaked into the skeleton: {t}");
            }
        }

        [Fact]
        public void Implementation_ranges_cover_bodies_but_not_type_bodies()
        {
            var src =
                "class A {\n" +                            // 1  class body is NOT implementation
                "  final int x = 1;\n" +                   // 2
                "  void f() {\n" +                         // 3  body 4..5
                "    var a = 1;\n" +                       // 4
                "    print(a);\n" +                        // 5
                "  }\n" +                                  // 6
                "}\n" +                                    // 7
                "\n" +                                     // 8
                "void g() {\n" +                           // 9  body 10..12 (closure inside is covered by it)
                "  run(() {\n" +                           // 10
                "    print(2);\n" +                        // 11
                "  });\n" +                                // 12
                "}\n" +                                    // 13
                "\n" +                                     // 14
                "late final String name = () {\n" +        // 15 closure body 16..16 (visited: field initializer)
                "  return 'x';\n" +                        // 16
                "}();\n";                                  // 17

            var ranges = DartSkeletonizer.ImplementationRanges(src);
            Assert.Contains(ranges, r => r.StartLine == 4 && r.EndLine == 5);    // f()
            Assert.Contains(ranges, r => r.StartLine == 10 && r.EndLine == 12);  // g()
            Assert.Contains(ranges, r => r.StartLine == 16 && r.EndLine == 16);  // closure in a field initializer
            Assert.DoesNotContain(ranges, r => r.StartLine <= 1 && r.EndLine >= 7);
        }

        [Fact]
        public void No_implementation_line_reaches_the_skeleton()
        {
            AssertNoImplementationLeak(
                "import 'package:flutter/widgets.dart';\n" +
                "\n" +
                "late final String name = () {\n" +
                "  if (flag) {\n" +
                "    return 'a';\n" +
                "  }\n" +
                "  return 'b';\n" +
                "}();\n" +
                "\n" +
                "class Widget extends StatefulWidget {\n" +
                "  const Widget({super.key})\n" +
                "      : assert(size >= 0),\n" +
                "        super();\n" +
                "\n" +
                "  final List<int> items = <int>[\n" +
                "    for (var i = 0; i < 3; i++) i,\n" +
                "  ];\n" +
                "\n" +
                "  Widget build(BuildContext context) => Builder(\n" +
                "    builder: (BuildContext context) {\n" +
                "      return const SizedBox();\n" +
                "    },\n" +
                "  );\n" +
                "\n" +
                "  void dispose() {\n" +
                "    super.dispose();\n" +
                "  }\n" +
                "}\n");
        }

        [Fact]
        public void No_implementation_line_reaches_the_skeleton_in_edge_shapes()
        {
            // multi-line strings, raw strings, nested closures and enum members
            AssertNoImplementationLeak(
                "enum Mode {\n" +
                "  light,\n" +
                "  dark;\n" +
                "\n" +
                "  String describe() {\n" +
                "    const String sql = '''\n" +
                "      select * from t\n" +
                "      where x = 1\n" +
                "    ''';\n" +
                "    return sql;\n" +
                "  }\n" +
                "}\n" +
                "\n" +
                "void main() {\n" +
                "  final Future<void> f = Future<void>(() async {\n" +
                "    await Future<void>.delayed(const Duration(seconds: 1));\n" +
                "  });\n" +
                "  f.then((_) {\n" +
                "    print(r'C:\\temp');\n" +
                "  });\n" +
                "}\n");
        }

        [Fact]
        public void Block_comment_before_class_is_kept()
        {
            var src =
                "/*\n" +
                " * Legacy widget.\n" +
                " */\n" +
                "class A {}\n";

            Assert.Contains("Legacy widget.", Sk(src).Skeleton);
        }

        // ------------------------------------------------------ lexer robustness

        [Fact]
        public void Braces_inside_strings_and_comments_do_not_confuse_the_parser()
        {
            var src =
                "const String kBraces = '}';\n" +
                "const String kOpen = \"{\";\n" +
                "const String kRaw = r'\\{';\n" +
                "const String kInterp = 'a ${map['}']} b';\n" +
                "// closing brace } in a comment\n" +
                "final Map<String, String> kMap = {\n" +
                "  'key': 'value',\n" +
                "};\n" +
                "\n" +
                "class A {\n" +
                "  void f() {\n" +
                "    print(kBraces);\n" +
                "  }\n" +
                "\n" +
                "  void g() {\n" +
                "    print(kOpen);\n" +
                "  }\n" +
                "}\n";

            var r = Sk(src);
            Assert.Contains("const String kBraces = '}';", r.Skeleton);
            Assert.Contains("const String kOpen = \"{\";", r.Skeleton);
            Assert.Contains("const String kRaw = r'\\{';", r.Skeleton);
            Assert.Contains("const String kInterp = 'a ${map['}']} b';", r.Skeleton);
            Assert.Contains("// closing brace } in a comment", r.Skeleton);
            Assert.Contains("final Map<String, String> kMap = {", r.Skeleton);
            Assert.Contains("class A {", r.Skeleton);
            Assert.Contains("void f() {", r.Skeleton);
            Assert.Contains("void g() {", r.Skeleton);
            Assert.DoesNotContain("print(", r.Skeleton);
        }

        [Fact]
        public void Multi_line_strings_do_not_leak_their_content()
        {
            var src =
                "const String doc = '''\n" +
                "class NotAClass {\n" +
                "  void nope() {}\n" +
                "}\n" +
                "''';\n" +
                "\n" +
                "class Real {\n" +
                "  void f() {}\n" +
                "}\n";

            var r = Sk(src);
            Assert.Contains("class Real {", r.Skeleton);
            Assert.Contains("void f() {", r.Skeleton);
            Assert.DoesNotContain("nope", r.Skeleton);
        }

        [Fact]
        public void Nested_block_comments_are_handled()
        {
            var src =
                "/* outer /* inner */ still comment */\n" +
                "class A {\n" +
                "  void f() {}\n" +
                "}\n";

            var r = Sk(src);
            Assert.Contains("class A {", r.Skeleton);
            Assert.Contains("void f() {", r.Skeleton);
        }

        // -------------------------------------------------------------- members

        [Fact]
        public void Closures_in_field_initializers_do_not_swallow_following_members()
        {
            var src =
                "class A {\n" +
                "  final int Function() cb = () {\n" +
                "    return 1;\n" +
                "  };\n" +
                "\n" +
                "  final int after = 2;\n" +
                "  int tailMethod() => 3;\n" +
                "}\n";

            var r = Sk(src);
            Assert.Contains("final int after = 2;", r.Skeleton);
            Assert.Contains("int tailMethod() => 3;", r.Skeleton);
            Assert.DoesNotContain("return 1;", r.Skeleton);
        }

        [Fact]
        public void Constructors_with_initializer_lists_are_kept()
        {
            var src =
                "class Point {\n" +
                "  final int x;\n" +
                "  Point(int x) : this.x = x, super() {\n" +
                "    print(x);\n" +
                "  }\n" +
                "\n" +
                "  Point.fromOther(Other o) : x = o.x;\n" +
                "\n" +
                "  factory Point.zero() => Point(0);\n" +
                "}\n";

            var r = Sk(src);
            Assert.Contains("Point(int x) : this.x = x, super() {", r.Skeleton);
            Assert.Contains("Point.fromOther(Other o) : x = o.x;", r.Skeleton);
            Assert.Contains("factory Point.zero() => Point(0);", r.Skeleton);
            Assert.DoesNotContain("print(x)", r.Skeleton);
        }

        [Fact]
        public void Long_parameter_list_keeps_first_and_last_line()
        {
            var sb = new StringBuilder("class W {\n  const W({\n");
            for (int i = 0; i < 20; i++)
                sb.Append("    this.param").Append(i).Append(",\n");
            sb.Append("  });\n\n  void f() {}\n}\n");

            var r = Sk(sb.ToString());
            Assert.Contains("const W({", r.Skeleton);
            Assert.Contains("});", r.Skeleton);
            Assert.Contains("// ... (", r.Skeleton);
            Assert.DoesNotContain("param5,", r.Skeleton);
            Assert.Contains("void f() {", r.Skeleton);
        }

        [Fact]
        public void Local_declarations_inside_bodies_are_dropped()
        {
            var src =
                "void main() {\n" +
                "  final localVariable = 1;\n" +
                "  void localHelper() {\n" +
                "    print(localVariable);\n" +
                "  }\n" +
                "  localHelper();\n" +
                "}\n" +
                "\n" +
                "int topLevel = 3;\n";

            var r = Sk(src);
            Assert.Contains("void main() {", r.Skeleton);
            Assert.Contains("int topLevel = 3;", r.Skeleton);
            Assert.DoesNotContain("localVariable", r.Skeleton);
            Assert.DoesNotContain("localHelper", r.Skeleton);
        }

        [Fact]
        public void Getter_setter_operator_and_static_members_are_kept()
        {
            var src =
                "class C {\n" +
                "  static final List<int> _items = <int>[];\n" +
                "\n" +
                "  int get length => _items.length;\n" +
                "  set length(int value) {\n" +
                "    _items.length = value;\n" +
                "  }\n" +
                "\n" +
                "  C operator +(C other) => C();\n" +
                "  late final String name;\n" +
                "}\n";

            var r = Sk(src);
            Assert.Contains("static final List<int> _items = <int>[];", r.Skeleton);
            Assert.Contains("int get length => _items.length;", r.Skeleton);
            Assert.Contains("set length(int value) {", r.Skeleton);
            Assert.Contains("C operator +(C other) => C();", r.Skeleton);
            Assert.Contains("late final String name;", r.Skeleton);
            Assert.DoesNotContain("_items.length = value;", r.Skeleton);
        }

        [Fact]
        public void Statements_spanning_a_few_lines_are_kept_whole()
        {
            var src =
                "const List<String> kNames = <String>[\n" +
                "  'a',\n" +
                "  'b',\n" +
                "];\n";

            var r = Sk(src);
            Assert.Contains("const List<String> kNames = <String>[", r.Skeleton);
            Assert.Contains("'a',", r.Skeleton);
            Assert.Contains("];", r.Skeleton);
        }

        [Fact]
        public void Huge_statement_is_trimmed_to_first_and_last_line()
        {
            var sb = new StringBuilder("const List<String> kNames = <String>[\n");
            for (int i = 0; i < 60; i++) sb.Append("  'name").Append(i).Append("',\n");
            sb.Append("];\n");

            var r = Sk(sb.ToString());
            Assert.Contains("const List<String> kNames = <String>[", r.Skeleton);
            Assert.Contains("];", r.Skeleton);
            Assert.Contains("// ... (", r.Skeleton);
            Assert.DoesNotContain("'name30'", r.Skeleton);
        }

        [Fact]
        public void Nested_literals_in_fields_do_not_start_a_body()
        {
            var src =
                "class A {\n" +
                "  static const Map<String, List<int>> m = <String, List<int>>{'a': [1, 2]};\n" +
                "  static const Set<int> s = <int>{1, 2};\n" +
                "  void f() {}\n" +
                "}\n";

            var r = Sk(src);
            Assert.Contains("static const Map<String, List<int>> m = <String, List<int>>{'a': [1, 2]};", r.Skeleton);
            Assert.Contains("static const Set<int> s = <int>{1, 2};", r.Skeleton);
            Assert.Contains("void f() {", r.Skeleton);
        }

        [Fact]
        public void Omitted_line_count_matches_the_body()
        {
            var src =
                "void f() {\n" +   // line 0
                "  a();\n" +        // line 1
                "  b();\n" +        // line 2
                "}\n";              // line 3

            var r = Sk(src);
            Assert.Contains("void f() {", r.Skeleton);
            Assert.Contains("// ... (2 lines omitted) ...", r.Skeleton);
        }

        [Fact]
        public void Annotations_on_their_own_line_stay_with_the_member()
        {
            var src =
                "class A {\n" +
                "  @override\n" +
                "  @mustCallSuper\n" +
                "  void dispose() {\n" +
                "    super.dispose();\n" +
                "  }\n" +
                "}\n";

            var r = Sk(src);
            Assert.Contains("@override", r.Skeleton);
            Assert.Contains("@mustCallSuper", r.Skeleton);
            Assert.Contains("void dispose() {", r.Skeleton);
            Assert.DoesNotContain("super.dispose();", r.Skeleton);
        }

        // ----------------------------------------------------------------- caps

        [Fact]
        public void Skeleton_is_capped_and_marked_as_truncated()
        {
            var sb = new StringBuilder("class Big {\n");
            for (int i = 0; i < 400; i++)
                sb.Append("  void method").Append(i).Append("() {\n    print(").Append(i).Append(");\n  }\n");
            sb.Append("}\n");

            var r = Sk(sb.ToString());
            Assert.Contains("// ... (skeleton truncated) ...", r.Skeleton);
            Assert.True(r.Skeleton.Split('\n').Length <= 201, "skeleton must stay within the line cap");
            Assert.True(r.SkeletonLines < r.OriginalLines);
        }

        [Fact]
        public void Empty_and_whitespace_sources_produce_empty_skeletons()
        {
            Assert.Equal("", Sk("").Skeleton);
            Assert.Equal("", Sk("   \n\n").Skeleton);
        }

        [Fact]
        public void Crlf_sources_do_not_leak_carriage_returns()
        {
            var r = Sk("class A {\r\n  void f() {\r\n    print(1);\r\n  }\r\n}\r\n");
            Assert.Contains("class A {", r.Skeleton);
            Assert.Contains("void f() {", r.Skeleton);
            Assert.DoesNotContain("\r", r.Skeleton);
        }

        [Fact]
        public void Unterminated_input_does_not_throw()
        {
            var src = "class A {\n  void f() {\n    if (x) {\n";
            var r = Sk(src);
            Assert.Contains("class A {", r.Skeleton);
            Assert.Contains("void f() {", r.Skeleton);
        }

        [Fact]
        public void Skeleton_is_shorter_than_a_realistic_widget_file()
        {
            var src =
                "import 'package:flutter/widgets.dart';\n" +
                "\n" +
                "/// A widget that keeps its subtree alive.\n" +
                "class KeepAlive extends StatefulWidget {\n" +
                "  const KeepAlive({super.key, required this.child});\n" +
                "\n" +
                "  final Widget child;\n" +
                "\n" +
                "  @override\n" +
                "  State<KeepAlive> createState() => _KeepAliveState();\n" +
                "}\n" +
                "\n" +
                "class _KeepAliveState extends State<KeepAlive> {\n" +
                "  @override\n" +
                "  Widget build(BuildContext context) {\n" +
                "    return NotificationListener<KeepAliveNotification>(\n" +
                "      onNotification: (KeepAliveNotification notification) {\n" +
                "        return true;\n" +
                "      },\n" +
                "      child: widget.child,\n" +
                "    );\n" +
                "  }\n" +
                "}\n";

            var r = Sk(src);
            Assert.Contains("import 'package:flutter/widgets.dart';", r.Skeleton);
            Assert.Contains("class KeepAlive extends StatefulWidget {", r.Skeleton);
            Assert.Contains("const KeepAlive({super.key, required this.child});", r.Skeleton);
            Assert.Contains("State<KeepAlive> createState() => _KeepAliveState();", r.Skeleton);
            Assert.Contains("class _KeepAliveState extends State<KeepAlive> {", r.Skeleton);
            Assert.Contains("Widget build(BuildContext context) {", r.Skeleton);
            Assert.DoesNotContain("NotificationListener<KeepAliveNotification>(", r.Skeleton);
            Assert.True(r.SkeletonLines < r.OriginalLines, "widget body should compress");
        }

        [Fact]
        public void Profile_selects_the_dart_skeletonizer()
        {
            var p = LangSupport.FromExtension("dart");
            Assert.Equal(SkeletonKind.Dart, p.Kind);
            Assert.Equal("dart", p.FenceTag);
        }
    }
}
