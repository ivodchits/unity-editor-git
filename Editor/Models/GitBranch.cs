namespace GitEditor
{
    public class GitBranch
    {
        public string Name;
        public string Remote;
        public string TrackingBranch;
        public bool IsCurrent;
        public bool IsRemote;
        public int Ahead;
        public int Behind;

        public string DisplayName => IsRemote ? $"{Remote}/{Name}" : Name;
    }
}
