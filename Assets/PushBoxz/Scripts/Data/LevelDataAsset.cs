using System;
using System.Collections.Generic;
using PushBoxz.Core;
using UnityEngine;

namespace PushBoxz.Data
{
    /// <summary>
    /// Authoring-time and runtime level definition.
    /// It stores immutable starting data only; runtime positions are tracked by gameplay/session classes.
    /// </summary>
    [CreateAssetMenu(menuName = "PushBoxz/Level Data", fileName = "LevelData")]
    public class LevelDataAsset : ScriptableObject
    {
        public string version = "0.1";
        public string levelId = "level_001";
        public int width = 5;
        public int height = 6;
        public Vector2Int playerStart = Vector2Int.zero;
        public List<TileCell> cells = new List<TileCell>();
        public List<Vector2Int> boxStarts = new List<Vector2Int>();
        public List<LevelSolutionStep> solutionSteps = new List<LevelSolutionStep>();

        /// <summary>
        /// Returns the terrain type for a cell. Missing cells are treated as floor for backward compatibility.
        /// </summary>
        public BaseTileType GetBaseTile(int x, int y)
        {
            for (var i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                if (cell.x == x && cell.y == y)
                {
                    return cell.baseType;
                }
            }

            return BaseTileType.Floor;
        }

        /// <summary>
        /// Returns whether the goal overlay is present at the given cell.
        /// </summary>
        public bool HasGoal(int x, int y)
        {
            for (var i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                if (cell.x == x && cell.y == y)
                {
                    return cell.hasGoal;
                }
            }

            return false;
        }

        /// <summary>
        /// Updates or creates a terrain cell.
        /// </summary>
        public void SetBaseTile(int x, int y, BaseTileType baseType)
        {
            for (var i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                if (cell.x == x && cell.y == y)
                {
                    cell.baseType = baseType;
                    return;
                }
            }

            cells.Add(new TileCell(x, y, baseType));
        }

        /// <summary>
        /// Updates or creates the goal overlay for a cell.
        /// </summary>
        public void SetGoal(int x, int y, bool hasGoal)
        {
            for (var i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                if (cell.x == x && cell.y == y)
                {
                    cell.hasGoal = hasGoal;
                    return;
                }
            }

            cells.Add(new TileCell(x, y, BaseTileType.Floor, hasGoal));
        }

        /// <summary>
        /// Counts goal overlays in the level; used by validation and completion checks.
        /// </summary>
        public int GetGoalCount()
        {
            var count = 0;
            for (var i = 0; i < cells.Count; i++)
            {
                if (cells[i] != null && cells[i].hasGoal)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Bounds check in grid coordinates.
        /// </summary>
        public bool IsInside(Vector2Int position)
        {
            return position.x >= 0 && position.y >= 0 && position.x < width && position.y < height;
        }
    }

    /// <summary>
    /// Step types saved from generated/known solutions for playback or documentation.
    /// </summary>
    public enum LevelSolutionStepKind
    {
        Move = 0,
        Push = 1
    }

    /// <summary>
    /// Serializable solution step. Push steps include both player and box movement data.
    /// </summary>
    [Serializable]
    public class LevelSolutionStep
    {
        public LevelSolutionStepKind kind;
        public int boxId = -1;
        public Direction direction;
        public Vector2Int playerFrom;
        public Vector2Int playerTo;
        public Vector2Int boxFrom;
        public Vector2Int boxTo;
    }
}
