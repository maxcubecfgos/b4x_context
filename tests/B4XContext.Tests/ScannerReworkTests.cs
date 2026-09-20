using System.Collections.Generic;
using System.IO;
using System.Linq;
using B4XContext.Services;
using Xunit;

namespace B4XContext.Tests
{
    public class ScannerReworkTests
    {
        private static string MakeRoot()
        {
            var dir = Path.Combine(Path.GetTempPath(), "b4x_ctx_scan_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        [Fact]
        public void Scan_skips_hidden_directories_and_files()
        {
            var dir = MakeRoot();
            try
            {
                Directory.CreateDirectory(Path.Combine(dir, ".config"));
                File.WriteAllText(Path.Combine(dir, ".config", "secret.py"), "x=1\n");
                File.WriteAllText(Path.Combine(dir, ".env"), "{\"a\":1}\n");
                File.WriteAllText(Path.Combine(dir, "app.py"), "x=1\n");

                var files = ProjectScanner.ScanProject(dir);
                Assert.Single(files);
                Assert.Equal("app.py", files[0].Name);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Fact]
        public void Scan_respects_root_gitignore_for_files_and_dirs()
        {
            var dir = MakeRoot();
            try
            {
                File.WriteAllText(Path.Combine(dir, ".gitignore"), "*.json\ndist_new/\n");
                File.WriteAllText(Path.Combine(dir, "cfg.json"), "{}\n");
                File.WriteAllText(Path.Combine(dir, "app.py"), "x=1\n");
                Directory.CreateDirectory(Path.Combine(dir, "dist_new"));
                File.WriteAllText(Path.Combine(dir, "dist_new", "junk.py"), "x=1\n");

                var files = ProjectScanner.ScanProject(dir);
                Assert.Single(files);
                Assert.Equal("app.py", files[0].Name);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Fact]
        public void Scan_nested_gitignore_only_applies_to_its_subtree()
        {
            var dir = MakeRoot();
            try
            {
                Directory.CreateDirectory(Path.Combine(dir, "a"));
                Directory.CreateDirectory(Path.Combine(dir, "b"));
                File.WriteAllText(Path.Combine(dir, "a", ".gitignore"), "*.secret\n");
                File.WriteAllText(Path.Combine(dir, "a", "data.py"), "x=1\n");
                File.WriteAllText(Path.Combine(dir, "b", "keep.py"), "x=1\n");

                var files = ProjectScanner.ScanProject(dir);
                Assert.Equal(2, files.Count);
                Assert.Contains(files, f => f.Name == "keep.py");
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Fact]
        public void Scan_marks_nothing_included_by_default()
        {
            var dir = MakeRoot();
            try
            {
                File.WriteAllText(Path.Combine(dir, "__init__.py"), "");
                File.WriteAllText(Path.Combine(dir, "app.py"), "x=1\n");
                File.WriteAllText(Path.Combine(dir, "__main__.py"), "print(1)\n");

                var files = ProjectScanner.ScanProject(dir);
                Assert.Equal(3, files.Count);
                Assert.All(files, f => Assert.False(f.Included));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Fact]
        public void Scan_sets_relative_directory_and_orders_root_first()
        {
            var dir = MakeRoot();
            try
            {
                Directory.CreateDirectory(Path.Combine(dir, "models"));
                Directory.CreateDirectory(Path.Combine(dir, "views"));
                File.WriteAllText(Path.Combine(dir, "server.py"), "x=1\n");
                File.WriteAllText(Path.Combine(dir, "models", "user.py"), "x=1\n");
                File.WriteAllText(Path.Combine(dir, "views", "home.py"), "x=1\n");

                var files = ProjectScanner.ScanProject(dir);
                Assert.Equal("", files[0].RelativeDirectory);
                Assert.Equal("server.py", files[0].Name);
                Assert.Contains(files, f => f.Name == "user.py" && f.RelativeDirectory == "models");
                Assert.Contains(files, f => f.Name == "home.py" && f.RelativeDirectory == "views");
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Fact]
        public void Scan_prunes_ignored_dir_before_descending()
        {
            var dir = MakeRoot();
            try
            {
                Directory.CreateDirectory(Path.Combine(dir, "out"));
                Directory.CreateDirectory(Path.Combine(dir, "logs"));
                Directory.CreateDirectory(Path.Combine(dir, ".pytest_cache"));
                File.WriteAllText(Path.Combine(dir, "out", "x.py"), "x=1\n");
                File.WriteAllText(Path.Combine(dir, "logs", "x.py"), "x=1\n");
                File.WriteAllText(Path.Combine(dir, ".pytest_cache", "x.py"), "x=1\n");

                var files = ProjectScanner.ScanProject(dir);
                Assert.Empty(files);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Fact]
        public void Scan_loads_all_files_including_no_extension_markup_and_project_files()
        {
            var dir = MakeRoot();
            try
            {
                var names = new[] { "LICENSE", "TODO", "README.md", "App.xaml", "App.csproj", "App.slnx", "App.sln", "notes.txt" };
                foreach (var n in names)
                    File.WriteAllText(Path.Combine(dir, n), "content\n");

                var files = ProjectScanner.ScanProject(dir);
                Assert.Equal(8, files.Count);
                Assert.Equal(1, files.Count(f => f.Name == "LICENSE" && f.Kind == ""));
                Assert.Equal(1, files.Count(f => f.Name == "TODO" && f.Kind == ""));
                Assert.Equal(1, files.Count(f => f.Name == "README.md" && f.Kind == "md"));
                Assert.Contains(files, f => f.Name == "App.xaml");
                Assert.Contains(files, f => f.Name == "App.csproj");
                Assert.Contains(files, f => f.Name == "App.slnx");
                Assert.Contains(files, f => f.Name == "App.sln");
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Fact]
        public void Scan_ignores_binary_media_and_lock_suffixes()
        {
            var dir = MakeRoot();
            try
            {
                var names = new[] { "a.png", "b.jpg", "c.ico", "d.exe", "e.pdf", "f.zip", "g.mp4", "h.log", "i.lock", "j.db", "k.jpeg", "l.gif", "m.min.js", "n.cache" };
                foreach (var n in names)
                    File.WriteAllText(Path.Combine(dir, n), "content\n");
                File.WriteAllText(Path.Combine(dir, "keep.py"), "x=1\n");

                var files = ProjectScanner.ScanProject(dir);
                Assert.Single(files);
                Assert.Equal("keep.py", files[0].Name);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Fact]
        public void Scan_ignores_desktop_metadata_file_names()
        {
            var dir = MakeRoot();
            try
            {
                File.WriteAllText(Path.Combine(dir, "Thumbs.db"), "x");
                File.WriteAllText(Path.Combine(dir, "desktop.ini"), "x");
                File.WriteAllText(Path.Combine(dir, "app.py"), "x=1\n");

                var files = ProjectScanner.ScanProject(dir);
                Assert.Single(files);
                Assert.Equal("app.py", files[0].Name);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Fact]
        public void Scan_sets_size_on_files()
        {
            var dir = MakeRoot();
            try
            {
                File.WriteAllText(Path.Combine(dir, "app.py"), "x = 1\n");
                var files = ProjectScanner.ScanProject(dir);
                Assert.True(files[0].Size > 0);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }
    }
}