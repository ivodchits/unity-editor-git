using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace GitEditor
{
    public static class GitBranchService
    {
        public static List<GitBranch> GetBranches(bool includeRemote = true)
        {
            var branches = new List<GitBranch>();

            // Local branches
            var result = GitCommandRunner.Run("branch", "-vv");
            if (result.Success)
            {
                foreach (string line in result.Output.Split('\n'))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var branch = ParseLocalBranch(line);
                    if (branch != null) branches.Add(branch);
                }
            }

            // Remote branches
            if (includeRemote)
            {
                result = GitCommandRunner.Run("branch", "-r");
                if (result.Success)
                {
                    foreach (string line in result.Output.Split('\n'))
                    {
                        string trimmed = line.Trim();
                        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.Contains("->")) continue;

                        int slash = trimmed.IndexOf('/');
                        branches.Add(new GitBranch
                        {
                            Name = slash >= 0 ? trimmed.Substring(slash + 1) : trimmed,
                            Remote = slash >= 0 ? trimmed.Substring(0, slash) : "",
                            IsRemote = true
                        });
                    }
                }
            }

            return branches;
        }

        public static string GetCurrentBranch()
        {
            var result = GitCommandRunner.Run("rev-parse", "--abbrev-ref", "HEAD");
            return result.Success ? result.Output.Trim() : "";
        }

        public static GitResult Checkout(string branchName) => GitCommandRunner.Run("checkout", branchName);
        public static GitResult CreateAndCheckout(string branchName) => GitCommandRunner.Run("checkout", "-b", branchName);
        public static GitResult DeleteBranch(string branchName, bool force = false)
            => GitCommandRunner.Run("branch", force ? "-D" : "-d", branchName);
        public static GitResult Merge(string branchName) => GitCommandRunner.Run("merge", branchName);
        public static GitResult Rebase(string branchName) => GitCommandRunner.Run("rebase", branchName);

        static GitBranch ParseLocalBranch(string line)
        {
            bool isCurrent = line.StartsWith("*");
            string trimmed = line.TrimStart('*').Trim();

            // Format: "name hash [tracking] message" or "name hash message"
            string[] parts = trimmed.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 1) return null;

            var branch = new GitBranch
            {
                Name = parts[0],
                IsCurrent = isCurrent
            };

            // Parse tracking info [origin/main: ahead 1, behind 2]
            if (parts.Length > 1)
            {
                var match = Regex.Match(parts[1], @"\[([^\]]+)\]");
                if (match.Success)
                {
                    string tracking = match.Groups[1].Value;
                    int colonIdx = tracking.IndexOf(':');
                    branch.TrackingBranch = colonIdx >= 0 ? tracking.Substring(0, colonIdx).Trim() : tracking.Trim();

                    var aheadMatch = Regex.Match(tracking, @"ahead (\d+)");
                    var behindMatch = Regex.Match(tracking, @"behind (\d+)");
                    if (aheadMatch.Success) branch.Ahead = int.Parse(aheadMatch.Groups[1].Value);
                    if (behindMatch.Success) branch.Behind = int.Parse(behindMatch.Groups[1].Value);

                    int slashIdx = branch.TrackingBranch.IndexOf('/');
                    if (slashIdx >= 0) branch.Remote = branch.TrackingBranch.Substring(0, slashIdx);
                }
            }

            return branch;
        }
    }
}
