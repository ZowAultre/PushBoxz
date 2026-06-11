using UnityEngine;

namespace PushBoxz.Core
{
    public static class DirectionUtility
    {
        public static Vector2Int ToOffset(Direction direction)
        {
            switch (direction)
            {
                case Direction.Up:
                    return Vector2Int.up;
                case Direction.Down:
                    return Vector2Int.down;
                case Direction.Left:
                    return Vector2Int.left;
                case Direction.Right:
                    return Vector2Int.right;
                default:
                    return Vector2Int.zero;
            }
        }

        public static Quaternion ToRotation(Direction direction)
        {
            switch (direction)
            {
                case Direction.Up:
                    return Quaternion.LookRotation(Vector3.forward, Vector3.up);
                case Direction.Down:
                    return Quaternion.LookRotation(Vector3.back, Vector3.up);
                case Direction.Left:
                    return Quaternion.LookRotation(Vector3.left, Vector3.up);
                case Direction.Right:
                    return Quaternion.LookRotation(Vector3.right, Vector3.up);
                default:
                    return Quaternion.identity;
            }
        }
    }
}
