using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GitEditor
{
    public class FileHistoryPopup : EditorWindow
    {
        string _filePath;
        List<GitCommit> _commits = new List<GitCommit>();
        List<GitFileDiff> _diffs = new List<GitFileDiff>();
        int _selectedIndex = -1;
        Vector2 _commitScrollPos;
        Vector2 _diffScrollPos;

        public static void Show(string filePath)
        {
            var window = GetWindow<FileHistoryPopup>(true, $"History — {filePath}");
            window._filePath = filePath;
            window.minSize = new Vector2(700, 400);
            window.Refresh();
        }

        void Refresh()
        {
            _commits = GitLogService.GetLog(200, _filePath);
            _selectedIndex = -1;
            _diffs.Clear();
        }

        void OnGUI()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField(_filePath, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton))
                Refresh();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();

            // Left: commit list
            EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.45f));
            _commitScrollPos = EditorGUILayout.BeginScrollView(_commitScrollPos);

            for (int i = 0; i < _commits.Count; i++)
            {
                var commit = _commits[i];
                bool isSel = i == _selectedIndex;

                EditorGUILayout.BeginHorizontal(isSel ? GitStyles.SelectedRow : GUIStyle.none);
                GUILayout.Label(commit.ShortHash, GitStyles.MonoLabel, GUILayout.Width(55));
                GUILayout.Label(commit.Message, EditorStyles.label);
                GUILayout.FlexibleSpace();
                string shortDate = commit.Date.Length > 10 ? commit.Date.Substring(0, 10) : commit.Date;
                GUILayout.Label(shortDate, EditorStyles.miniLabel, GUILayout.Width(70));
                EditorGUILayout.EndHorizontal();

                var rect = GUILayoutUtility.GetLastRect();
                if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
                {
                    _selectedIndex = i;
                    _diffs = GitDiffService.GetCommitDiff(commit.Hash);
                    Event.current.Use();
                    Repaint();
                }
            }

            EditorGUILayout.EndScrollView();

            // Revert button
            if (_selectedIndex >= 0)
            {
                GUI.color = new Color(1f, 0.7f, 0.5f);
                if (GUILayout.Button("Revert file to this version"))
                {
                    var commit = _commits[_selectedIndex];
                    if (EditorUtility.DisplayDialog("Revert File",
                        $"Revert '{_filePath}' to commit {commit.ShortHash}?\nThis stages the reverted version.",
                        "Revert", "Cancel"))
                    {
                        GitCommandRunner.Run("checkout", commit.Hash, "--", "\"" + _filePath + "\"");
                    }
                }
                GUI.color = Color.white;
            }

            EditorGUILayout.EndVertical();

            // Right: diff
            EditorGUILayout.BeginVertical();
            _diffScrollPos = EditorGUILayout.BeginScrollView(_diffScrollPos);

            if (_diffs.Count > 0)
            {
                // Only show diffs for the selected file
                foreach (var fileDiff in _diffs)
                {
                    if (fileDiff.FilePath != null && !fileDiff.FilePath.Replace('\\', '/').EndsWith(_filePath.Replace('\\', '/')))
                        continue;

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
            }
            else if (_selectedIndex >= 0)
            {
                EditorGUILayout.LabelField("No diff available for this commit.", EditorStyles.centeredGreyMiniLabel);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }
    }
}
