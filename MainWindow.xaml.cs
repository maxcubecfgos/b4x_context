using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Interop;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using B4XContext.Models;
using WF = System.Windows.Forms;
using SW = System.Windows;
// Placeholder: file verified up-to-date (no-op)
using MessageBox = System.Windows.MessageBox;
using B4XContext.Services;
using B4XContext.Engine;

namespace b4x_context
{
    public partial class MainWindow : Window
    {
        // P/Invoke for global hotkey
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int WM_HOTKEY = 0x0312;
        private const int HOTKEY_ID = 0x9000;

        private HwndSource? _hwndSource;
        private IntPtr _windowHandle = IntPtr.Zero;
        private uint _hotkeyModifiers = 0;
        private uint _hotkeyKey = 0;
        private string _settingsPath;
        private List<ProjectFile> _files = new List<ProjectFile>();
        private string _projectRoot;
        private string _projectFile;
        private string _lastCompileText;
        // Tracks the raw parsed result of the last build. Null = no build has been run yet.
        private Dictionary<string, object> _lastBuildResult;
        private string _activeFile;
        private int _activeLine = 1;
        private string _activeSubName;
        private string _activeCode;

        // Expose the observable-ish list to the UI binding
        public System.Collections.ObjectModel.ObservableCollection<ProjectFile> FilesCollection { get; } = new System.Collections.ObjectModel.ObservableCollection<ProjectFile>();

        public MainWindow()
        {
            InitializeComponent();

            // Setup settings path and load hotkey config
            _settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "B4XContext", "settings.json");
            LoadHotkeySettings();
        }

        // Low-level global Ctrl+C watcher implementation (see user-provided exact implementation)
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int VK_CONTROL = 0x11;
        private const int VK_C = 0x43;

