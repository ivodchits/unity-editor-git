using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GitEditor
{
    public class BranchesSectionDrawer
    {
        List<GitBranch> _local = new List<GitBranch>();
        List<GitBranch> _remote = new List<GitBranch>();
        bool _localFoldout = true;
        bool _remoteFoldout = true;
        Vector2 _scrollPos;
        string _currentBranch = "";
        string _newBranchName = "";

        public System.Action OnChanged;

        public void Refresh()
        {
            var all = GitBranchService.GetBranches();
            _local = all.Where(b => !b.IsRemote).ToList();
            _remote = all.Where(b => b.IsRemote).ToList();
            _currentBranch = GitBranchService.GetCurrentBranch();
        }

        public void Draw(float scrollHeight = 200)
        {
            EditorGUILayout.LabelField("Branches", GitStyles.Header);

            // New branch
            EditorGUILayout.BeginHorizontal();
            _newBranchName = EditorGUILayout.TextField(_newBranchName, GUILayout.MinWidth(100));
            GUI.enabled = !string.IsNullOrWhiteSpace(_newBranchName);
            if (GUILayout.Button("Create", EditorStyles.miniButton, GUILayout.Width(50)))
            {
                var result = GitBranchService.CreateAndCheckout(_newBranchName.Trim());
                if (result.Success)
                {
                    _newBranchName = "";
                    Refresh();
                    OnChanged?.Invoke();
                }
                else
                    EditorUtility.DisplayDialog("Error", result.Error, "OK");
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.Height(scrollHeight));

            // Local branches
            _localFoldout = EditorGUILayout.Foldout(_localFoldout, $"Local ({_local.Count})", true);
            if (_localFoldout)
            {
                foreach (var branch in _local)
                    DrawBranchRow(branch);
            }

            // Remote branches
            _remoteFoldout = EditorGUILayout.Foldout(_remoteFoldout, $"Remote ({_remote.Count})", true);
            if (_remoteFoldout)
            {
                foreach (var branch in _remote)
                    DrawBranchRow(branch);
            }

            EditorGUILayout.EndScrollView();
        }

        void DrawBranchRow(GitBranch branch)
        {
            var rowRect = EditorGUILayout.BeginHorizontal();

            // Handle clicks before any child controls can consume the event
            if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
            {
                if (Event.current.button == 1)
                {
                    ShowBranchMenu(branch);
                    Event.current.Use();
                }
                else if (Event.current.button == 0 && Event.current.clickCount == 2 && !branch.IsCurrent)
                {
                    string target = branch.IsRemote ? branch.Name : branch.DisplayName;
                    var result = GitBranchService.Checkout(target);
                    if (result.Success) { Refresh(); OnChanged?.Invoke(); }
                    else EditorUtility.DisplayDialog("Checkout Failed", result.Error, "OK");
                    Event.current.Use();
                }
            }

            // Current branch indicator
            string prefix = branch.IsCurrent ? "> " : "  ";
            var labelStyle = branch.IsCurrent ? EditorStyles.boldLabel : EditorStyles.label;
            GUILayout.Label(prefix + branch.DisplayName, labelStyle);

            // Ahead/behind
            if (branch.Ahead > 0 || branch.Behind > 0)
            {
                string info = "";
                if (branch.Ahead > 0) info += $"+{branch.Ahead}";
                if (branch.Behind > 0) info += (info.Length > 0 ? " " : "") + $"-{branch.Behind}";
                GUILayout.Label(info, EditorStyles.miniLabel, GUILayout.Width(50));
            }

            GUILayout.FlexibleSpace();

            EditorGUILayout.EndHorizontal();
        }

        void ShowBranchMenu(GitBranch branch)
        {
            var menu = new GenericMenu();

            if (!branch.IsCurrent)
            {
                string target = branch.IsRemote ? branch.Name : branch.DisplayName;
                menu.AddItem(new GUIContent("Checkout"), false, () =>
                {
                    var result = GitBranchService.Checkout(target);
                    if (result.Success) { Refresh(); OnChanged?.Invoke(); }
                    else EditorUtility.DisplayDialog("Checkout Failed", result.Error, "OK");
                });

                menu.AddItem(new GUIContent("Merge into current"), false, () =>
                {
                    var result = GitBranchService.Merge(branch.DisplayName);
                    if (result.Success) { Refresh(); OnChanged?.Invoke(); }
                    else EditorUtility.DisplayDialog("Merge Failed", result.Error, "OK");
                });

                menu.AddItem(new GUIContent("Rebase onto current"), false, () =>
                {
                    var result = GitBranchService.Rebase(branch.DisplayName);
                    if (result.Success) { Refresh(); OnChanged?.Invoke(); }
                    else EditorUtility.DisplayDialog("Rebase Failed", result.Error, "OK");
                });
            }

            if (!branch.IsRemote && !branch.IsCurrent)
            {
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Delete"), false, () =>
                {
                    if (EditorUtility.DisplayDialog("Delete Branch",
                        $"Delete branch '{branch.Name}'?", "Delete", "Cancel"))
                    {
                        var result = GitBranchService.DeleteBranch(branch.Name);
                        if (result.Success) { Refresh(); OnChanged?.Invoke(); }
                        else
                        {
                            if (EditorUtility.DisplayDialog("Force Delete?",
                                $"Branch not fully merged. Force delete?\n\n{result.Error}", "Force Delete", "Cancel"))
                            {
                                GitBranchService.DeleteBranch(branch.Name, true);
                                Refresh();
                                OnChanged?.Invoke();
                            }
                        }
                    }
                });
            }

            menu.ShowAsContext();
        }
    }
}
