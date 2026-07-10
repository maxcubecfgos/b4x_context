using System;

namespace B4XContext.Models
{
    public enum FileMode
    {
        Skeleton,
        Full
    }

    public class ProjectFile
    {
        public string Path { get; set; }
        public string Name => System.IO.Path.GetFileName(Path);
        public string Directory => System.IO.Path.GetDirectoryName(Path) ?? "";
        public bool Included { get; set; } = true;
        public FileMode Mode { get; set; } = FileMode.Skeleton;
        public int EstimatedTokens { get; set; } = 0;
        public string Kind { get; set; } = "file";

        public ProjectFile(string path)
        {
            Path = path ?? throw new ArgumentNullException(nameof(path));
        }
    }
}
