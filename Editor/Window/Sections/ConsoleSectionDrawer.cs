using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GitEditor
{
    public class ConsoleSectionDrawer
    {
        struct ConsoleEntry
        {
            public string Command;
            public string Output;
            public bool IsError;
        }

        readonly List<ConsoleEntry> _entries = new List<ConsoleEntry>();
        string _commandInput = "";
        Vector2 _scrollPos;
        bool _isOpen;
        bool _autoScroll = true;

        public void Initialize()
        {
            _isOpen = GitSettings.ConsoleSectionOpen;
            GitCommandRunner.OnCommandCompleted += OnCommandCompleted;
        }

        public void Dispose()
        {
            GitCommandRunner.OnCommandCompleted -= OnCommandCompleted;
        }

        void OnCommandCompleted(string command, GitResult result)
        {
            _entries.Add(new ConsoleEntry
            {
                Command = command,
                Output = result.Success ? result.Output : result.Error,
                IsError = !result.Success
            });

            if (_entries.Count > 200)
                _entries.RemoveRange(0, _entries.Count - 200);
        }

        // Approximate pixel heights of the non-scroll rows when the section is open
        const float HeaderRowHeight = 20f;
        const float InputRowHeight  = 22f;

        public void Draw(float totalHeight = 162f)
        {
            // Dark background covering the full console section height
            var topRect = GUILayoutUtility.GetRect(0, 0, GUILayout.ExpandWidth(true), GUILayout.Height(0));
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(new Rect(topRect.x, topRect.y, topRect.width, totalHeight),
                    new Color(0.08f, 0.08f, 0.08f, 0.85f));

            EditorGUILayout.BeginHorizontal();
            bool newOpen = EditorGUILayout.Foldout(_isOpen, "Console", true, GitStyles.SectionHeader);
            if (newOpen != _isOpen)
            {
                _isOpen = newOpen;
                GitSettings.ConsoleSectionOpen = _isOpen;
            }
            GUILayout.FlexibleSpace();
            if (_entries.Count > 0 && GUILayout.Button("Clear", EditorStyles.miniButton, GUILayout.Width(45)))
                _entries.Clear();
            EditorGUILayout.EndHorizontal();

            if (!_isOpen) return;

            float scrollHeight = Mathf.Max(20f, totalHeight - HeaderRowHeight - InputRowHeight);
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.Height(scrollHeight));

            foreach (var entry in _entries)
            {
                var color = GUI.contentColor;
                GUI.contentColor = new Color(0.6f, 0.8f, 1f);
                GUILayout.Label("$ " + entry.Command, GitStyles.MonoLabel);
                GUI.contentColor = entry.IsError ? new Color(1f, 0.5f, 0.5f) : color;
                if (!string.IsNullOrWhiteSpace(entry.Output))
                {
                    string output = entry.Output.Length > 2000
                        ? entry.Output.Substring(0, 2000) + "\n… (truncated)"
                        : entry.Output;
                    GUILayout.Label(output.TrimEnd(), GitStyles.MonoLabel);
                }
                GUI.contentColor = color;
            }

            EditorGUILayout.EndScrollView();

            if (_autoScroll && _entries.Count > 0)
                _scrollPos.y = float.MaxValue;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("$", GUILayout.Width(12));
            GUI.SetNextControlName("ConsoleInput");
            _commandInput = EditorGUILayout.TextField(_commandInput);
            if (GUILayout.Button("Run", EditorStyles.miniButton, GUILayout.Width(35))
                || (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return
                    && GUI.GetNameOfFocusedControl() == "ConsoleInput"))
            {
                if (!string.IsNullOrWhiteSpace(_commandInput))
                {
                    string cmd = _commandInput.Trim();
                    _commandInput = "";
                    GUI.FocusControl(null);
                    if (cmd.StartsWith("git ")) cmd = cmd.Substring(4);
                    GitCommandRunner.Run(cmd.Split(' '));
                }
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
