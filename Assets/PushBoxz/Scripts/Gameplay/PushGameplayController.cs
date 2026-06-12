using System;
using System.Collections.Generic;
using PushBoxz.Core;
using PushBoxz.Data;
using UnityEngine;

namespace PushBoxz.Gameplay
{
    public sealed class PushGameplayController
    {
        private readonly List<BoxRuntimeState> boxes = new List<BoxRuntimeState>();
        private readonly Dictionary<Vector2Int, BoxRuntimeState> boxesByPosition = new Dictionary<Vector2Int, BoxRuntimeState>();
        private readonly HashSet<Vector2Int> goals = new HashSet<Vector2Int>();

        private LevelDataAsset level;
        private PlayerRuntimeState player;
        private int movingBoxCount;

        public LevelDataAsset CurrentLevel
        {
            get { return level; }
        }

        public PlayerRuntimeState Player
        {
            get { return player; }
        }

        public IReadOnlyList<BoxRuntimeState> Boxes
        {
            get { return boxes; }
        }

        public bool HasLevel
        {
            get { return level != null && player != null; }
        }

        public bool BoxMoving
        {
            get { return movingBoxCount > 0; }
        }

        public bool IsBusy
        {
            get { return player != null && (player.IsBusy || BoxMoving); }
        }

        public bool IsCompleted
        {
            get { return HasLevel && boxes.Count > 0 && boxes.Count == goals.Count && AreAllBoxesOnGoals(); }
        }

        public void LoadLevel(LevelDataAsset levelData)
        {
            if (levelData == null)
            {
                throw new ArgumentNullException(nameof(levelData));
            }

            level = levelData;
            player = new PlayerRuntimeState(levelData.playerStart, Direction.Down);
            boxes.Clear();
            boxesByPosition.Clear();
            goals.Clear();
            movingBoxCount = 0;

            if (levelData.cells != null)
            {
                for (var i = 0; i < levelData.cells.Count; i++)
                {
                    var cell = levelData.cells[i];
                    if (cell == null || !cell.hasGoal)
                    {
                        continue;
                    }

                    goals.Add(new Vector2Int(cell.x, cell.y));
                }
            }

            for (var i = 0; i < levelData.boxStarts.Count; i++)
            {
                var position = levelData.boxStarts[i];
                if (boxesByPosition.ContainsKey(position))
                {
                    continue;
                }

                var box = new BoxRuntimeState(i, position);
                boxes.Add(box);
                boxesByPosition.Add(position, box);
            }
        }

        public bool Move(Direction direction)
        {
            EnsureLoaded();

            player.Facing = direction;
            if (IsBusy)
            {
                return false;
            }

            var target = player.Position + DirectionUtility.ToOffset(direction);
            if (!CanPlayerOccupy(target))
            {
                return false;
            }

            player.Position = target;
            player.ActionState = PlayerActionState.Idle;
            return true;
        }

        public bool SetPlayerPosition(Vector2Int position)
        {
            EnsureLoaded();

            if (IsBusy || !CanPlayerOccupy(position))
            {
                return false;
            }

            player.Position = position;
            return true;
        }

        public bool SetPlayerPose(Vector2Int position, Direction facing)
        {
            EnsureLoaded();

            if (IsBusy || !CanPlayerOccupy(position))
            {
                return false;
            }

            player.Position = position;
            player.Facing = facing;
            return true;
        }

        public void SetPlayerFacing(Direction facing)
        {
            EnsureLoaded();
            player.Facing = facing;
        }

        public PushResult TryPush()
        {
            EnsureLoaded();
            return TryPush(DirectionUtility.ToOffset(player.Facing));
        }

        public PushResult TryPush(Vector2Int directionOffset)
        {
            return TryPush(directionOffset, false);
        }

