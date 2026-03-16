using UnityEditor;
using UnityEngine;

namespace GitEditor
{
    public static class GitIcons
    {
        static GUIContent _refresh;
        public static GUIContent Refresh => _refresh ??= EditorGUIUtility.IconContent("d_Refresh");

        static GUIContent _folder;
        public static GUIContent Folder => _folder ??= EditorGUIUtility.IconContent("d_Folder Icon");

        static GUIContent _console;
        public static GUIContent Console => _console ??= EditorGUIUtility.IconContent("d_UnityEditor.ConsoleWindow");

        static GUIContent _plus;
        public static GUIContent Plus => _plus ??= EditorGUIUtility.IconContent("d_CreateAddNew");

        static GUIContent _minus;
        public static GUIContent Minus => _minus ??= EditorGUIUtility.IconContent("d_ol_minus");

        static GUIContent _warning;
        public static GUIContent Warning => _warning ??= EditorGUIUtility.IconContent("console.warnicon.sml");

        static GUIContent _info;
        public static GUIContent Info => _info ??= EditorGUIUtility.IconContent("console.infoicon.sml");
    }
}
