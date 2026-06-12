using System;
using System.Collections.Generic;
using PushBoxz.Core;
using UnityEngine;

namespace PushBoxz.Data
{
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

        public bool IsInside(Vector2Int position)
        {
            return position.x >= 0 && position.y >= 0 && position.x < width && position.y < height;
        }
    }

    public enum LevelSolutionStepKind
    {
        Move = 0,
        Push = 1
    }

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
