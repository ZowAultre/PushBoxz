using PushBoxz.Data;
using PushBoxz.Presentation;
using UnityEditor;
using UnityEngine;

namespace PushBoxz.Editor
{
    public class PushBoxzLevelSceneBuilderRegistryWindow : EditorWindow
    {
        private LevelSceneBuilderRegistry registry;
        private SerializedObject serializedRegistry;
        private SerializedProperty levelsProperty;
        private Vector2 scrollPosition;

        [MenuItem("Tools/PushBoxz/Level Registry")]
        public static void ShowWindow()
        {
            var window = GetWindow<PushBoxzLevelSceneBuilderRegistryWindow>("关卡列表");
            window.minSize = new Vector2(560f, 320f);
            window.LoadRegistry();
            window.Show();
        }

        private void OnEnable()
        {
            LoadRegistry();
        }

        private void OnGUI()
        {
            DrawRegistryField();
            if (registry == null)
            {
                EditorGUILayout.HelpBox("请先创建或选择 Level Scene Builder Registry。", MessageType.Info);
                return;
            }

            serializedRegistry.Update();
            DrawToolbar();
            EditorGUILayout.Space(6f);
            DrawLevelList();
            serializedRegistry.ApplyModifiedProperties();
        }

        private void DrawRegistryField()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUI.BeginChangeCheck();
                var nextRegistry = (LevelSceneBuilderRegistry)EditorGUILayout.ObjectField(registry, typeof(LevelSceneBuilderRegistry), false);
                if (EditorGUI.EndChangeCheck())
                {
                    SetRegistry(nextRegistry);
                }

                if (GUILayout.Button("重建", EditorStyles.toolbarButton, GUILayout.Width(64f)))
                {
                    SetRegistry(PushBoxzLevelRegistryBuilder.RebuildRegistryAsset());
                }
            }
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("新增"))
                {
                    AddEntry();
                }

                if (GUILayout.Button("全部启用"))
                {
                    SetAllStates(enabledValue: true, keepUnlocked: true);
                }

                if (GUILayout.Button("全部禁用"))
                {
                    SetAllStates(enabledValue: false, keepUnlocked: true);
                }

                if (GUILayout.Button("全部解锁"))
                {
                    SetAllStates(unlockedValue: true, keepEnabled: true);
                }

                if (GUILayout.Button("全部锁定"))
                {
                    SetAllStates(unlockedValue: false, keepEnabled: true);
                }
            }
        }

        private void DrawLevelList()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("关卡资源", EditorStyles.boldLabel);
                GUILayout.Label("启用", EditorStyles.boldLabel, GUILayout.Width(48f));
                GUILayout.Label("解锁", EditorStyles.boldLabel, GUILayout.Width(48f));
                GUILayout.Label(string.Empty, GUILayout.Width(52f));
            }

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            for (var i = 0; i < levelsProperty.arraySize; i++)
            {
                DrawEntryRow(i);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawEntryRow(int index)
        {
            var entryProperty = levelsProperty.GetArrayElementAtIndex(index);
            var levelProperty = entryProperty.FindPropertyRelative("level");
            var enabledProperty = entryProperty.FindPropertyRelative("enabled");
            var unlockedProperty = entryProperty.FindPropertyRelative("unlocked");

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                var nextLevel = (LevelDataAsset)EditorGUILayout.ObjectField(levelProperty.objectReferenceValue, typeof(LevelDataAsset), false);
                var nextEnabled = EditorGUILayout.Toggle(enabledProperty.boolValue, GUILayout.Width(48f));
                var nextUnlocked = EditorGUILayout.Toggle(unlockedProperty.boolValue, GUILayout.Width(48f));

                if (GUILayout.Button("删除", GUILayout.Width(48f)))
                {
                    var removedLevel = levelProperty.objectReferenceValue as LevelDataAsset;
                    levelsProperty.DeleteArrayElementAtIndex(index);
                    if (removedLevel != null)
                    {
                        LevelMenuController.ClearStoredLevelUnlock(removedLevel);
                    }

                    MarkRegistryDirty();
                    return;
                }

                if (nextLevel != levelProperty.objectReferenceValue || nextEnabled != enabledProperty.boolValue || nextUnlocked != unlockedProperty.boolValue)
                {
                    var previousLevel = levelProperty.objectReferenceValue as LevelDataAsset;
                    levelProperty.objectReferenceValue = nextLevel;
                    enabledProperty.boolValue = nextEnabled;
                    unlockedProperty.boolValue = nextUnlocked;
                    if (!nextUnlocked)
                    {
                        LevelMenuController.ClearStoredLevelUnlock(nextLevel != null ? nextLevel : previousLevel);
                    }

                    MarkRegistryDirty();
                }
            }
        }

        private void AddEntry()
        {
            levelsProperty.InsertArrayElementAtIndex(levelsProperty.arraySize);
            var entryProperty = levelsProperty.GetArrayElementAtIndex(levelsProperty.arraySize - 1);
            entryProperty.FindPropertyRelative("level").objectReferenceValue = null;
            entryProperty.FindPropertyRelative("enabled").boolValue = true;
            entryProperty.FindPropertyRelative("unlocked").boolValue = levelsProperty.arraySize == 1;
            MarkRegistryDirty();
        }

        private void SetAllStates(
            bool enabledValue = false,
            bool unlockedValue = false,
            bool keepEnabled = false,
            bool keepUnlocked = false)
        {
            for (var i = 0; i < levelsProperty.arraySize; i++)
            {
                var entryProperty = levelsProperty.GetArrayElementAtIndex(i);
                var levelProperty = entryProperty.FindPropertyRelative("level");
                var enabledProperty = entryProperty.FindPropertyRelative("enabled");
                var unlockedProperty = entryProperty.FindPropertyRelative("unlocked");

                if (!keepEnabled)
                {
                    enabledProperty.boolValue = enabledValue;
                }

                if (!keepUnlocked)
                {
                    unlockedProperty.boolValue = unlockedValue;
                    if (!unlockedValue)
                    {
                        LevelMenuController.ClearStoredLevelUnlock(levelProperty.objectReferenceValue as LevelDataAsset);
                    }
                }
            }

            MarkRegistryDirty();
        }

        private void LoadRegistry()
        {
            var loaded = Resources.Load<LevelSceneBuilderRegistry>(PushBoxzLevelRegistryBuilder.RegistryResourceName);
            if (loaded == null)
            {
                loaded = PushBoxzLevelRegistryBuilder.RebuildRegistryAsset();
            }

            SetRegistry(loaded);
        }

        private void SetRegistry(LevelSceneBuilderRegistry nextRegistry)
        {
            registry = nextRegistry;
            serializedRegistry = registry != null ? new SerializedObject(registry) : null;
            levelsProperty = serializedRegistry != null ? serializedRegistry.FindProperty("levels") : null;
        }

        private void MarkRegistryDirty()
        {
            if (registry == null)
            {
                return;
            }

            serializedRegistry.ApplyModifiedProperties();
            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssets();
        }
    }
}
