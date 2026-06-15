using System;

namespace PushBoxz.Data
{
    /// <summary>
    /// Serializable terrain cell used by LevelDataAsset.
    /// Goals are stored as an overlay so boxes can stand on goals at runtime without mutating source data.
    /// </summary>
    [Serializable]
    public class TileCell
    {
        public int x;
        public int y;
        public BaseTileType baseType = BaseTileType.Floor;
        public bool hasGoal;

        public TileCell()
        {
        }

        public TileCell(int x, int y, BaseTileType baseType, bool hasGoal = false)
        {
            this.x = x;
            this.y = y;
            this.baseType = baseType;
            this.hasGoal = hasGoal;
        }
    }
}
