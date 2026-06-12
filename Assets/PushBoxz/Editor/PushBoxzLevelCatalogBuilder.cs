using System.Collections.Generic;
using System.Linq;
using PushBoxz.Data;
using PushBoxz.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PushBoxz.Editor
{
    public static class PushBoxzLevelCatalogBuilder
    {
        private const string LevelsFolder = "Assets/PushBoxz/Levels";
        private const string ResourcesFolder = "Assets/PushBoxz/Resources";
        private const string CatalogPath = ResourcesFolder + "/LevelCatalog.asset";
        private const string LevelMenuName = "PushBoxz Level Menu";

        [MenuItem("Tools/PushBoxz/Rebuild Level Catalog")]
        public static void RebuildLevelCatalogMenu()
        {
            var catalog = RebuildCatalogAsset();
            Selection.activeObject = catalog;
            EditorUtility.DisplayDialog("PushBoxz", "已重建关卡目录：\n" + CatalogPath, "OK");
        }

        [MenuItem("Tools/PushBoxz/Create Level Menu In Scene")]
        public static void CreateLevelMenuInScene()
        {
            var catalog = RebuildCatalogAsset();
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

            controller.Catalog = catalog;
            EditorUtility.SetDirty(menuObject);
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Selection.activeGameObject = menuObject;
            EditorUtility.DisplayDialog("PushBoxz", "已在当前场景创建关卡菜单。进入 Play Mode 后可从菜单选择关卡。", "OK");
        }

        public static LevelCatalog RebuildCatalogAsset()
        {
            EnsureLevelsFolder();

            var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<LevelCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var levels = new List<LevelDataAsset>();
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
                    Debug.LogWarning("PushBoxz skipped duplicate levelId in catalog: " + id + " (" + path + ")");
                    continue;
                }

                levels.Add(level);
            }

            catalog.levels = levels.OrderBy(level => level.levelId).ToList();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return catalog;
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
