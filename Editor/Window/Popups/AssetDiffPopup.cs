using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GitEditor
{
    public class AssetDiffPopup : EditorWindow
    {
        string _filePath;
        bool _staged;
        string _commitHash;
        List<AssetDiffEntry> _entries = new List<AssetDiffEntry>();
        bool _hasUnrecognized;
        Vector2 _scrollPos;

        public static void Show(string filePath, bool staged = false, string commitHash = null)
        {
            string fileName = Path.GetFileName(filePath);
            var window = GetWindow<AssetDiffPopup>(true, $"Asset Diff \u2014 {fileName}");
            window._filePath = filePath;
            window._staged = staged;
            window._commitHash = commitHash;
            window.minSize = new Vector2(600, 300);
            window.Refresh();
        }

        void Refresh()
        {
            // Get diff with extended context for better YAML parsing
            List<GitFileDiff> diffs;
            if (!string.IsNullOrEmpty(_commitHash))
                diffs = GitDiffService.GetCommitFileDiff(_commitHash, _filePath, 20);
            else
                diffs = GitDiffService.GetFileDiff(_filePath, _staged, 20);

            _entries = UnityYamlDiffParser.Parse(diffs, _filePath, _commitHash);
            _hasUnrecognized = _entries.Any(e => e.ChangeType == AssetDiffChangeType.Unrecognized);
        }

        void OnGUI()
        {
            // Toolbar
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField(_filePath, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton))
                Refresh();
            EditorGUILayout.EndHorizontal();

            if (_entries.Count == 0)
            {
                EditorGUILayout.LabelField("No changes detected.", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            // Entries
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            var recognized = _entries.Where(e => e.ChangeType != AssetDiffChangeType.Unrecognized).ToList();
            var unrecognized = _entries.Where(e => e.ChangeType == AssetDiffChangeType.Unrecognized).ToList();

            foreach (var entry in recognized)
            {
                DrawEntry(entry);
            }

            if (unrecognized.Count > 0)
            {
                EditorGUILayout.Space(8);
                DrawSeparator();
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField($"Unrecognized Changes ({unrecognized.Count})", GitStyles.Header);
                EditorGUILayout.Space(2);

                foreach (var entry in unrecognized)
                {
                    DrawEntry(entry);
                }
            }

            EditorGUILayout.EndScrollView();

            // View in IDE button at the bottom
            if (_hasUnrecognized)
            {
                EditorGUILayout.Space(4);
                DrawSeparator();
                EditorGUILayout.Space(4);
                if (GUILayout.Button("View Text Diff in IDE", GUILayout.Height(28)))
                {
                    OpenInIDE();
                }
                EditorGUILayout.Space(4);
            }
        }

        void DrawEntry(AssetDiffEntry entry)
        {
            EditorGUILayout.BeginHorizontal();

            switch (entry.ChangeType)
            {
                case AssetDiffChangeType.PropertyChanged:
                    DrawPropertyChange(entry);
                    break;

                case AssetDiffChangeType.ComponentAdded:
                    DrawColoredLabel(entry.ToDisplayString(), GitStyles.StagedColor);
                    break;

                case AssetDiffChangeType.ComponentRemoved:
                    DrawColoredLabel(entry.ToDisplayString(), new Color(0.9f, 0.4f, 0.4f));
                    break;

                case AssetDiffChangeType.GameObjectAdded:
                    DrawColoredLabel(entry.ToDisplayString(), GitStyles.StagedColor);
                    break;

                case AssetDiffChangeType.GameObjectRemoved:
                    DrawColoredLabel(entry.ToDisplayString(), new Color(0.9f, 0.4f, 0.4f));
                    break;

                case AssetDiffChangeType.Unrecognized:
                    GUILayout.Label(entry.RawLine ?? "", GitStyles.MonoLabel);
                    break;
            }

            EditorGUILayout.EndHorizontal();
        }

        void DrawPropertyChange(AssetDiffEntry entry)
        {
            string path = "";
            if (!string.IsNullOrEmpty(entry.ObjectName))
                path += $"'{entry.ObjectName}'";
            if (!string.IsNullOrEmpty(entry.ComponentName))
                path += $".'{entry.ComponentName}'";
            if (!string.IsNullOrEmpty(entry.PropertyName))
                path += $".'{entry.PropertyName}'";

            GUILayout.Label(path, GitStyles.MonoLabel);

            var prevColor = GUI.color;

            GUI.color = EditorGUIUtility.isProSkin
                ? new Color(0.9f, 0.4f, 0.4f)
                : new Color(0.7f, 0.0f, 0.0f);
            GUILayout.Label(entry.OldValue ?? "", GitStyles.MonoLabel);

            GUI.color = EditorGUIUtility.isProSkin
                ? new Color(0.7f, 0.7f, 0.7f)
                : new Color(0.4f, 0.4f, 0.4f);
            GUILayout.Label("\u2192", GitStyles.MonoLabel); // arrow

            GUI.color = EditorGUIUtility.isProSkin
                ? new Color(0.4f, 0.9f, 0.4f)
                : new Color(0.0f, 0.5f, 0.0f);
            GUILayout.Label(entry.NewValue ?? "", GitStyles.MonoLabel);

            GUI.color = prevColor;
            GUILayout.FlexibleSpace();
        }

        void DrawColoredLabel(string text, Color color)
        {
            var prevColor = GUI.color;
            GUI.color = color;
            GUILayout.Label(text, GitStyles.MonoLabel);
            GUI.color = prevColor;
            GUILayout.FlexibleSpace();
        }

        void DrawSeparator()
        {
            var rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin
                ? new Color(0.3f, 0.3f, 0.3f)
                : new Color(0.7f, 0.7f, 0.7f));
        }

        void OpenInIDE()
        {
            string fullPath = Path.Combine(GitCommandRunner.RepoRoot, _filePath);
            if (File.Exists(fullPath))
                UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(fullPath, 1);
        }
    }
}
