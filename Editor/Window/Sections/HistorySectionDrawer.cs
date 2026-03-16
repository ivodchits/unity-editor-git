using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GitEditor
{
    public class HistorySectionDrawer
    {
        List<GitCommit> _commits = new List<GitCommit>();
        Vector2 _scrollPos;
        int _selectedIndex = -1;
        string _filterAuthor = "";

        public GitCommit SelectedCommit => _selectedIndex >= 0 && _selectedIndex < _commits.Count
            ? _commits[_selectedIndex] : null;

        public System.Action<GitCommit> OnCommitSelected;
        public System.Action OnRefreshNeeded;

        public void Refresh()
        {
            _commits = GitLogService.GetLog(GitSettings.LogCount);
        }

        public void Draw(float scrollHeight = 200)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("History", GitStyles.Header, GUILayout.Width(60));
            GUILayout.FlexibleSpace();
            _filterAuthor = EditorGUILayout.TextField(_filterAuthor, EditorStyles.toolbarSearchField, GUILayout.Width(100));
            EditorGUILayout.EndHorizontal();

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.Height(scrollHeight));

            for (int i = 0; i < _commits.Count; i++)
            {
                var commit = _commits[i];

                if (!string.IsNullOrEmpty(_filterAuthor) &&
                    !commit.Author.ToLower().Contains(_filterAuthor.ToLower()) &&
                    !commit.Message.ToLower().Contains(_filterAuthor.ToLower()))
                    continue;

                bool isSelected = i == _selectedIndex;

                var rowRect = EditorGUILayout.BeginHorizontal(isSelected ? GitStyles.SelectedRow : GUIStyle.none);

                // Handle clicks before any child controls — right-click especially
                if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
                {
                    if (Event.current.button == 0)
                    {
                        _selectedIndex = i;
                        OnCommitSelected?.Invoke(commit);
                        Event.current.Use();
                    }
                    else if (Event.current.button == 1)
                    {
                        ShowCommitMenu(commit);
                        Event.current.Use();
                    }
                }

                GUILayout.Label(commit.ShortHash, GitStyles.MonoLabel, GUILayout.Width(60));

                if (commit.Refs.Count > 0)
                {
                    var prevColor = GUI.contentColor;
                    GUI.contentColor = new Color(0.5f, 0.8f, 1f);
                    GUILayout.Label($"[{string.Join(", ", commit.Refs)}]", EditorStyles.miniLabel, GUILayout.MaxWidth(150));
                    GUI.contentColor = prevColor;
                }

                GUILayout.Label(commit.Message, EditorStyles.label);
                GUILayout.FlexibleSpace();
                GUILayout.Label(commit.Author, EditorStyles.miniLabel, GUILayout.Width(80));
                string shortDate = commit.Date.Length > 10 ? commit.Date.Substring(0, 10) : commit.Date;
                GUILayout.Label(shortDate, EditorStyles.miniLabel, GUILayout.Width(70));

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        void ShowCommitMenu(GitCommit commit)
        {
            var menu = new GenericMenu();

            menu.AddItem(new GUIContent("Copy Hash"), false, () =>
                EditorGUIUtility.systemCopyBuffer = commit.Hash);
            menu.AddItem(new GUIContent("Copy Short Hash"), false, () =>
                EditorGUIUtility.systemCopyBuffer = commit.ShortHash);

            menu.AddSeparator("");

            menu.AddItem(new GUIContent("Checkout (detached HEAD)"), false, () =>
            {
                if (EditorUtility.DisplayDialog("Checkout Commit",
                    $"Checkout commit {commit.ShortHash}?\nThis will create a detached HEAD.", "Checkout", "Cancel"))
                {
                    var r = GitCommandRunner.Run("checkout", commit.Hash);
                    if (!r.Success) EditorUtility.DisplayDialog("Failed", r.Error, "OK");
                    else OnRefreshNeeded?.Invoke();
                }
            });

            menu.AddItem(new GUIContent("Create Branch Here…"), false, () =>
                CreateBranchPopup.Show(commit.Hash, OnRefreshNeeded));

            menu.AddSeparator("");

            menu.AddItem(new GUIContent("Revert Commit"), false, () =>
            {
                if (EditorUtility.DisplayDialog("Revert Commit",
                    $"Create a new commit that undoes {commit.ShortHash}?", "Revert", "Cancel"))
                {
                    var r = GitCommandRunner.Run("revert", "--no-edit", commit.Hash);
                    if (!r.Success) EditorUtility.DisplayDialog("Failed", r.Error, "OK");
                    else OnRefreshNeeded?.Invoke();
                }
            });

            menu.AddItem(new GUIContent("Soft Reset to Here"), false, () =>
            {
                if (EditorUtility.DisplayDialog("Soft Reset",
                    $"Soft reset to {commit.ShortHash}?\nCommits after this become staged changes.", "Reset", "Cancel"))
                {
                    var r = GitCommandRunner.Run("reset", "--soft", commit.Hash);
                    if (!r.Success) EditorUtility.DisplayDialog("Failed", r.Error, "OK");
                    else OnRefreshNeeded?.Invoke();
                }
            });

            menu.AddItem(new GUIContent("Hard Reset to Here"), false, () =>
            {
                if (EditorUtility.DisplayDialog("Hard Reset",
                    $"HARD reset to {commit.ShortHash}?\nALL changes after this will be permanently lost.", "Hard Reset", "Cancel"))
                {
                    var r = GitCommandRunner.Run("reset", "--hard", commit.Hash);
                    if (!r.Success) EditorUtility.DisplayDialog("Failed", r.Error, "OK");
                    else OnRefreshNeeded?.Invoke();
                }
            });

            menu.ShowAsContext();
        }
    }
}
