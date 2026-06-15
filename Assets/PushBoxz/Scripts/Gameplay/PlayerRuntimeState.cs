using PushBoxz.Core;
using UnityEngine;

namespace PushBoxz.Gameplay
{
    /// <summary>
    /// Runtime-only player state for grid position, facing, and temporary action lockout.
    /// </summary>
    public sealed class PlayerRuntimeState
    {
        public Vector2Int Position { get; internal set; }
        public Direction Facing { get; internal set; }
        public PlayerActionState ActionState { get; internal set; }

        public bool IsBusy
        {
            get
            {
                return ActionState == PlayerActionState.Pushing
                    || ActionState == PlayerActionState.Cooldown;
            }
        }

        public PlayerRuntimeState(Vector2Int position, Direction facing)
        {
            Position = position;
            Facing = facing;
            ActionState = PlayerActionState.Idle;
        }
    }
}
