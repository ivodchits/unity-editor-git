using UnityEditor;
using UnityEngine;

namespace GitEditor
{
    public class PullPushSectionDrawer
    {
        GitAsyncOperation _activeOp;
        string _activeLabel;
        public System.Action OnCompleted;

        public bool IsRunning => _activeOp != null && !_activeOp.IsDone;

        public void Draw()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Remote", GitStyles.Header, GUILayout.Width(60));

            GUI.enabled = !IsRunning;

            if (GUILayout.Button("Pull", GUILayout.Width(50)))
            {
                _activeOp = GitRemoteService.Pull(GitSettings.PullRebase);
                _activeLabel = "Pulling...";
            }

            GitSettings.PullRebase = GUILayout.Toggle(GitSettings.PullRebase, "Rebase", GUILayout.Width(60));

            if (GUILayout.Button("Push", GUILayout.Width(50)))
            {
                _activeOp = GitRemoteService.Push();
                _activeLabel = "Pushing...";
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Fetch All", GUILayout.Width(70)))
            {
                _activeOp = GitRemoteService.FetchAll();
                _activeLabel = "Fetching...";
            }

            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            if (IsRunning)
            {
                EditorGUILayout.HelpBox(_activeLabel, MessageType.Info);
            }
        }

        public void Update()
        {
            if (_activeOp != null && _activeOp.IsDone)
            {
                var result = _activeOp.Result;
                _activeOp = null;

                if (!result.Success && !string.IsNullOrEmpty(result.Error))
                    EditorUtility.DisplayDialog("Git Operation Failed", result.Error, "OK");

                OnCompleted?.Invoke();
            }
        }
    }
}
