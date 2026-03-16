using System;
using System.Collections.Generic;

namespace GitEditor
{
    public static class GitLogService
    {
        const string Separator = "<<SEP>>";
        const string Format = "%H" + Separator + "%h" + Separator + "%an" + Separator + "%ai" + Separator + "%s" + Separator + "%D";

        public static List<GitCommit> GetLog(int count = 100, string pathFilter = null)
        {
            var args = new List<string> { "log", $"--pretty=format:{Format}", $"-n {count}" };
            if (!string.IsNullOrEmpty(pathFilter))
            {
                args.Add("--");
                args.Add("\"" + pathFilter + "\"");
            }

            var result = GitCommandRunner.Run(args.ToArray());
            var commits = new List<GitCommit>();
            if (!result.Success) return commits;

            foreach (string line in result.Output.Split('\n'))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                string[] parts = line.Split(new[] { Separator }, StringSplitOptions.None);
                if (parts.Length < 5) continue;

                var commit = new GitCommit
                {
                    Hash = parts[0].Trim(),
                    ShortHash = parts[1].Trim(),
                    Author = parts[2].Trim(),
                    Date = parts[3].Trim(),
                    Message = parts[4].Trim()
                };

                if (parts.Length > 5 && !string.IsNullOrWhiteSpace(parts[5]))
                {
                    foreach (string r in parts[5].Split(','))
                    {
                        string trimmed = r.Trim();
                        if (!string.IsNullOrEmpty(trimmed))
                            commit.Refs.Add(trimmed);
                    }
                }

                commits.Add(commit);
            }

            return commits;
        }

        public static List<string> GetCommitFiles(string hash)
        {
            var result = GitCommandRunner.Run("diff-tree", "--no-commit-id", "-r", "--name-status", hash);
            var files = new List<string>();
            if (!result.Success) return files;

            foreach (string line in result.Output.Split('\n'))
            {
                if (!string.IsNullOrWhiteSpace(line))
                    files.Add(line.Trim());
            }
            return files;
        }
    }
}
