using UnityEditor;
using UnityEngine;

namespace GitEditor
{
    public static class GitSettings
    {
        static string KeyPrefix => "GitEditor_" + Application.dataPath.GetHashCode() + "_";

        public static string RepoRoot
        {
            get => EditorPrefs.GetString(KeyPrefix + "RepoRoot", "");
            set => EditorPrefs.SetString(KeyPrefix + "RepoRoot", value);
        }

        public static int LogCount
        {
            get => EditorPrefs.GetInt(KeyPrefix + "LogCount", 100);
            set => EditorPrefs.SetInt(KeyPrefix + "LogCount", value);
        }

        public static bool PullRebase
        {
            get => EditorPrefs.GetBool(KeyPrefix + "PullRebase", true);
            set => EditorPrefs.SetBool(KeyPrefix + "PullRebase", value);
        }

        public static bool ConsoleSectionOpen
        {
            get => EditorPrefs.GetBool(KeyPrefix + "ConsoleSectionOpen", false);
            set => EditorPrefs.SetBool(KeyPrefix + "ConsoleSectionOpen", value);
        }

        public static float SplitRatio
        {
            get => EditorPrefs.GetFloat(KeyPrefix + "SplitRatio", 0.55f);
            set => EditorPrefs.SetFloat(KeyPrefix + "SplitRatio", value);
        }

        public static float StagingHeight
        {
            get => EditorPrefs.GetFloat(KeyPrefix + "StagingHeight", 150f);
            set => EditorPrefs.SetFloat(KeyPrefix + "StagingHeight", value);
        }

        public static float HistoryHeight
        {
            get => EditorPrefs.GetFloat(KeyPrefix + "HistoryHeight", 200f);
            set => EditorPrefs.SetFloat(KeyPrefix + "HistoryHeight", value);
        }

        public static float ConsoleHeight
        {
            get => EditorPrefs.GetFloat(KeyPrefix + "ConsoleHeight", 162f);
            set => EditorPrefs.SetFloat(KeyPrefix + "ConsoleHeight", value);
        }
    }
}
