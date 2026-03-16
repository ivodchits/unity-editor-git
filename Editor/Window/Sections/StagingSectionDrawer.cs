using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GitEditor
{
    public class StagingSectionDrawer
    {
        List<GitFileChange> _staged = new List<GitFileChange>();
        List<GitFileChange> _unstaged = new List<GitFileChange>();
        bool _stagedFoldout = true;
        bool _unstagedFoldout = true;
        Vector2 _scrollPos;
        GitFileChange _selectedChange;

        public GitFileChange SelectedChange => _selectedChange;
        public System.Action OnChanged;

        public void Refresh()
        {
            var all = GitStatusService.GetStatus();
            _staged = all.Where(f => f.Staged).ToList();
            _unstaged = all.Where(f => !f.Staged).ToList();
        }

        float _scrollHeight = 150;

        public void Draw(float scrollHeight = 150)
        {
            _scrollHeight = scrollHeight;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Staging Area", GitStyles.Header);
            GUILayout.FlexibleSpace();
            if (_unstaged.Count > 0 && GUILayout.Button("Stage All", GUILayout.Width(70)))
            {
                GitStatusService.StageAll();
                Refresh();
                OnChanged?.Invoke();
            }
            if (_staged.Count > 0 && GUILayout.Button("Unstage All", GUILayout.Width(80)))
            {
                GitStatusService.UnstageAll();
                Refresh();
                OnChanged?.Invoke();
            }
            EditorGUILayout.EndHorizontal();

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.Height(_scrollHeight));

            // Staged files
            _stagedFoldout = EditorGUILayout.Foldout(_stagedFoldout, $"Staged ({_staged.Count})", true);
            if (_stagedFoldout)
            {
                foreach (var file in _staged)
                    DrawFileRow(file);
            }

            // Unstaged files
            _unstagedFoldout = EditorGUILayout.Foldout(_unstagedFoldout, $"Unstaged ({_unstaged.Count})", true);
            if (_unstagedFoldout)
            {
                foreach (var file in _unstaged)
                    DrawFileRow(file);
            }

            EditorGUILayout.EndScrollView();

            if (_unstaged.Count > 0)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUI.color = new Color(1f, 0.6f, 0.6f);
                if (GUILayout.Button("Revert All Unstaged", GUILayout.Width(140)))
                {
                    if (EditorUtility.DisplayDialog("Revert All",
                        "This will discard all unstaged changes. This cannot be undone.", "Revert", "Cancel"))
                    {
                        GitStatusService.RevertAll();
                        Refresh();
                        OnChanged?.Invoke();
                    }
                }
                GUI.color = Color.white;
                EditorGUILayout.EndHorizontal();
            }
        }

        void DrawFileRow(GitFileChange file)
        {
            bool isSelected = _selectedChange == file;
            var style = isSelected ? GitStyles.SelectedRow : EditorStyles.label;

            var rowRect = EditorGUILayout.BeginHorizontal();

            // Right-click check must come BEFORE any Button() calls inside this row.
            // GUI.Button consumes MouseDown for ALL mouse buttons (to set hotControl),
            // so by EndHorizontal the event type is already Changed to Used.
            if (Event.current.type == EventType.MouseDown && Event.current.button == 1
                && rowRect.Contains(Event.current.mousePosition))
            {
                ShowContextMenu(file);
                Event.current.Use();
            }

            // Status indicator
            var prevColor = GUI.color;
            GUI.color = file.Staged ? GitStyles.StagedColor
                : file.Status == FileStatus.Untracked ? GitStyles.UntrackedColor
                : GitStyles.UnstagedColor;
            GUILayout.Label(file.StatusChar, GUILayout.Width(16));
            GUI.color = prevColor;

            // File name (clickable)
            string fileName = Path.GetFileName(file.Path);
            string dirPath = Path.GetDirectoryName(file.Path)?.Replace('\\', '/');
            string display = string.IsNullOrEmpty(dirPath) ? fileName : $"{fileName}  <color=#888>{dirPath}/</color>";

            if (GUILayout.Button(display, isSelected ? GitStyles.SelectedRow : GitStyles.MonoLabel))
            {
                _selectedChange = file;
                OnChanged?.Invoke(); // triggers diff display in info pane
            }

            GUILayout.FlexibleSpace();

            // Action buttons
            if (file.Staged)
            {
                if (GUILayout.Button("Unstage", EditorStyles.miniButton, GUILayout.Width(55)))
                {
                    GitStatusService.Unstage(file.Path);
                    Refresh();
                    OnChanged?.Invoke();
                }
            }
            else
            {
                if (GUILayout.Button("Stage", EditorStyles.miniButton, GUILayout.Width(45)))
                {
                    GitStatusService.Stage(file.Path);
                    Refresh();
                    OnChanged?.Invoke();
                }
            }

            if (!file.Staged && file.Status != FileStatus.Untracked)
            {
                GUI.color = new Color(1f, 0.6f, 0.6f);
                if (GUILayout.Button("Revert", EditorStyles.miniButton, GUILayout.Width(50)))
                {
                    if (EditorUtility.DisplayDialog("Revert File",
                        $"Revert changes to {file.Path}? This cannot be undone.", "Revert", "Cancel"))
                    {
                        GitStatusService.Revert(file.Path);
                        Refresh();
                        OnChanged?.Invoke();
                    }
                }
                GUI.color = prevColor;
            }
            else if (!file.Staged && file.Status == FileStatus.Untracked)
            {
                GUI.color = new Color(1f, 0.6f, 0.6f);
                if (GUILayout.Button("Delete", EditorStyles.miniButton, GUILayout.Width(50)))
                {
                    if (EditorUtility.DisplayDialog("Delete File",
                        $"Delete untracked file {file.Path}? This cannot be undone.", "Delete", "Cancel"))
                    {
                        GitStatusService.DiscardUntracked(file.Path);
                        Refresh();
                        OnChanged?.Invoke();
                    }
                }
                GUI.color = prevColor;
            }

            EditorGUILayout.EndHorizontal();
        }

        void ShowContextMenu(GitFileChange file)
        {
            var menu = new GenericMenu();

            if (file.Staged)
                menu.AddItem(new GUIContent("Unstage"), false, () => { GitStatusService.Unstage(file.Path); Refresh(); OnChanged?.Invoke(); });
            else
                menu.AddItem(new GUIContent("Stage"), false, () => { GitStatusService.Stage(file.Path); Refresh(); OnChanged?.Invoke(); });

            if (!file.Staged && file.Status != FileStatus.Untracked)
            {
                menu.AddItem(new GUIContent("Revert"), false, () =>
                {
                    if (EditorUtility.DisplayDialog("Revert File", $"Revert {file.Path}?", "Revert", "Cancel"))
                    {
                        GitStatusService.Revert(file.Path);
                        Refresh();
                        OnChanged?.Invoke();
                    }
                });
            }

            menu.AddSeparator("");

            string fullPath = Path.Combine(GitCommandRunner.RepoRoot, file.Path);
            if (file.Path.StartsWith("Assets/") || file.Path.StartsWith("Assets\\"))
            {
                menu.AddItem(new GUIContent("Ping in Project"), false, () =>
                {
                    var obj = AssetDatabase.LoadAssetAtPath<Object>(file.Path);
                    if (obj != null)
                    {
                        EditorGUIUtility.PingObject(obj);
                        Selection.activeObject = obj;
                    }
                });
            }

            menu.AddItem(new GUIContent("Show in Explorer"), false, () => EditorUtility.RevealInFinder(fullPath));
            menu.AddItem(new GUIContent("File History"), false, () => FileHistoryPopup.Show(file.Path));

            menu.ShowAsContext();
        }
    }
}
