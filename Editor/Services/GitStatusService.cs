using System.Collections.Generic;

namespace GitEditor
{
    public static class GitStatusService
    {
        public static List<GitFileChange> GetStatus()
        {
            var result = GitCommandRunner.Run("status", "--porcelain=v1");
            var changes = new List<GitFileChange>();
            if (!result.Success) return changes;

            foreach (string line in result.Output.Split('\n'))
            {
                if (line.Length < 4) continue;

                char indexStatus = line[0];
                char workStatus = line[1];
                string path = line.Substring(3).Trim().Trim('"');

                // Staged change
                if (indexStatus != ' ' && indexStatus != '?')
                {
                    changes.Add(new GitFileChange
                    {
                        Path = path,
                        Status = CharToStatus(indexStatus),
                        Staged = true
                    });
                }

                // Unstaged change
                if (workStatus != ' ')
                {
                    changes.Add(new GitFileChange
                    {
                        Path = path,
                        Status = workStatus == '?' ? FileStatus.Untracked : CharToStatus(workStatus),
                        Staged = false
                    });
                }
            }

            return changes;
        }

        public static GitResult Stage(string path) => GitCommandRunner.Run("add", "--", Quote(path));
        public static GitResult StageAll() => GitCommandRunner.Run("add", "-A");
        public static GitResult Unstage(string path) => GitCommandRunner.Run("reset", "HEAD", "--", Quote(path));
        public static GitResult UnstageAll() => GitCommandRunner.Run("reset", "HEAD");
        public static GitResult Revert(string path) => GitCommandRunner.Run("checkout", "--", Quote(path));
        public static GitResult RevertAll() => GitCommandRunner.Run("checkout", "--", ".");
        public static GitResult DiscardUntracked(string path) => GitCommandRunner.Run("clean", "-fd", "--", Quote(path));

        public static GitResult Commit(string message)
        {
            return GitCommandRunner.Run("commit", "-m", Quote(message));
        }

        static string Quote(string s) => "\"" + s.Replace("\"", "\\\"") + "\"";

        static FileStatus CharToStatus(char c)
        {
            switch (c)
            {
                case 'M': return FileStatus.Modified;
                case 'A': return FileStatus.Added;
                case 'D': return FileStatus.Deleted;
                case 'R': return FileStatus.Renamed;
                case 'C': return FileStatus.Copied;
                case 'U': return FileStatus.Unmerged;
                case '?': return FileStatus.Untracked;
                default: return FileStatus.Modified;
            }
        }
    }
}
