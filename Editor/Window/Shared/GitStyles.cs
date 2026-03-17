using System;
using UnityEditor;
using UnityEngine;

namespace GitEditor
{
    public static class GitStyles
    {
        static GUIStyle _header;
        public static GUIStyle Header => _header ??= SafeStyle(
            () => new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 },
            () => new GUIStyle { fontSize = 12, fontStyle = FontStyle.Bold });

        static GUIStyle _sectionHeader;
        public static GUIStyle SectionHeader => _sectionHeader ??= SafeStyle(
            () => new GUIStyle(EditorStyles.foldoutHeader) { fontStyle = FontStyle.Bold },
            () => new GUIStyle { fontStyle = FontStyle.Bold });

        static GUIStyle _monoLabel;
        public static GUIStyle MonoLabel
        {
            get
            {
                if (_monoLabel == null)
                {
                    _monoLabel = SafeStyle(
                        () => new GUIStyle(EditorStyles.label)
                        {
                            font = Font.CreateDynamicFontFromOSFont("Consolas", 11),
                            fontSize = 11,
                            wordWrap = false,
                            richText = true
                        },
                        () => new GUIStyle { fontSize = 11, wordWrap = false, richText = true });
                }
                return _monoLabel;
            }
        }

        static GUIStyle _diffAdd;
        public static GUIStyle DiffAdd
        {
            get
            {
                if (_diffAdd == null)
                {
                    _diffAdd = new GUIStyle(MonoLabel);
                    _diffAdd.normal.textColor = EditorGUIUtility.isProSkin
                        ? new Color(0.4f, 0.9f, 0.4f)
                        : new Color(0.0f, 0.5f, 0.0f);
                }
                return _diffAdd;
            }
        }

        static GUIStyle _diffRemove;
        public static GUIStyle DiffRemove
        {
            get
            {
                if (_diffRemove == null)
                {
                    _diffRemove = new GUIStyle(MonoLabel);
                    _diffRemove.normal.textColor = EditorGUIUtility.isProSkin
                        ? new Color(0.9f, 0.4f, 0.4f)
                        : new Color(0.7f, 0.0f, 0.0f);
                }
                return _diffRemove;
            }
        }

        static GUIStyle _diffHunkHeader;
        public static GUIStyle DiffHunkHeader
        {
            get
            {
                if (_diffHunkHeader == null)
                {
                    _diffHunkHeader = new GUIStyle(MonoLabel);
                    _diffHunkHeader.normal.textColor = EditorGUIUtility.isProSkin
                        ? new Color(0.5f, 0.7f, 1.0f)
                        : new Color(0.0f, 0.3f, 0.7f);
                }
                return _diffHunkHeader;
            }
        }

        static GUIStyle _commitMessage;
        public static GUIStyle CommitMessage => _commitMessage ??= SafeStyle(
            () => new GUIStyle(EditorStyles.textArea) { wordWrap = true },
            () => new GUIStyle { wordWrap = true });

        static GUIStyle _selectedRow;
        public static GUIStyle SelectedRow
        {
            get
            {
                if (_selectedRow == null)
                {
                    _selectedRow = SafeStyle(
                        () => new GUIStyle(EditorStyles.label) { richText = true },
                        () => new GUIStyle { richText = true });
                    var tex = new Texture2D(1, 1);
                    tex.SetPixel(0, 0, EditorGUIUtility.isProSkin
                        ? new Color(0.17f, 0.36f, 0.53f)
                        : new Color(0.24f, 0.48f, 0.90f, 0.4f));
                    tex.Apply();
                    _selectedRow.normal.background  = tex;
                    _selectedRow.hover.background   = tex;
                    _selectedRow.active.background  = tex;
                    _selectedRow.focused.background = tex;
                }
                return _selectedRow;
            }
        }

        public static Color StagedColor => EditorGUIUtility.isProSkin
            ? new Color(0.4f, 0.9f, 0.4f) : new Color(0.0f, 0.5f, 0.0f);
        public static Color UnstagedColor => EditorGUIUtility.isProSkin
            ? new Color(0.9f, 0.7f, 0.3f) : new Color(0.6f, 0.4f, 0.0f);
        public static Color UntrackedColor => EditorGUIUtility.isProSkin
            ? new Color(0.7f, 0.7f, 0.7f) : new Color(0.4f, 0.4f, 0.4f);

        // Creates a GUIStyle using the primary factory; falls back to the secondary
        // if EditorStyles is not yet initialized (can happen early after domain reload).
        static GUIStyle SafeStyle(Func<GUIStyle> primary, Func<GUIStyle> fallback)
        {
            try { return primary(); }
            catch { return fallback(); }
        }

        public static void ResetStyles()
        {
            _header = null;
            _sectionHeader = null;
            _monoLabel = null;
            _diffAdd = null;
            _diffRemove = null;
            _diffHunkHeader = null;
            _commitMessage = null;
            _selectedRow = null;
        }
    }
}
