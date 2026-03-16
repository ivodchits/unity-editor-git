namespace GitEditor
{
    public static class GitRemoteService
    {
        public static GitAsyncOperation FetchAll() => GitCommandRunner.RunAsync("fetch", "--all", "--prune");
        public static GitAsyncOperation Fetch(string remote = "origin") => GitCommandRunner.RunAsync("fetch", remote);

        public static GitAsyncOperation Pull(bool rebase = false)
        {
            return rebase
                ? GitCommandRunner.RunAsync("pull", "--rebase")
                : GitCommandRunner.RunAsync("pull");
        }

        public static GitAsyncOperation Push()
        {
            string branch = GitBranchService.GetCurrentBranch();
            return GitCommandRunner.RunAsync("push", "-u", "origin", branch);
        }

        public static GitResult GetRemotes()
        {
            return GitCommandRunner.Run("remote", "-v");
        }
    }
}
