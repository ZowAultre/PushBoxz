using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PushBoxz.Data
{
    /// <summary>
    /// Runtime persistence for player-created levels.
    /// Official levels live as project assets; custom levels are stored as JSON in persistentDataPath.
    /// </summary>
    public static class CustomLevelStorage
    {
        private const string FolderName = "CustomLevels";
        private const string FileName = "levels.json";
        private const string LevelIdPrefix = "C";

        [Serializable]
        private class CustomLevelCollection
        {
            public List<CustomLevelRecord> levels = new List<CustomLevelRecord>();
        }

        [Serializable]
        private class CustomLevelRecord
        {
            public string levelId;
            public int width;
            public int height;
            public int playerX;
            public int playerY;
            public List<TileCell> cells = new List<TileCell>();
            public List<Vector2Int> boxStarts = new List<Vector2Int>();
        }

        public static List<LevelDataAsset> LoadLevels()
        {
            var records = LoadCollection().levels;
            var levels = new List<LevelDataAsset>(records.Count);
            for (var i = 0; i < records.Count; i++)
            {
                var level = ToLevelDataAsset(records[i]);
                if (level != null)
                {
                    levels.Add(level);
                }
            }

            return levels;
        }

        /// <summary>
        /// Saves or overwrites a custom level by levelId.
        /// </summary>
        public static LevelDataAsset SaveLevel(LevelDataAsset source)
        {
            if (source == null)
            {
                return null;
            }

            var collection = LoadCollection();
            var record = ToRecord(source);
            if (string.IsNullOrWhiteSpace(record.levelId))
            {
                record.levelId = CreateNextLevelId(collection);
            }

            var existingIndex = collection.levels.FindIndex(level => level != null && level.levelId == record.levelId);
            if (existingIndex >= 0)
            {
                collection.levels[existingIndex] = record;
            }
            else
            {
                collection.levels.Add(record);
            }

            SaveCollection(collection);
            return ToLevelDataAsset(record);
        }

        /// <summary>
        /// Removes a saved custom level. Returns false when the id does not exist.
        /// </summary>
        public static bool DeleteLevel(string levelId)
        {
            if (string.IsNullOrWhiteSpace(levelId))
            {
                return false;
            }

            var collection = LoadCollection();
            var removed = collection.levels.RemoveAll(level => level != null && level.levelId == levelId) > 0;
            if (removed)
            {
                SaveCollection(collection);
            }

            return removed;
        }

        /// <summary>
        /// Creates a transient LevelDataAsset instance from runtime/custom data.
        /// The returned asset is not added to the Unity AssetDatabase.
        /// </summary>
        public static LevelDataAsset CreateRuntimeLevel(
            string levelId,
            int width,
            int height,
            Vector2Int playerStart,
            IEnumerable<TileCell> cells,
            IEnumerable<Vector2Int> boxStarts)
        {
            var level = ScriptableObject.CreateInstance<LevelDataAsset>();
            level.version = "custom.1";
            level.levelId = levelId;
            level.width = width;
            level.height = height;
            level.playerStart = playerStart;
            level.cells = CopyCells(cells);
            level.boxStarts = boxStarts != null ? new List<Vector2Int>(boxStarts) : new List<Vector2Int>();
            level.solutionSteps = new List<LevelSolutionStep>();
            return level;
        }

        public static bool ValidatePlayable(LevelDataAsset level, out string message)
        {
            if (level == null)
            {
                message = "Level is empty.";
                return false;
            }

            if (level.width <= 0 || level.height <= 0)
            {
                message = "Width and height must be greater than 0.";
                return false;
            }

            if (!level.IsInside(level.playerStart) || level.GetBaseTile(level.playerStart.x, level.playerStart.y) != BaseTileType.Floor)
            {
                message = "Player must be placed on a floor tile.";
                return false;
            }

            if (level.boxStarts == null || level.boxStarts.Count == 0)
            {
                message = "Place at least one box.";
                return false;
            }

            var goalCount = level.GetGoalCount();
            if (goalCount == 0)
            {
                message = "Place at least one goal.";
                return false;
            }

            if (goalCount != level.boxStarts.Count)
            {
                message = "Box count must match goal count.";
                return false;
            }

            var occupiedBoxes = new HashSet<Vector2Int>();
            for (var i = 0; i < level.boxStarts.Count; i++)
            {
                var box = level.boxStarts[i];
                if (!level.IsInside(box) || level.GetBaseTile(box.x, box.y) != BaseTileType.Floor)
                {
                    message = "Every box must be placed on a floor tile.";
                    return false;
                }

                if (box == level.playerStart)
                {
                    message = "Player and box cannot share a tile.";
                    return false;
                }

                if (!occupiedBoxes.Add(box))
                {
                    message = "Two boxes cannot share a tile.";
                    return false;
                }
            }

            message = "Saved.";
            return true;
        }

        private static CustomLevelRecord ToRecord(LevelDataAsset source)
        {
            return new CustomLevelRecord
            {
                levelId = string.IsNullOrWhiteSpace(source.levelId) ? null : source.levelId,
                width = source.width,
                height = source.height,
                playerX = source.playerStart.x,
                playerY = source.playerStart.y,
                cells = CopyCells(source.cells),
                boxStarts = source.boxStarts != null ? new List<Vector2Int>(source.boxStarts) : new List<Vector2Int>()
            };
        }

        private static LevelDataAsset ToLevelDataAsset(CustomLevelRecord record)
        {
            if (record == null || record.width <= 0 || record.height <= 0)
            {
                return null;
            }

            return CreateRuntimeLevel(
                record.levelId,
                record.width,
                record.height,
                new Vector2Int(record.playerX, record.playerY),
                record.cells,
                record.boxStarts);
        }

        private static List<TileCell> CopyCells(IEnumerable<TileCell> cells)
        {
            var result = new List<TileCell>();
            if (cells == null)
            {
                return result;
            }

            foreach (var cell in cells)
            {
                if (cell != null)
                {
                    result.Add(new TileCell(cell.x, cell.y, cell.baseType, cell.hasGoal));
                }
            }

            return result;
        }

        private static string CreateNextLevelId(CustomLevelCollection collection)
        {
            var usedIds = new HashSet<string>();
            if (collection != null && collection.levels != null)
            {
                for (var i = 0; i < collection.levels.Count; i++)
                {
                    var level = collection.levels[i];
                    if (level != null && !string.IsNullOrWhiteSpace(level.levelId))
                    {
                        usedIds.Add(level.levelId);
                    }
                }
            }

            var index = 1;
            string id;
            do
            {
                id = LevelIdPrefix + index.ToString("000");
                index++;
            }
            while (usedIds.Contains(id));

            return id;
        }

        private static CustomLevelCollection LoadCollection()
        {
            var path = GetFilePath();
            if (!File.Exists(path))
            {
                return new CustomLevelCollection();
            }

            try
            {
                var json = File.ReadAllText(path);
                var collection = JsonUtility.FromJson<CustomLevelCollection>(json);
                return collection != null && collection.levels != null ? collection : new CustomLevelCollection();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Failed to load custom levels: " + exception.Message);
                return new CustomLevelCollection();
            }
        }

        private static void SaveCollection(CustomLevelCollection collection)
        {
            var folder = GetFolderPath();
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            var json = JsonUtility.ToJson(collection, true);
            File.WriteAllText(GetFilePath(), json);
        }

        private static string GetFolderPath()
        {
            return Path.Combine(Application.persistentDataPath, FolderName);
        }

        private static string GetFilePath()
        {
            return Path.Combine(GetFolderPath(), FileName);
        }
    }
}
