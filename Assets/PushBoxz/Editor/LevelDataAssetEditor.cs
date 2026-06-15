using PushBoxz.Data;
using UnityEditor;
using UnityEngine;

namespace PushBoxz.Editor
{
    /// <summary>
    /// Adds inspector and asset-context shortcuts for opening a LevelDataAsset directly
    /// in the dedicated PushBoxz level editor.
    /// </summary>
    [CustomEditor(typeof(LevelDataAsset))]
    public class LevelDataAssetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(8f);
            if (GUILayout.Button("打开关卡编辑器"))
            {
                PushBoxzLevelEditorWindow.OpenWithAsset((LevelDataAsset)target);
            }
        }

        [MenuItem("Assets/PushBoxz/用关卡编辑器打开", true)]
        private static bool ValidateOpenSelectedLevel()
        {
            return Selection.activeObject is LevelDataAsset;
        }

        [MenuItem("Assets/PushBoxz/用关卡编辑器打开")]
        private static void OpenSelectedLevel()
        {
            PushBoxzLevelEditorWindow.OpenWithAsset(Selection.activeObject as LevelDataAsset);
        }
    }
}
