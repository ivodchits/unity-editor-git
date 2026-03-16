using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEditor;

namespace GitEditor
{
    public class GitAsyncOperation
    {
        public bool IsDone { get; internal set; }
        public GitResult Result { get; internal set; }
        internal Process Process;
        internal string StdOut;
        internal string StdErr;
    }

    public static class GitCommandRunner
    {
        public static event Action<string, GitResult> OnCommandCompleted;

        static string _repoRoot;

        public static string RepoRoot
        {
            get
            {
                if (string.IsNullOrEmpty(_repoRoot))
                    _repoRoot = DetectRepoRoot();
                return _repoRoot;
            }
            set => _repoRoot = value;
        }

        static string DetectRepoRoot()
        {
            string saved = GitSettings.RepoRoot;
            if (!string.IsNullOrEmpty(saved))
                return saved;

            var result = RunRaw("rev-parse", "--show-toplevel");
            if (result.Success)
            {
                string root = result.Output.Trim().Replace('/', '\\');
                GitSettings.RepoRoot = root;
                return root;
            }
            return "";
        }

        public static void ResetRepoRoot()
        {
            _repoRoot = null;
            GitSettings.RepoRoot = "";
        }

        public static GitResult Run(params string[] args)
        {
            var result = RunRaw(args);
            string cmd = "git " + string.Join(" ", args);
            OnCommandCompleted?.Invoke(cmd, result);
            return result;
        }

        static GitResult RunRaw(params string[] args)
        {
            var psi = CreateProcessStartInfo(args);
            try
            {
                using var process = Process.Start(psi);
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                return new GitResult
                {
                    ExitCode = process.ExitCode,
                    Output = output,
                    Error = error
                };
            }
            catch (Exception ex)
            {
                return new GitResult
                {
                    ExitCode = -1,
                    Output = "",
                    Error = ex.Message
                };
            }
        }

        public static GitAsyncOperation RunAsync(params string[] args)
        {
            var op = new GitAsyncOperation();
            var psi = CreateProcessStartInfo(args);
            string cmd = "git " + string.Join(" ", args);

            try
            {
                var process = Process.Start(psi);
                op.Process = process;

                process.OutputDataReceived += (s, e) =>
                {
                    if (e.Data != null) op.StdOut += e.Data + "\n";
                };
                process.ErrorDataReceived += (s, e) =>
                {
                    if (e.Data != null) op.StdErr += e.Data + "\n";
                };
                process.EnableRaisingEvents = true;
                process.Exited += (s, e) =>
                {
                    op.Result = new GitResult
                    {
                        ExitCode = process.ExitCode,
                        Output = op.StdOut ?? "",
                        Error = op.StdErr ?? ""
                    };
                    op.IsDone = true;
                    OnCommandCompleted?.Invoke(cmd, op.Result);
                    process.Dispose();
                };

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
            }
            catch (Exception ex)
            {
                op.Result = new GitResult { ExitCode = -1, Output = "", Error = ex.Message };
                op.IsDone = true;
                OnCommandCompleted?.Invoke(cmd, op.Result);
            }

            return op;
        }

        static ProcessStartInfo CreateProcessStartInfo(string[] args)
        {
            // Use the backing field directly — accessing the RepoRoot property here
            // would recurse infinitely because DetectRepoRoot() calls RunRaw() which
            // calls CreateProcessStartInfo() again before _repoRoot is set.
            string workDir = string.IsNullOrEmpty(_repoRoot)
                ? UnityEngine.Application.dataPath
                : _repoRoot;

            return new ProcessStartInfo
            {
                FileName = "git",
                Arguments = string.Join(" ", args),
                WorkingDirectory = workDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
        }
    }
}
