using UnityEditor;
using UnityEngine;

namespace GitEditor
{
    public class CommitSectionDrawer
    {
        string _commitMessage = "";
        public System.Action OnCommitted;

        public void Draw()
        {
            EditorGUILayout.LabelField("Commit", GitStyles.Header);

            _commitMessage = EditorGUILayout.TextArea(_commitMessage, GitStyles.CommitMessage,
                GUILayout.MinHeight(40), GUILayout.MaxHeight(80));

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            GUI.enabled = !string.IsNullOrWhiteSpace(_commitMessage);
            if (GUILayout.Button("Commit", GUILayout.Width(80), GUILayout.Height(24)))
            {
                var result = GitStatusService.Commit(_commitMessage.Trim());
                if (result.Success)
                {
                    _commitMessage = "";
                    GUI.FocusControl(null);
                    OnCommitted?.Invoke();
                }
                else
                {
                    EditorUtility.DisplayDialog("Commit Failed", result.Error, "OK");
                }
            }
            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();
        }
    }
}
