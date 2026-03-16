using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace GitEditor
{
    public static class GitStashService
    {
        public static List<GitStash> GetStashes()
        {
            var result = GitCommandRunner.Run("stash", "list");
            var stashes = new List<GitStash>();
            if (!result.Success) return stashes;

            foreach (string line in result.Output.Split('\n'))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var match = Regex.Match(line, @"stash@\{(\d+)\}:\s*(.+)");
                if (match.Success)
                {
                    stashes.Add(new GitStash
                    {
                        Index = int.Parse(match.Groups[1].Value),
                        Message = match.Groups[2].Value.Trim()
                    });
                }
            }

            return stashes;
        }

        public static GitResult Push(string message = null)
        {
            if (string.IsNullOrEmpty(message))
                return GitCommandRunner.Run("stash", "push");
            return GitCommandRunner.Run("stash", "push", "-m", "\"" + message + "\"");
        }

        public static GitResult Apply(int index) => GitCommandRunner.Run("stash", "apply", $"stash@{{{index}}}");
        public static GitResult Pop(int index) => GitCommandRunner.Run("stash", "pop", $"stash@{{{index}}}");
        public static GitResult Drop(int index) => GitCommandRunner.Run("stash", "drop", $"stash@{{{index}}}");
    }
}
