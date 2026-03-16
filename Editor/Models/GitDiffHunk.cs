using System.Collections.Generic;

namespace GitEditor
{
    public class GitDiffHunk
    {
        public string Header;
        public List<string> Lines = new List<string>();
    }

    public class GitFileDiff
    {
        public string FilePath;
        public List<GitDiffHunk> Hunks = new List<GitDiffHunk>();
    }
}
