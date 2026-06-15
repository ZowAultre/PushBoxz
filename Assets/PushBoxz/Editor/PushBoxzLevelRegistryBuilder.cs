using System.Collections.Generic;
using System.Linq;
using PushBoxz.Data;
using PushBoxz.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PushBoxz.Editor
{
    public static class PushBoxzLevelRegistryBuilder
    {
        private const string LevelsFolder = "Assets/PushBoxz/Levels";
        private const string ResourcesFolder = "Assets/PushBoxz/Resources";
        public const string RegistryResourceName = "LevelSceneBuilderRegistry";
        private const string RegistryPath = ResourcesFolder + "/" + RegistryResourceName + ".asset";
        private const string LevelMenuName = "PushBoxz Level Menu";

        [MenuItem("Tools/PushBoxz/Rebuild Level Registry")]
        public static void RebuildLevelRegistryMenu()
        {
            var registry = RebuildRegistryAsset();
            Selection.activeObject = registry;
            EditorUtility.DisplayDialog("PushBoxz", "已重建关卡记录：\n" + RegistryPath, "OK");
        }

        public static void CreateLevelMenuInScene()
        {
            var registry = RebuildRegistryAsset();
            var menuObject = GameObject.Find(LevelMenuName);
            if (menuObject == null)
            {
                menuObject = new GameObject(LevelMenuName);
            }

            var controller = menuObject.GetComponent<LevelMenuController>();
            if (controller == null)
            {
                controller = menuObject.AddComponent<LevelMenuController>();
            }

            controller.Registry = registry;
            EditorUtility.SetDirty(menuObject);
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Selection.activeGameObject = menuObject;
            EditorUtility.DisplayDialog("PushBoxz", "已在当前场景创建关卡菜单。进入 Play Mode 后可以从菜单选择关卡。", "OK");
        }

        public static LevelSceneBuilderRegistry RebuildRegistryAsset()
        {
            EnsureLevelsFolder();

            var registry = AssetDatabase.LoadAssetAtPath<LevelSceneBuilderRegistry>(RegistryPath);
            if (registry == null)
            {
                registry = ScriptableObject.CreateInstance<LevelSceneBuilderRegistry>();
                AssetDatabase.CreateAsset(registry, RegistryPath);
            }

            var previousEntries = registry.levels != null
                ? registry.levels
                    .Where(entry => entry != null && entry.level != null)
                    .GroupBy(entry => entry.level)
                    .ToDictionary(group => group.Key, group => group.First())
                : new Dictionary<LevelDataAsset, LevelSceneBuilderRegistryEntry>();
            var playableLevels = new List<LevelDataAsset>();
            var usedIds = new HashSet<string>();
            var guids = AssetDatabase.FindAssets("t:LevelDataAsset", new[] { LevelsFolder });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var level = AssetDatabase.LoadAssetAtPath<LevelDataAsset>(path);
                if (!IsPlayableLevel(level))
                {
                    continue;
                }

                var id = level.levelId.Trim();
                if (!usedIds.Add(id))
                {
                    Debug.LogWarning("PushBoxz skipped duplicate levelId in registry: " + id + " (" + path + ")");
                    continue;
                }

                playableLevels.Add(level);
            }

            var entries = new List<LevelSceneBuilderRegistryEntry>();
            var addedLevels = new HashSet<LevelDataAsset>();
            if (registry.levels != null)
            {
                for (var i = 0; i < registry.levels.Count; i++)
                {
                    var previous = registry.levels[i];
                    if (previous == null || previous.level == null || !playableLevels.Contains(previous.level) || !addedLevels.Add(previous.level))
                    {
                        continue;
                    }

                    entries.Add(new LevelSceneBuilderRegistryEntry
                    {
                        level = previous.level,
                        enabled = previous.enabled,
                        unlocked = previous.unlocked
                    });
                }
            }

            var newLevels = playableLevels
                .Where(level => level != null && !addedLevels.Contains(level))
                .OrderBy(level => level.levelId);
            foreach (var level in newLevels)
            {
                addedLevels.Add(level);
                if (previousEntries.TryGetValue(level, out var previous))
                {
                    entries.Add(new LevelSceneBuilderRegistryEntry
                    {
                        level = level,
                        enabled = previous.enabled,
                        unlocked = previous.unlocked
                    });
                    continue;
                }

                entries.Add(new LevelSceneBuilderRegistryEntry
                {
                    level = level,
                    enabled = true,
                    unlocked = entries.Count == 0
                });
            }

            registry.levels = entries;
            if (registry.levels.Count > 0 && !registry.levels.Any(entry => entry.unlocked))
            {
                registry.levels[0].unlocked = true;
            }

            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return registry;
        }

        public static bool AddLevelToRegistry(LevelDataAsset level)
        {
            if (level == null)
            {
                return false;
            }

            EnsureLevelsFolder();
            var registry = AssetDatabase.LoadAssetAtPath<LevelSceneBuilderRegistry>(RegistryPath);
            if (registry == null)
            {
                registry = ScriptableObject.CreateInstance<LevelSceneBuilderRegistry>();
                AssetDatabase.CreateAsset(registry, RegistryPath);
            }

            if (registry.levels.Any(entry => entry != null && entry.level == level))
            {
                return false;
            }

            registry.levels.Add(new LevelSceneBuilderRegistryEntry
            {
                level = level,
                enabled = true,
                unlocked = registry.levels.Count == 0
            });

            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return true;
        }

        private static bool IsPlayableLevel(LevelDataAsset level)
        {
            if (level == null || string.IsNullOrWhiteSpace(level.levelId) || level.width <= 0 || level.height <= 0)
            {
                return false;
            }

            if (level.boxStarts == null || level.boxStarts.Count == 0 || level.GetGoalCount() != level.boxStarts.Count)
            {
                return false;
            }

            if (!level.IsInside(level.playerStart) || level.GetBaseTile(level.playerStart.x, level.playerStart.y) != BaseTileType.Floor)
            {
                return false;
            }

            var occupiedBoxes = new HashSet<Vector2Int>();
            for (var i = 0; i < level.boxStarts.Count; i++)
            {
                var box = level.boxStarts[i];
                if (!level.IsInside(box) || level.GetBaseTile(box.x, box.y) != BaseTileType.Floor || box == level.playerStart)
                {
                    return false;
                }

                if (!occupiedBoxes.Add(box))
                {
                    return false;
                }
            }

            return true;
        }

        private static void EnsureLevelsFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/PushBoxz"))
            {
                AssetDatabase.CreateFolder("Assets", "PushBoxz");
            }

            if (!AssetDatabase.IsValidFolder(LevelsFolder))
            {
                AssetDatabase.CreateFolder("Assets/PushBoxz", "Levels");
            }

            if (!AssetDatabase.IsValidFolder(ResourcesFolder))
            {
                AssetDatabase.CreateFolder("Assets/PushBoxz", "Resources");
            }
        }
    }
}
