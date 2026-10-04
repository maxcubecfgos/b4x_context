using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace B4XContext.Models
{
    public enum FileMode
    {
        Skeleton,
        Full,
        Custom
    }

    public class ProjectFile : INotifyPropertyChanged
    {

        public string Path { get; set; }
        public string Name => System.IO.Path.GetFileName(Path);
        public string Directory => System.IO.Path.GetDirectoryName(Path) ?? "";
        public string RelativeDirectory { get; set; } = "";
        public string Kind { get; set; } = "file";
        public long Size { get; set; }
        public int LineCount { get; set; }
        public ObservableCollection<ModuleItem> Items { get; } = new ObservableCollection<ModuleItem>();

        public bool IsCodeFile => Kind == "bas" || Kind == "b4a" || Kind == "b4j" || Kind == "b4i";

        public bool IsB4x => IsCodeFile;

        /// <summary>True when the file supports the granular per-item "Custom" mode (B4X modules and Dart).</summary>
        public bool SupportsGranular => IsCodeFile || Kind == "dart";

        public bool IsGenericText => !IsCodeFile && Kind != "bal" && Kind != "bjl" && Kind != "bil";

        private bool _included = false;
        private FileMode _mode = FileMode.Skeleton;
        private int _estimatedTokens = 0;
        private bool _isExpanded;
        private string? _summary;
        private int _summaryTokens;
        private bool _useSummary;

        /// <summary>Anchored summary produced by <see cref="Services.LocalCompactor"/> (opencode-style).</summary>
        public string? Summary
        {
            get => _summary;
            set { if (_summary == value) return; _summary = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasSummary)); }
        }

        public bool HasSummary => !string.IsNullOrEmpty(_summary);

        /// <summary>cl100k tokens of <see cref="Summary"/>; replaces the file tokens while <see cref="UseSummary"/> is on.</summary>
        public int SummaryTokens
        {
            get => _summaryTokens;
            set { if (_summaryTokens == value) return; _summaryTokens = value; OnPropertyChanged(); }
        }

        /// <summary>When true the bundle emits <see cref="Summary"/> instead of the file content.</summary>
        public bool UseSummary
        {
            get => _useSummary;
            set { if (_useSummary == value) return; _useSummary = value; OnPropertyChanged(); }
        }

        /// <summary>Tokens this file contributes to the bundle right now.</summary>
        public int EffectiveTokens => UseSummary && SummaryTokens > 0 ? SummaryTokens : EstimatedTokens;

        public bool Included
        {
            get => _included;
            set { if (_included == value) return; _included = value; OnPropertyChanged(); }
        }

        public FileMode Mode
        {
            get => _mode;
            set { if (_mode == value) return; _mode = value; OnPropertyChanged(); }
        }

        public int EstimatedTokens
        {
            get => _estimatedTokens;
            set { if (_estimatedTokens == value) return; _estimatedTokens = value; OnPropertyChanged(); }
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set { if (_isExpanded == value) return; _isExpanded = value; OnPropertyChanged(); }
        }

        public ProjectFile(string path)
        {
            Path = path ?? throw new ArgumentNullException(nameof(path));
            Items.CollectionChanged += OnItemsChanged;
        }

        public bool HasItems => Items.Count > 0;
        public IEnumerable<ModuleItem> ItemsSubs => Items.Where(i => i.Kind == ModuleItemKind.Sub);
        public IEnumerable<ModuleItem> ItemsVariables => Items.Where(i => i.Kind == ModuleItemKind.Variable);
        public IEnumerable<ModuleItem> ItemsTypes => Items.Where(i => i.Kind == ModuleItemKind.Type);
        public IEnumerable<ModuleItem> ItemsRegions => Items.Where(i => i.Kind == ModuleItemKind.Region);

        public bool HasSubs => Items.Any(i => i.Kind == ModuleItemKind.Sub);
        public bool HasVariables => Items.Any(i => i.Kind == ModuleItemKind.Variable);
        public bool HasTypes => Items.Any(i => i.Kind == ModuleItemKind.Type);
        public bool HasRegions => Items.Any(i => i.Kind == ModuleItemKind.Region);

        public void ResetCustom()
        {
            foreach (var item in Items) item.IsSelected = false;
            Mode = FileMode.Skeleton;
        }

        private void OnItemsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(HasItems));
            OnPropertyChanged(nameof(HasSubs));
            OnPropertyChanged(nameof(HasVariables));
            OnPropertyChanged(nameof(HasTypes));
            OnPropertyChanged(nameof(HasRegions));
            OnPropertyChanged(nameof(ItemsSubs));
            OnPropertyChanged(nameof(ItemsVariables));
            OnPropertyChanged(nameof(ItemsTypes));
            OnPropertyChanged(nameof(ItemsRegions));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}