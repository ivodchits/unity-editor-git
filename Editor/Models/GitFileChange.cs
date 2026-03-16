namespace GitEditor
{
    public enum FileStatus
    {
        Modified,
        Added,
        Deleted,
        Renamed,
        Copied,
        Untracked,
        Unmerged
    }

    public class GitFileChange
    {
        public string Path;
        public string OldPath;
        public FileStatus Status;
        public bool Staged;

        public string StatusChar
        {
            get
            {
                switch (Status)
                {
                    case FileStatus.Modified: return "M";
                    case FileStatus.Added: return "A";
                    case FileStatus.Deleted: return "D";
                    case FileStatus.Renamed: return "R";
                    case FileStatus.Copied: return "C";
                    case FileStatus.Untracked: return "?";
                    case FileStatus.Unmerged: return "U";
                    default: return " ";
                }
            }
        }
    }
}
