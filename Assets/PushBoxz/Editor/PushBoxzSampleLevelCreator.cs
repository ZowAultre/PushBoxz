using System.Collections.Generic;
using PushBoxz.Data;
using UnityEditor;
using UnityEngine;

namespace PushBoxz.Editor
{
    /// <summary>
    /// Small development utility for creating a known-good sample level asset.
    /// Kept separate from the main editor so test content generation stays optional.
    /// </summary>
    public static class PushBoxzSampleLevelCreator
    {
        private const string LevelsFolder = "Assets/PushBoxz/Levels";

        public static void CreateSamplePushLevel()
        {
            EnsureLevelsFolder();

            var path = AssetDatabase.GenerateUniqueAssetPath(LevelsFolder + "/sample_push_level.asset");
            var level = ScriptableObject.CreateInstance<LevelDataAsset>();
            level.version = "0.1";
            level.levelId = "sample_push_level";
            level.width = 5;
            level.height = 6;
            level.playerStart = new Vector2Int(2, 1);
            level.cells = new List<TileCell>();

            for (var y = 0; y < level.height; y++)
            {
                for (var x = 0; x < level.width; x++)
                {
                    var isBoundary = x == 0 || y == 0 || x == level.width - 1 || y == level.height - 1;
                    level.cells.Add(new TileCell(x, y, isBoundary ? BaseTileType.Wall : BaseTileType.Floor));
                }
            }

            level.boxStarts = new List<Vector2Int>
            {
                new Vector2Int(2, 2),
                new Vector2Int(3, 3)
            };
            level.SetGoal(2, 4, true);
            level.SetGoal(3, 4, true);

            AssetDatabase.CreateAsset(level, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = level;
            EditorUtility.DisplayDialog("PushBoxz", "Created sample push level:\n" + path, "OK");
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
        }
    }
}
