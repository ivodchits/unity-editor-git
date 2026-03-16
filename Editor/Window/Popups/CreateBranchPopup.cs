using UnityEditor;
using UnityEngine;

namespace GitEditor
{
    public class CreateBranchPopup : EditorWindow
    {
        string _fromHash;
        string _branchName = "";
        System.Action _onCreated;

        public static void Show(string fromHash, System.Action onCreated = null)
        {
            var window = CreateInstance<CreateBranchPopup>();
            window._fromHash = fromHash;
            window._onCreated = onCreated;
            window.titleContent = new GUIContent("Create Branch");
            window.minSize = window.maxSize = new Vector2(320, 76);
            window.ShowAuxWindow();
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField($"New branch at {_fromHash.Substring(0, System.Math.Min(8, _fromHash.Length))}:");
            GUI.SetNextControlName("BranchNameField");
            _branchName = EditorGUILayout.TextField("Branch name:", _branchName);

            if (Event.current.type == EventType.Layout)
                EditorGUI.FocusTextInControl("BranchNameField");

            EditorGUILayout.BeginHorizontal();
            GUI.enabled = !string.IsNullOrWhiteSpace(_branchName);
            if (GUILayout.Button("Create") ||
                (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return))
            {
                var result = GitCommandRunner.Run("checkout", "-b", _branchName.Trim(), _fromHash);
                if (result.Success)
                {
                    _onCreated?.Invoke();
                    Close();
                }
                else
                {
                    EditorUtility.DisplayDialog("Create Branch Failed", result.Error, "OK");
                }
            }
            GUI.enabled = true;
            if (GUILayout.Button("Cancel"))
                Close();
            EditorGUILayout.EndHorizontal();
        }
    }
}
