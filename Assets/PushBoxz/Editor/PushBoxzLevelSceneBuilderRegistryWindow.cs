using PushBoxz.Data;
using PushBoxz.Presentation;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace PushBoxz.Editor
{
    public class PushBoxzLevelSceneBuilderRegistryWindow : EditorWindow
    {
        private LevelSceneBuilderRegistry registry;
        private SerializedObject serializedRegistry;
        private SerializedProperty levelsProperty;
        private ReorderableList levelList;
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
            if (levelList == null)
            {
                CreateReorderableLevelList();
            }

            if (levelList == null)
            {
                return;
            }

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            levelList.DoLayoutList();
            EditorGUILayout.EndScrollView();
        }

        private void CreateReorderableLevelList()
        {
            if (levelsProperty == null)
            {
                levelList = null;
                return;
            }

            levelList = new ReorderableList(serializedRegistry, levelsProperty, true, true, true, true);
            levelList.drawHeaderCallback = DrawLevelListHeader;
            levelList.drawElementCallback = DrawEntryElement;
            levelList.elementHeight = EditorGUIUtility.singleLineHeight + 8f;
            levelList.onAddCallback = _ => AddEntry();
            levelList.onRemoveCallback = RemoveSelectedEntry;
            levelList.onReorderCallback = _ => MarkRegistryDirty();
        }

        private void DrawLevelListHeader(Rect rect)
        {
            const float toggleWidth = 54f;
            const float deleteWidth = 56f;
            var levelWidth = Mathf.Max(80f, rect.width - toggleWidth * 2f - deleteWidth - 20f);

            EditorGUI.LabelField(new Rect(rect.x + 4f, rect.y, levelWidth, rect.height), "Level Asset", EditorStyles.boldLabel);
            EditorGUI.LabelField(new Rect(rect.x + levelWidth + 8f, rect.y, toggleWidth, rect.height), "Enabled", EditorStyles.boldLabel);
            EditorGUI.LabelField(new Rect(rect.x + levelWidth + toggleWidth + 8f, rect.y, toggleWidth, rect.height), "Unlocked", EditorStyles.boldLabel);
        }

        private void DrawEntryElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            var entryProperty = levelsProperty.GetArrayElementAtIndex(index);
            var levelProperty = entryProperty.FindPropertyRelative("level");
            var enabledProperty = entryProperty.FindPropertyRelative("enabled");
            var unlockedProperty = entryProperty.FindPropertyRelative("unlocked");

            const float toggleWidth = 54f;
            const float deleteWidth = 56f;
            var lineHeight = EditorGUIUtility.singleLineHeight;
            rect.y += 4f;
            var levelWidth = Mathf.Max(80f, rect.width - toggleWidth * 2f - deleteWidth - 20f);
            var levelRect = new Rect(rect.x + 4f, rect.y, levelWidth, lineHeight);
            var enabledRect = new Rect(levelRect.xMax + 6f, rect.y, toggleWidth, lineHeight);
            var unlockedRect = new Rect(enabledRect.xMax, rect.y, toggleWidth, lineHeight);
            var deleteRect = new Rect(unlockedRect.xMax + 6f, rect.y, deleteWidth, lineHeight);

            EditorGUI.BeginChangeCheck();
            var nextLevel = (LevelDataAsset)EditorGUI.ObjectField(levelRect, levelProperty.objectReferenceValue, typeof(LevelDataAsset), false);
            var nextEnabled = EditorGUI.Toggle(enabledRect, enabledProperty.boolValue);
            var nextUnlocked = EditorGUI.Toggle(unlockedRect, unlockedProperty.boolValue);
            if (EditorGUI.EndChangeCheck())
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

            if (GUI.Button(deleteRect, "Delete"))
            {
                RemoveEntryAt(index);
            }
        }

        private void RemoveSelectedEntry(ReorderableList list)
        {
            if (list == null || list.index < 0 || list.index >= levelsProperty.arraySize)
            {
                return;
            }

            RemoveEntryAt(list.index);
        }

        private void RemoveEntryAt(int index)
        {
            var entryProperty = levelsProperty.GetArrayElementAtIndex(index);
            var levelProperty = entryProperty.FindPropertyRelative("level");
            var removedLevel = levelProperty.objectReferenceValue as LevelDataAsset;
            levelsProperty.DeleteArrayElementAtIndex(index);
            if (removedLevel != null)
            {
                LevelMenuController.ClearStoredLevelUnlock(removedLevel);
            }

            MarkRegistryDirty();
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
            CreateReorderableLevelList();
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