        public PushResult TryPush(Vector2Int directionOffset, bool movePlayerIntoBoxCell)
        {
            EnsureLoaded();

            if (IsBusy)
            {
                return PushResult.Failure(PushFailReason.PlayerBusy);
            }

            directionOffset = new Vector2Int(Mathf.Clamp(directionOffset.x, -1, 1), Mathf.Clamp(directionOffset.y, -1, 1));
            if (directionOffset == Vector2Int.zero)
            {
                return PushResult.Failure(PushFailReason.NoBoxInFacingDirection);
            }

            var boxPosition = player.Position + directionOffset;
            if (!boxesByPosition.TryGetValue(boxPosition, out var box))
            {
                return PushResult.Failure(PushFailReason.NoBoxInFacingDirection);
            }

            var target = boxPosition + directionOffset;
            if (!level.IsInside(target))
            {
                return PushResult.Failure(PushFailReason.TargetOutOfBounds);
            }

            if (!CanBoxOccupy(target))
            {
                return PushResult.Failure(PushFailReason.TargetBlocked);
            }

            boxesByPosition.Remove(boxPosition);
            boxesByPosition.Add(target, box);
            box.Position = target;
            if (movePlayerIntoBoxCell)
            {
                player.Position = boxPosition;
            }

            player.ActionState = PlayerActionState.Pushing;

            return PushResult.Success(box, boxPosition, target);
        }

        public void BeginPushCooldown()
        {
            EnsureLoaded();

            player.ActionState = PlayerActionState.Cooldown;
        }

        public void BeginPushCooldown(BoxRuntimeState box)
        {
            EnsureLoaded();
            BeginBoxMove(box);
            player.ActionState = PlayerActionState.Cooldown;
        }

        public void CompletePushCooldown()
        {
            EnsureLoaded();

            if (!BoxMoving)
            {
                player.ActionState = PlayerActionState.Idle;
            }
        }

        public void BeginBoxMove(BoxRuntimeState box)
        {
            EnsureLoaded();
            ValidateBox(box);

            if (box.IsMoving)
            {
                return;
            }

            box.IsMoving = true;
            movingBoxCount++;
        }

        public void CompleteBoxMove(BoxRuntimeState box)
        {
            EnsureLoaded();
            ValidateBox(box);

            if (!box.IsMoving)
            {
                return;
            }

            box.IsMoving = false;
            movingBoxCount = Mathf.Max(0, movingBoxCount - 1);

            if (!BoxMoving && player.ActionState == PlayerActionState.Cooldown)
            {
                player.ActionState = PlayerActionState.Idle;
            }
        }

        public void CompletePlayerAction()
        {
            EnsureLoaded();

            if (!BoxMoving)
            {
                player.ActionState = PlayerActionState.Idle;
            }
        }

        public bool TryGetBoxAt(Vector2Int position, out BoxRuntimeState box)
        {
            EnsureLoaded();
            return boxesByPosition.TryGetValue(position, out box);
        }

        public bool IsWalkable(Vector2Int position)
        {
            EnsureLoaded();
            return IsFloorLike(position);
        }

        public bool IsOccupied(Vector2Int position)
        {
            EnsureLoaded();
            return boxesByPosition.ContainsKey(position);
        }

        private bool CanPlayerOccupy(Vector2Int position)
        {
            return IsFloorLike(position) && !boxesByPosition.ContainsKey(position);
        }

        private bool CanBoxOccupy(Vector2Int position)
        {
            return IsFloorLike(position) && !boxesByPosition.ContainsKey(position);
        }

        private bool IsFloorLike(Vector2Int position)
        {
            return level.IsInside(position) && level.GetBaseTile(position.x, position.y) == BaseTileType.Floor;
        }

        private bool AreAllBoxesOnGoals()
        {
            for (var i = 0; i < boxes.Count; i++)
            {
                if (!goals.Contains(boxes[i].Position))
                {
                    return false;
                }
            }

            return true;
        }

        private void EnsureLoaded()
        {
            if (!HasLevel)
            {
                throw new InvalidOperationException("LoadLevel must be called before using PushGameplayController.");
            }
        }

        private void ValidateBox(BoxRuntimeState box)
        {
            if (box == null || !boxes.Contains(box))
            {
                throw new ArgumentException("Box does not belong to this controller.", nameof(box));
            }
        }
    }
}
