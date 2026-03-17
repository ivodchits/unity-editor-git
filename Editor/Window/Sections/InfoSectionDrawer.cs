using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEditorInternal;

namespace GitEditor
{
    public class InfoSectionDrawer
    {
        GitCommit _commit;
        List<string> _changedFiles = new List<string>();
        List<GitFileDiff> _allDiffs = new List<GitFileDiff>();
        int _selectedFileIndex = -1;
        Vector2 _filesScrollPos;
        Vector2 _diffScrollPos;

        GitFileChange _workingChange;
        List<GitFileDiff> _workingDiffs = new List<GitFileDiff>();

        public void ShowCommit(GitCommit commit)
        {
            _commit = commit;
            _workingChange = null;
            _changedFiles = commit != null ? GitLogService.GetCommitFiles(commit.Hash) : new List<string>();
            _allDiffs = commit != null ? GitDiffService.GetCommitDiff(commit.Hash) : new List<GitFileDiff>();
            _selectedFileIndex = -1;
        }

        public void ShowWorkingChange(GitFileChange change)
        {
            _workingChange = change;
            _commit = null;
            if (change != null)
                _workingDiffs = GitDiffService.GetFileDiff(change.Path, change.Staged);
            else
                _workingDiffs.Clear();
        }

        public void Draw()
        {
            if (_workingChange != null)
                DrawWorkingDiff();
            else if (_commit != null)
                DrawCommitInfo();
            else
                EditorGUILayout.LabelField("Select a commit or file to view details.", EditorStyles.centeredGreyMiniLabel);
        }

