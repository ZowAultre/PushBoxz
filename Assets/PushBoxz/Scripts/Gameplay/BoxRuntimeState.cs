using UnityEngine;

namespace PushBoxz.Gameplay
{
    public sealed class BoxRuntimeState
    {
        public int Id { get; }
        public Vector2Int StartPosition { get; }
        public Vector2Int Position { get; internal set; }
        public bool IsMoving { get; internal set; }

        public BoxRuntimeState(int id, Vector2Int startPosition)
        {
            Id = id;
            StartPosition = startPosition;
            Position = startPosition;
            IsMoving = false;
        }
    }
}
