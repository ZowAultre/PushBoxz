using System;

namespace PushBoxz.Data
{
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