        void DrawCommitInfo()
        {
            EditorGUILayout.LabelField("Commit Info", GitStyles.Header);
            EditorGUILayout.SelectableLabel($"Hash: {_commit.Hash}", GUILayout.Height(18));
            EditorGUILayout.LabelField("Author", _commit.Author);
            EditorGUILayout.LabelField("Date", _commit.Date);
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(_commit.Message, EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space(8);

            EditorGUILayout.LabelField($"Changed Files ({_changedFiles.Count})", GitStyles.Header);
            _filesScrollPos = EditorGUILayout.BeginScrollView(_filesScrollPos, GUILayout.MaxHeight(120));

            for (int i = 0; i < _changedFiles.Count; i++)
            {
                bool isSel = i == _selectedFileIndex;
                string entry = _changedFiles[i];
                char status = entry.Length > 0 ? entry[0] : ' ';
                string filePath = entry.Length > 2 ? entry.Substring(2).Trim() : entry;

                var rowRect = EditorGUILayout.BeginHorizontal(isSel ? GitStyles.SelectedRow : GUIStyle.none);

                // Right-click before any Button consumes the event
                if (Event.current.type == EventType.MouseDown && Event.current.button == 1
                    && rowRect.Contains(Event.current.mousePosition))
                {
                    ShowFileMenu(filePath);
                    Event.current.Use();
                }

                var color = GUI.color;
                GUI.color = status == 'A' ? GitStyles.StagedColor
                    : status == 'D' ? new Color(0.9f, 0.4f, 0.4f)
                    : GitStyles.UnstagedColor;
                GUILayout.Label(status.ToString(), GUILayout.Width(16));
                GUI.color = color;

                if (GUILayout.Button(filePath, EditorStyles.label))
                {
                    _selectedFileIndex = i;
                    _diffScrollPos = Vector2.zero;
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("…", EditorStyles.miniButton, GUILayout.Width(22)))
                    ShowFileMenu(filePath);

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.Space(8);

            // Show diff for selected file, or all diffs if none selected
            var diffsToShow = GetDiffsForSelected();
            DrawDiffView(diffsToShow);
        }

        List<GitFileDiff> GetDiffsForSelected()
        {
            if (_selectedFileIndex < 0 || _selectedFileIndex >= _changedFiles.Count)
                return _allDiffs;

            string entry = _changedFiles[_selectedFileIndex];
            string filePath = (entry.Length > 2 ? entry.Substring(2).Trim() : entry).Replace('\\', '/');

            var filtered = _allDiffs
                .Where(d => d.FilePath != null && d.FilePath.Replace('\\', '/').EndsWith(filePath))
                .ToList();

            return filtered.Count > 0 ? filtered : _allDiffs;
        }

        void DrawWorkingDiff()
        {
            EditorGUILayout.LabelField(
                $"{(_workingChange.Staged ? "Staged" : "Unstaged")} — {_workingChange.Path}",
                GitStyles.Header);
            DrawDiffView(_workingDiffs);
        }

        void DrawDiffView(List<GitFileDiff> diffs)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Diff", GitStyles.Header);
            GUILayout.FlexibleSpace();

            string diffFilePath = GetCurrentDiffFilePath(diffs);
            GUI.enabled = !string.IsNullOrEmpty(diffFilePath);
            if (GUILayout.Button("Diff", EditorStyles.miniButton, GUILayout.Width(55)))
                HandleDiffButton(diffFilePath, diffs);
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            _diffScrollPos = EditorGUILayout.BeginScrollView(_diffScrollPos);

            foreach (var fileDiff in diffs)
            {
                if (!string.IsNullOrEmpty(fileDiff.FilePath))
                    EditorGUILayout.LabelField(fileDiff.FilePath, EditorStyles.boldLabel);

                foreach (var hunk in fileDiff.Hunks)
                {
                    GUILayout.Label(hunk.Header, GitStyles.DiffHunkHeader);
                    foreach (string line in hunk.Lines)
                    {
                        GUIStyle style = line.StartsWith("+") ? GitStyles.DiffAdd
                            : line.StartsWith("-") ? GitStyles.DiffRemove
                            : GitStyles.MonoLabel;
                        GUILayout.Label(line, style);
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }

        static readonly HashSet<string> UnitySerializedExtensions = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
        {
            ".prefab", ".unity", ".asset", ".mat", ".controller",
            ".overrideController", ".lighting", ".physicMaterial",
            ".physicsMaterial", ".flare", ".fontsettings", ".guiskin",
            ".mask", ".mixer", ".renderTexture", ".shadervariants",
            ".spriteatlas", ".terrainlayer", ".brush"
        };

        static bool IsUnitySerializedFile(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string ext = Path.GetExtension(path);
            return UnitySerializedExtensions.Contains(ext);
        }

        string GetCurrentDiffFilePath(List<GitFileDiff> diffs)
        {
            if (_workingChange != null)
                return _workingChange.Path;

            if (_selectedFileIndex >= 0 && _selectedFileIndex < _changedFiles.Count)
            {
                string entry = _changedFiles[_selectedFileIndex];
                return entry.Length > 2 ? entry.Substring(2).Trim() : entry;
            }

            // Single file in the diff list
            if (diffs.Count == 1 && !string.IsNullOrEmpty(diffs[0].FilePath))
                return diffs[0].FilePath;

            return null;
        }

        void HandleDiffButton(string filePath, List<GitFileDiff> diffs)
        {
            if (string.IsNullOrEmpty(filePath)) return;

            if (IsUnitySerializedFile(filePath))
            {
                AssetDiffPopup.Show(
                    filePath,
                    _workingChange?.Staged ?? false,
                    _commit?.Hash);
            }
            else
            {
                OpenInIDE(filePath);
            }
        }

        static void OpenInIDE(string filePath)
        {
            string fullPath = Path.Combine(GitCommandRunner.RepoRoot, filePath);
            if (File.Exists(fullPath))
                InternalEditorUtility.OpenFileAtLineExternal(fullPath, 1);
        }

        void ShowFileMenu(string filePath)
        {
            var menu = new GenericMenu();
            string fullPath = Path.Combine(GitCommandRunner.RepoRoot, filePath);

            // Checkout options — only available when viewing a specific commit
            if (_commit != null)
            {
                menu.AddItem(new GUIContent("Checkout this version (after commit)"), false, () =>
                {
                    if (EditorUtility.DisplayDialog("Checkout File",
                        $"Restore '{filePath}' to its state IN commit {_commit.ShortHash}?\nThis stages the change.",
                        "Checkout", "Cancel"))
                    {
                        var r = GitCommandRunner.Run("checkout", _commit.Hash, "--", "\"" + filePath + "\"");
                        if (!r.Success) EditorUtility.DisplayDialog("Checkout Failed", r.Error, "OK");
                        else AssetDatabase.Refresh();
                    }
                });

                menu.AddItem(new GUIContent("Checkout previous version (before commit)"), false, () =>
                {
                    if (EditorUtility.DisplayDialog("Checkout File",
                        $"Restore '{filePath}' to its state BEFORE commit {_commit.ShortHash}?\nThis stages the change.",
                        "Checkout", "Cancel"))
                    {
                        var r = GitCommandRunner.Run("checkout", _commit.Hash + "^", "--", "\"" + filePath + "\"");
                        if (!r.Success) EditorUtility.DisplayDialog("Checkout Failed", r.Error, "OK");
                        else AssetDatabase.Refresh();
                    }
                });

                menu.AddSeparator("");
            }

            if (filePath.StartsWith("Assets/") || filePath.StartsWith("Assets\\"))
            {
                menu.AddItem(new GUIContent("Ping in Project"), false, () =>
                {
                    var obj = AssetDatabase.LoadAssetAtPath<Object>(filePath);
                    if (obj != null) { EditorGUIUtility.PingObject(obj); Selection.activeObject = obj; }
                });
            }

            menu.AddItem(new GUIContent("Show in Explorer"), false, () => EditorUtility.RevealInFinder(fullPath));
            menu.AddItem(new GUIContent("File History"), false, () => FileHistoryPopup.Show(filePath));
            menu.ShowAsContext();
        }
    }
}
