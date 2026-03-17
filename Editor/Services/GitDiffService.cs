using System.Collections.Generic;

namespace GitEditor
{
    public static class GitDiffService
    {
        public static List<GitFileDiff> GetStagedDiff()
        {
            var result = GitCommandRunner.Run("diff", "--cached");
            return ParseDiffOutput(result.Output);
        }

        public static List<GitFileDiff> GetUnstagedDiff()
        {
            var result = GitCommandRunner.Run("diff");
            return ParseDiffOutput(result.Output);
        }

        public static List<GitFileDiff> GetCommitDiff(string hash)
        {
            var result = GitCommandRunner.Run("diff", hash + "^.." + hash);
            return ParseDiffOutput(result.Output);
        }

        public static List<GitFileDiff> GetFileDiff(string path, bool staged, int contextLines = 3)
        {
            string contextArg = "-U" + contextLines;
            var result = staged
                ? GitCommandRunner.Run("diff", "--cached", contextArg, "--", "\"" + path + "\"")
                : GitCommandRunner.Run("diff", contextArg, "--", "\"" + path + "\"");
            return ParseDiffOutput(result.Output);
        }

        public static List<GitFileDiff> GetCommitFileDiff(string hash, string path, int contextLines = 3)
        {
            string contextArg = "-U" + contextLines;
            var result = GitCommandRunner.Run("diff", contextArg, hash + "^.." + hash, "--", "\"" + path + "\"");
            return ParseDiffOutput(result.Output);
        }

        static List<GitFileDiff> ParseDiffOutput(string output)
        {
            var diffs = new List<GitFileDiff>();
            if (string.IsNullOrEmpty(output)) return diffs;

            GitFileDiff current = null;
            GitDiffHunk currentHunk = null;

            foreach (string line in output.Split('\n'))
            {
                if (line.StartsWith("diff --git"))
                {
                    current = new GitFileDiff();
                    currentHunk = null;
                    diffs.Add(current);

                    // Extract b/ path
                    int bIdx = line.LastIndexOf(" b/");
                    if (bIdx >= 0)
                        current.FilePath = line.Substring(bIdx + 3);
                }
                else if (line.StartsWith("@@") && current != null)
                {
                    currentHunk = new GitDiffHunk { Header = line };
                    current.Hunks.Add(currentHunk);
                }
                else if (currentHunk != null)
                {
                    currentHunk.Lines.Add(line);
                }
            }

            return diffs;
        }
    }
}