        private IntPtr _hookId = IntPtr.Zero;
        private LowLevelKeyboardProc _proc;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private void HookKeyboard()
        {
            _proc = HookCallback;
            using var curProcess = System.Diagnostics.Process.GetCurrentProcess();
            using var curModule = curProcess.MainModule;
            _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(curModule.ModuleName), 0);
        }

        private void UnhookKeyboard()
        {
            if (_hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                if (vkCode == VK_C && (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0)
                {
                    OnGlobalCopyDetected();
                }
            }
            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        private void OnGlobalCopyDetected()
        {
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                try
                {
                    if (System.Windows.Clipboard.ContainsText())
                    {
                        string text = System.Windows.Clipboard.GetText();
                        // Guard: if the clipboard contains an exported bundle (our own output), skip auto-populating
                        if (!string.IsNullOrWhiteSpace(text) && text.TrimStart().StartsWith("# Context Bundle", StringComparison.OrdinalIgnoreCase))
                        {
                            return;
                        }
                        PreambleText.Text = text;
                    }
                }
                catch
                {
                    // clipboard can be momentarily locked by another process, just skip
                }
            };
            timer.Start();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var helper = new WindowInteropHelper(this);
            _windowHandle = helper.Handle;
            _hwndSource = HwndSource.FromHwnd(_windowHandle);
            if (_hwndSource != null)
            {
                _hwndSource.AddHook(WndProc);
                TryRegisterHotkey();
                // Install keyboard hook to observe Ctrl+C globally
                HookKeyboard();
            }
        }

        private void TryRegisterHotkey()
        {
            try
            {
                if (_windowHandle == IntPtr.Zero) return;
                if (_hotkeyKey == 0) ParseHotkey("Control,Shift", "P");
                var ok = RegisterHotKey(_windowHandle, HOTKEY_ID, _hotkeyModifiers, _hotkeyKey);
                if (!ok)
                {
                    // registration failed; do not throw — the key may be taken
                    // could log to disk or show non-blocking status in UI
                }
            }
            catch { }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                // Bring window to foreground reliably
                try
                {
                    if (this.WindowState == WindowState.Minimized)
                        this.WindowState = WindowState.Normal;

                    this.Show();
                    this.Activate();
                    this.Topmost = true;
                    this.Topmost = false;
                    this.Focus();
                }
                catch { }
                handled = true;
            }
            return IntPtr.Zero;
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            // Show settings dialog
            var currentMods = GetModifiersString();
            var currentKey = GetKeyString();
            var dlg = new HotkeySettingsWindow(currentMods, currentKey) { Owner = this };
            var prevMods = currentMods;
            var prevKey = currentKey;
            var prevModifiersVal = _hotkeyModifiers;
            var prevKeyVal = _hotkeyKey;

            var res = dlg.ShowDialog();
            if (res == true)
            {
                // Attempt to apply new hotkey
                var newMods = dlg.Modifiers;
                var newKey = dlg.KeyName;

                // Unregister previous hotkey
                try { if (_windowHandle != IntPtr.Zero) UnregisterHotKey(_windowHandle, HOTKEY_ID); } catch { }

                // Parse new
                ParseHotkey(newMods, newKey);

                // Try register
                var ok = false;
                try { ok = RegisterHotKey(_windowHandle, HOTKEY_ID, _hotkeyModifiers, _hotkeyKey); } catch { ok = false; }

                if (!ok)
                {
                    // restore previous and re-register
                    ParseHotkey(prevMods, prevKey);
                    try { RegisterHotKey(_windowHandle, HOTKEY_ID, prevModifiersVal, prevKeyVal); } catch { }

                    // show inline error in dialog — open it again with error; simpler: show MessageBox inline
                    MessageBox.Show(this, "This combo is already in use by another application — try a different one.", "Hotkey registration failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    // persist settings
                    try
                    {
                        var dir = Path.GetDirectoryName(_settingsPath);
                        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                        var obj = new { Modifiers = newMods, Key = newKey };
                        File.WriteAllText(_settingsPath, JsonSerializer.Serialize(obj));
                    }
                    catch { }
                }
            }
        }

        private string GetModifiersString()
        {
            var parts = new List<string>();
            if ((_hotkeyModifiers & 0x0002) != 0) parts.Add("Control");
            if ((_hotkeyModifiers & 0x0001) != 0) parts.Add("Alt");
            if ((_hotkeyModifiers & 0x0004) != 0) parts.Add("Shift");
            if ((_hotkeyModifiers & 0x0008) != 0) parts.Add("Win");
            return string.Join(",", parts);
        }

        private string GetKeyString()
        {
            try
            {
                var wkey = System.Windows.Input.KeyInterop.KeyFromVirtualKey((int)_hotkeyKey);
                return wkey.ToString();
            }
            catch { return "P"; }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                if (_windowHandle != IntPtr.Zero)
                {
                    UnregisterHotKey(_windowHandle, HOTKEY_ID);
                }
                if (_hwndSource != null)
                {
                    _hwndSource.RemoveHook(WndProc);
                }
                // Unhook low-level keyboard hook
                UnhookKeyboard();
            }
            catch { }
            base.OnClosing(e);
        }

        private void UpdateSummary()
        {
            int selected = _files.Count(f => f.Included);
            FilesSelectedText.Text = selected.ToString();
            // Update total estimated tokens
            UpdateEstimatedTokens();
        }

        private void LoadHotkeySettings()
        {
            try
            {
                var dir = Path.GetDirectoryName(_settingsPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                if (!File.Exists(_settingsPath))
                {
                    // default Ctrl+Shift+P
                    var def = new { Modifiers = "Control,Shift", Key = "P" };
                    File.WriteAllText(_settingsPath, JsonSerializer.Serialize(def));
                }

                var txt = File.ReadAllText(_settingsPath);
                using var doc = JsonDocument.Parse(txt);
                var root = doc.RootElement;
                var mods = root.GetProperty("Modifiers").GetString();
                var key = root.GetProperty("Key").GetString();
                ParseHotkey(mods, key);
            }
            catch
            {
                // ignore and keep defaults
                _hotkeyModifiers = 0;
                _hotkeyKey = (uint)System.Windows.Input.KeyInterop.VirtualKeyFromKey(System.Windows.Input.Key.P);
            }
        }

        private void ParseHotkey(string mods, string key)
        {
            uint m = 0;
            if (!string.IsNullOrEmpty(mods))
            {
                var parts = mods.Split(new[] { ',', '|' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
                foreach (var p in parts)
                {
                    if (p.Equals("Control", StringComparison.OrdinalIgnoreCase) || p.Equals("Ctrl", StringComparison.OrdinalIgnoreCase)) m |= 0x0002; // MOD_CONTROL
                    if (p.Equals("Alt", StringComparison.OrdinalIgnoreCase)) m |= 0x0001; // MOD_ALT
                    if (p.Equals("Shift", StringComparison.OrdinalIgnoreCase)) m |= 0x0004; // MOD_SHIFT
                    if (p.Equals("Win", StringComparison.OrdinalIgnoreCase) || p.Equals("Windows", StringComparison.OrdinalIgnoreCase)) m |= 0x0008; // MOD_WIN
                }
            }
            _hotkeyModifiers = m;
            if (!string.IsNullOrEmpty(key))
            {
                try
                {
                    var k = (System.Windows.Input.Key)Enum.Parse(typeof(System.Windows.Input.Key), key, true);
                    _hotkeyKey = (uint)System.Windows.Input.KeyInterop.VirtualKeyFromKey(k);
                }
                catch
                {
                    _hotkeyKey = (uint)System.Windows.Input.KeyInterop.VirtualKeyFromKey(System.Windows.Input.Key.P);
                }
            }
        }

        private void MainWindow_Activated(object? sender, EventArgs e)
        {
            // Disabled: PreambleText should only be updated via global Ctrl+C watcher or manual paste by user.
        }

        private int EstimateTokensForFile(ProjectFile f)
        {
            try
            {
                if (f == null || !System.IO.File.Exists(f.Path)) return 0;
                var txt = CodeUtils.ReadTextSafely(f.Path);
                if (f.Mode == B4XContext.Models.FileMode.Skeleton)
                {
                    // generate skeleton and estimate size
                    var (root, issues) = B4xParser.Parse(txt);
                    var nodes = B4xParser.FlattenSubsAndTypes(root);
                    var snodes = nodes.Select(n => new SkeletonGenerator.Node
                    {
                        StartLine = n.StartLine,
                        EndLine = n.EndLine,
                        Kind = n.Kind,
                        Name = n.Name,
                        LeadingComment = n.LeadingComment
                    }).ToList();
                    var skeleton = SkeletonGenerator.GenerateModuleSkeleton(txt, snodes, Enumerable.Empty<string>());
                    return Math.Max(0, skeleton.Length / 4);
                }
                else
                {
                    return Math.Max(0, txt.Length / 4);
                }
            }
            catch { return 0; }
        }

        private void UpdateEstimatedTokens()
        {
            try
            {
                int total = 0;
                // Active sub + task
                var activeLen = (PreambleText.Text ?? string.Empty).Length + (TaskText.Text ?? string.Empty).Length;
                total += Math.Max(0, activeLen / 4);

                foreach (var f in FilesCollection)
                {
                    if (!f.Included) { f.EstimatedTokens = 0; continue; }
                    f.EstimatedTokens = EstimateTokensForFile(f);
                    total += f.EstimatedTokens;
                }

                // Include file-tree tokens if enabled (use same BuildAsciiTree used for bundle)
                try
                {
                    if (FileTreeToggle.IsChecked == true && _files != null && _files.Any())
                    {
                        var tree = BundleBuilder.BuildAsciiTree(_files.Select(f => f.Path));
                        total += Math.Max(0, tree.Length / 4);
                    }
                }
                catch { }

                // Update UI
                var totalText = $"ESTIMATED TOKENS: ~{total}";
                // Ensure there's a place to show this: update FilesSelectedText's sibling area
                FilesSelectedText.Text = FilesSelectedText.Text; // keep existing
                // Add or update a badge/TextBlock named EstimatedTokensText if present, else create dynamic
                var existing = this.FindName("EstimatedTokensText") as TextBlock;
                if (existing != null)
                {
                    existing.Text = totalText;
                }
                else
                {
                    // try to find Pack Summary panel and add if possible (best-effort, not creating UI elements here)
                }
            }
            catch { }
        }

        private void ToggleMode_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.DataContext is ProjectFile pf)
            {
                pf.Mode = pf.Mode == B4XContext.Models.FileMode.Skeleton ? B4XContext.Models.FileMode.Full : B4XContext.Models.FileMode.Skeleton;
                // Refresh list view
                FilesListView.Items.Refresh();
                UpdateEstimatedTokens();
            }
        }

        private void FileInclude_Checked(object sender, RoutedEventArgs e)
        {
            UpdateSummary();
        }

        private void FileTreeToggle_Changed(object sender, RoutedEventArgs e)
        {
            UpdateEstimatedTokens();
        }

        private void GenerateButton_Click(object sender, RoutedEventArgs e)
        {
            // Determine whether to include compile errors in the bundle.
            // Only include when a build has actually run and produced errors (or a fatal runner error).
            string compileErrorsToInclude = null;
            if (_lastBuildResult != null)
            {
                if (_lastBuildResult.TryGetValue("fatal_error", out var fat))
                {
                    // Include a small build-failure block so the consumer knows the build failed to run
                    compileErrorsToInclude = $"## BUILD ERROR\n\n{fat}";
                }
                else
                {
                    var success = _lastBuildResult.TryGetValue("success", out var sucObj) && sucObj is bool sb && sb;
                    if (!success && !string.IsNullOrEmpty(_lastCompileText))
                    {
                        compileErrorsToInclude = _lastCompileText;
                    }
                }
            }

            var md = BundleBuilder.BuildMarkdown(PreambleText.Text, TaskText.Text, _files, includeFileTree: FileTreeToggle.IsChecked == true,
                activeCode: PreambleText.Text, activeFile: _activeFile, activeSub: _activeSubName, compileErrors: compileErrorsToInclude);
            BundleBuilder.CopyToClipboard(md);
            GenerateButton.Content = "Copied!";
            var t = new System.Timers.Timer(1200) { AutoReset = false };
            t.Elapsed += (s, ev) => Dispatcher.Invoke(() => GenerateButton.Content = "GENERATE PROMPT");
            t.Start();
        }

        private async void CompileButton_Click(object sender, RoutedEventArgs e)
        {
            // no-op edit to trigger regeneration
            CompileButton.IsEnabled = false;
            CompileStatusText.Text = "Compiling...";
            CompileStatusText.Foreground = (System.Windows.Media.Brush)FindResource("MutedTextBrush");

            // Proper flow: find project file then builder
            var projFile = ProjectScanner.FindProjectFile(_projectRoot);
            var builder = BuilderLocator.LocateBuilder(_projectRoot);
            if (string.IsNullOrEmpty(builder) || string.IsNullOrEmpty(projFile))
            {
                CompileStatusText.Text = "Builder not found";
                CompileStatusText.Foreground = System.Windows.Media.Brushes.OrangeRed;
                CompileButton.IsEnabled = true;
                return;
            }

            Dictionary<string, object> parsed = null;
            try
            {
                parsed = await System.Threading.Tasks.Task.Run(() => BuilderRunner.RunBuild(builder, projFile, 300));
            }
            catch (Exception ex)
            {
                CompileStatusText.Text = $"Build failed to start: {ex.Message}";
                CompileStatusText.Foreground = System.Windows.Media.Brushes.OrangeRed;
                CompileButton.IsEnabled = true;
                return;
            }

            if (parsed == null) parsed = null; // keep null to indicate no build run

            // Save last build result state explicitly
            _lastBuildResult = parsed;

            // Handle fatal errors from runner
            if (parsed != null && parsed.TryGetValue("fatal_error", out var fat))
            {
                CompileStatusText.Text = $"Build error: {fat}";
                CompileStatusText.Foreground = System.Windows.Media.Brushes.OrangeRed;
                _lastCompileText = string.Empty;
                CompileButton.IsEnabled = true;
                return;
            }

            // Format compile errors into markdown
            try
            {
                // Only format if a build actually ran
                if (parsed == null)
                {
                    _lastCompileText = string.Empty;
                }
                else
                {
                    _lastCompileText = BuildFormatter.Format(parsed);
                }
            }
            catch (Exception ex)
            {
                // Internal tool error while formatting build output — present clearly and include stack trace
                var sbErr = new System.Text.StringBuilder();
                sbErr.AppendLine("## ⚠ Internal tool error while parsing build output");
                sbErr.AppendLine();
                sbErr.AppendLine("An internal exception occurred while parsing the build output. This is a tool error, not a compiler error.");
                sbErr.AppendLine();
                sbErr.AppendLine($"Exception: {ex.GetType().FullName}: {ex.Message}");
                sbErr.AppendLine();
                sbErr.AppendLine("Stack trace:");
                sbErr.AppendLine("```");
                sbErr.AppendLine(ex.ToString());
                sbErr.AppendLine("```");
                _lastCompileText = sbErr.ToString();

                // Visual feedback
                CompileStatusText.Text = "Internal error parsing build output";
                CompileStatusText.Foreground = System.Windows.Media.Brushes.OrangeRed;
            }

            // Visual feedback based on success/errors
            bool success = parsed.ContainsKey("success") && parsed["success"] is bool b && b;
            var errorsList = parsed.ContainsKey("errors") && parsed["errors"] is System.Collections.IEnumerable ? parsed["errors"] as System.Collections.IEnumerable : null;
            int errCount = 0;
            if (errorsList != null)
            {
                foreach (var _ in errorsList) errCount++;
            }

            if (success && errCount == 0)
            {
                CompileStatusText.Text = "✓ Build OK";
                CompileStatusText.Foreground = System.Windows.Media.Brushes.LimeGreen;
            }
            else
            {
                CompileStatusText.Text = $"✗ {errCount} errors — attached to bundle";
                CompileStatusText.Foreground = System.Windows.Media.Brushes.OrangeRed;
            }

            CompileButton.IsEnabled = true;
        }

        // Open folder and load project
        private void ChangeButton_Click(object sender, RoutedEventArgs e)
        {
            using (var dlg = new WF.FolderBrowserDialog())
            {
                var dr = dlg.ShowDialog();
                if (!string.IsNullOrEmpty(dlg.SelectedPath))
                {
                    LoadProjectFolder(dlg.SelectedPath);
                }
            }
        }

        private void LoadProjectFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || !System.IO.Directory.Exists(folder))
                return;

            // Reset previous state (preserve any user-edited or clipboard-pasted ACTIVE SUB text)
            _projectRoot = folder;
            _activeFile = null;
            _activeSubName = null;
            _activeCode = null;

            _files = ProjectScanner.ScanProject(folder);
            FilesCollection.Clear();
            foreach (var pf in _files) FilesCollection.Add(pf);
            FilesListView.ItemsSource = FilesCollection;
            UpdateSummary();
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_projectRoot))
                return;
            LoadProjectFolder(_projectRoot);
        }

        private void AllButton_Click(object sender, RoutedEventArgs e)
        {
            if (FilesCollection == null || FilesCollection.Count == 0)
                return;
            bool anyUnselected = FilesCollection.Any(f => !f.Included);
            foreach (var f in FilesCollection)
                f.Included = anyUnselected;
            FilesListView.Items.Refresh();
            UpdateSummary();
        }
    }
}
