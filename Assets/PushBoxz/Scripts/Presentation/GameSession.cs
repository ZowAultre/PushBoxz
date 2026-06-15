using System.Collections;
using System.Collections.Generic;
using PushBoxz.Core;
using PushBoxz.Data;
using PushBoxz.Gameplay;
using UnityEngine;

namespace PushBoxz.Presentation
{
    /// <summary>
    /// Runtime game orchestrator.
    /// It bridges pure gameplay rules, scene objects, input commands, audio, undo, and completion flow.
    /// </summary>
    public class GameSession : MonoBehaviour
    {
        private const float PushDuration = 0.12f;
        private const float PushCooldown = 0.18f;

        [SerializeField] private LevelDataAsset levelData;
        [SerializeField] private LevelSceneBuilder sceneBuilder;
        [SerializeField] private bool buildOnStart = true;
        [SerializeField] private bool useLocalFallbackWhenGameplayMissing = true;
        [SerializeField] private PlayerMovementMode movementMode = PlayerMovementMode.GridStep;
        [SerializeField] private float continuousMoveSpeed = 4f;
        [SerializeField] private float playerRadius = 0.28f;
        [SerializeField] private float maxPushDistance = 1.6f;

        private readonly PushGameplayController gameplay = new PushGameplayController();
        private readonly List<SessionSnapshot> undoHistory = new List<SessionSnapshot>();
        private Vector2Int playerGridPosition;
        private Vector3 playerWorldPosition;
        private Direction facingDirection = Direction.Up;
        private Vector3 facingWorldDirection = Vector3.forward;
        private BoxView focusedBox;
        private bool pushBusy;
        private float nextPushAllowedTime;
        private AudioSource audioSource;

        /// <summary>
        /// Full gameplay snapshot used by undo. It captures logical state and continuous-position state.
        /// </summary>
        private struct SessionSnapshot
        {
            public Vector2Int playerGridPosition;
            public Vector3 playerWorldPosition;
            public Direction facingDirection;
            public Vector3 facingWorldDirection;
            public int stepCount;
            public bool isCompleted;
            public List<Vector2Int> boxPositions;
        }

        public LevelDataAsset LevelData
        {
            get { return levelData; }
            set { levelData = value; }
        }

        public LevelSceneBuilder SceneBuilder
        {
            get { return sceneBuilder; }
        }

        public int StepCount { get; private set; }

        public Vector2Int PlayerGridPosition
        {
            get { return playerGridPosition; }
        }

        public Direction FacingDirection
        {
            get { return facingDirection; }
        }

        public bool IsCompleted { get; private set; }

        public PlayerMovementMode MovementMode
        {
            get { return movementMode; }
            set { SetMovementMode(value); }
        }

        public bool BuildOnStart
        {
            get { return buildOnStart; }
            set { buildOnStart = value; }
        }

        private void Awake()
        {
            EnsureSceneBuilder();
        }

        private void Start()
        {
            if (buildOnStart)
            {
                RestartLevel();
            }
        }

        public void RestartLevel()
        {
            StopAllCoroutines();
            pushBusy = false;
            nextPushAllowedTime = 0f;
            StepCount = 0;
            IsCompleted = false;
            undoHistory.Clear();

            EnsureSceneBuilder();
            if (sceneBuilder != null)
            {
                sceneBuilder.Build(levelData);
            }

            playerGridPosition = levelData != null ? levelData.playerStart : Vector2Int.zero;
            playerWorldPosition = sceneBuilder != null
                ? sceneBuilder.GetGridWorldPosition(playerGridPosition) + Vector3.up * 0.55f
                : new Vector3(playerGridPosition.x, 0.55f, playerGridPosition.y);
            facingDirection = Direction.Up;
            facingWorldDirection = Vector3.forward;
            SyncPlayerTransform();
            LoadGameplayLevel();
            playerWorldPosition = sceneBuilder != null
                ? sceneBuilder.GetGridWorldPosition(playerGridPosition) + Vector3.up * 0.55f
                : playerWorldPosition;
            SyncPlayerTransform();
            RefreshFocusedBox();
            RefreshGoalVisuals();
        }

        public bool TryMove(Direction direction)
        {
            if (pushBusy || IsCompleted)
            {
                return false;
            }

            facingDirection = direction;
            facingWorldDirection = WorldDirectionFromOffset(DirectionUtility.ToOffset(direction));
            var moveResult = false;
            var snapshot = CaptureSnapshot();
            if (gameplay.HasLevel)
            {
                moveResult = gameplay.Move(direction);
            }
            else if (useLocalFallbackWhenGameplayMissing)
            {
                moveResult = TryLocalMove(direction);
            }

            SyncPlayerFacing();

            if (!moveResult)
            {
                if (movementMode == PlayerMovementMode.GridStep && TryPushFromGridMove(direction))
                {
                    return true;
                }

                RefreshFocusedBox();
                return false;
            }

            playerGridPosition = gameplay.HasLevel ? gameplay.Player.Position : playerGridPosition + DirectionUtility.ToOffset(direction);
            facingDirection = gameplay.HasLevel ? gameplay.Player.Facing : direction;
            facingWorldDirection = WorldDirectionFromOffset(DirectionUtility.ToOffset(facingDirection));
            undoHistory.Add(snapshot);
            StepCount++;
            SyncPlayerTransform();
            RefreshFocusedBox();
            if (movementMode == PlayerMovementMode.GridStep)
            {
                PlayOneShot(sceneBuilder != null ? sceneBuilder.MoveClip : null);
            }

            if (gameplay.HasLevel)
            {
                gameplay.CompletePlayerAction();
            }

            return true;
        }

        public bool TryContinuousMove(Vector2 input, float deltaTime)
        {
            if (movementMode != PlayerMovementMode.Continuous || pushBusy || IsCompleted || input.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            EnsureSceneBuilder();
            var clampedInput = Vector2.ClampMagnitude(input, 1f);
            var worldDirection = new Vector3(clampedInput.x, 0f, clampedInput.y).normalized;
            facingWorldDirection = worldDirection;
            facingDirection = DirectionFromInput(clampedInput);
            SyncPlayerFacing();

            // Continuous movement checks collision against nearby grid cells, then syncs the nearest logical cell.
            var nextWorld = playerWorldPosition + worldDirection * continuousMoveSpeed * deltaTime;
            if (!CanOccupyWorldPosition(nextWorld))
            {
                RefreshFocusedBox();
                return false;
            }

            playerWorldPosition = nextWorld;
            playerGridPosition = sceneBuilder.GetNearestGridPosition(playerWorldPosition);

            if (gameplay.HasLevel && gameplay.IsWalkable(playerGridPosition) && !gameplay.IsOccupied(playerGridPosition))
            {
                gameplay.SetPlayerPose(playerGridPosition, facingDirection);
            }
            else if (gameplay.HasLevel)
            {
                gameplay.SetPlayerFacing(facingDirection);
            }

            sceneBuilder.SetPlayerWorldPosition(playerWorldPosition);
            RefreshFocusedBox();
            return true;
        }

        public void ToggleMovementMode()
        {
            SetMovementMode(movementMode == PlayerMovementMode.GridStep
                ? PlayerMovementMode.Continuous
                : PlayerMovementMode.GridStep);
        }

        public void SetMovementMode(PlayerMovementMode mode)
        {
            if (movementMode == mode)
            {
                return;
            }

            EnsureSceneBuilder();
            movementMode = mode;

            if (movementMode == PlayerMovementMode.Continuous)
            {
                playerWorldPosition = sceneBuilder.GetGridWorldPosition(playerGridPosition) + Vector3.up * 0.55f;
            }
            else
            {
                facingDirection = DirectionFromWorldDirection(facingWorldDirection);
                facingWorldDirection = WorldDirectionFromOffset(DirectionUtility.ToOffset(facingDirection));
                var nearestGrid = sceneBuilder.GetNearestGridPosition(playerWorldPosition);
                if (gameplay.HasLevel && gameplay.IsWalkable(nearestGrid) && !gameplay.IsOccupied(nearestGrid))
                {
                    playerGridPosition = nearestGrid;
                    gameplay.SetPlayerPose(playerGridPosition, facingDirection);
                }
                else if (gameplay.HasLevel)
                {
                    playerGridPosition = gameplay.Player.Position;
                }
                else
                {
                    playerGridPosition = nearestGrid;
                }
            }

            SyncPlayerTransform();
            RefreshFocusedBox();
        }

        public bool TryPush()
        {
            return TryPushInternal(Vector2Int.zero, false);
        }

        private bool TryPushFromGridMove(Direction direction)
        {
            return TryPushInternal(DirectionUtility.ToOffset(direction), true);
        }

        private bool TryPushInternal(Vector2Int forcedDirectionOffset, bool movePlayerIntoBoxCell)
        {
            if (pushBusy || IsCompleted || Time.time < nextPushAllowedTime)
            {
                return false;
            }

            var success = false;
            // The focused push query centralizes range, facing, and target-cell validation for both F-push and step-push.
            if (!TryGetFocusedPush(forcedDirectionOffset, out var boxView, out var directionOffset, out var from, out var to))
            {
                return false;
            }

            PushResult pushResult = default;
            var snapshot = CaptureSnapshot();

            if (gameplay.HasLevel)
            {
                if (movementMode == PlayerMovementMode.Continuous)
                {
                    var pushPlayerGrid = from - directionOffset;
                    if (!gameplay.SetPlayerPosition(pushPlayerGrid))
                    {
                        return false;
                    }
                }

                pushResult = gameplay.TryPush(directionOffset, movePlayerIntoBoxCell);
                success = pushResult.success;
                from = pushResult.from;
                to = pushResult.to;
            }
            else if (useLocalFallbackWhenGameplayMissing)
            {
                success = TryLocalPush(directionOffset, out from, out to);
                if (success && movePlayerIntoBoxCell)
                {
                    playerGridPosition = from;
                }
            }

            if (!success)
            {
                return false;
            }

            if (boxView.GridPosition != from && !sceneBuilder.TryGetBox(from, out boxView))
            {
                return false;
            }

            StepCount++;
            undoHistory.Add(snapshot);
            playerGridPosition = gameplay.HasLevel ? gameplay.Player.Position : movePlayerIntoBoxCell ? from : playerGridPosition;
            var wasBoxOnGoal = levelData != null && levelData.HasGoal(from.x, from.y);
            if (movementMode == PlayerMovementMode.GridStep)
            {
                SyncPlayerTransform();
            }

            PlayOneShot(sceneBuilder != null ? sceneBuilder.PushClip : null);
            StartCoroutine(AnimatePush(boxView, to, gameplay.HasLevel ? pushResult.box : null, wasBoxOnGoal));
            RefreshFocusedBox();
            return true;
        }

        public bool UndoLastStep()
        {
            if (pushBusy || undoHistory.Count == 0)
            {
                return false;
            }

            StopAllCoroutines();
            pushBusy = false;
            nextPushAllowedTime = 0f;

            var snapshot = undoHistory[undoHistory.Count - 1];
            undoHistory.RemoveAt(undoHistory.Count - 1);
            RestoreSnapshot(snapshot);
            return true;
        }

        private IEnumerator AnimatePush(BoxView boxView, Vector2Int to, BoxRuntimeState runtimeBox, bool wasBoxOnGoal)
        {
            pushBusy = true;
            nextPushAllowedTime = Time.time + PushCooldown;

            if (boxView != null)
            {
                if (gameplay.HasLevel && runtimeBox != null)
                {
                    gameplay.BeginPushCooldown(runtimeBox);
                }

                boxView.MoveTo(to, PushDuration);
            }

            yield return new WaitForSeconds(PushCooldown);

            if (gameplay.HasLevel)
            {
                if (runtimeBox != null)
                {
                    gameplay.CompleteBoxMove(runtimeBox);
                }

                gameplay.CompletePushCooldown();
            }

            pushBusy = false;
            var wasCompleted = IsCompleted;
            UpdateCompletionState();
            RefreshGoalVisuals();
            if (!wasBoxOnGoal && levelData != null && levelData.HasGoal(to.x, to.y))
            {
                PlayOneShot(sceneBuilder != null ? sceneBuilder.BoxOnGoalClip : null);
            }

            if (!wasCompleted && IsCompleted)
            {
                PlayOneShot(sceneBuilder != null ? sceneBuilder.LevelCompleteClip : null);
            }

            RefreshFocusedBox();
        }

        private void EnsureSceneBuilder()
        {
            if (sceneBuilder != null)
            {
                return;
            }

            sceneBuilder = GetComponent<LevelSceneBuilder>();
            if (sceneBuilder == null)
            {
                sceneBuilder = gameObject.AddComponent<LevelSceneBuilder>();
            }
        }

        private void PlayOneShot(AudioClip clip)
        {
            if (clip == null)
            {
                return;
            }

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                    audioSource.playOnAwake = false;
                }
            }

            audioSource.PlayOneShot(clip);
        }

        private SessionSnapshot CaptureSnapshot()
        {
            var snapshot = new SessionSnapshot
            {
                playerGridPosition = playerGridPosition,
                playerWorldPosition = playerWorldPosition,
                facingDirection = facingDirection,
                facingWorldDirection = facingWorldDirection,
                stepCount = StepCount,
                isCompleted = IsCompleted,
                boxPositions = new List<Vector2Int>()
            };

            if (gameplay.HasLevel)
            {
                for (var i = 0; i < gameplay.Boxes.Count; i++)
                {
                    snapshot.boxPositions.Add(gameplay.Boxes[i].Position);
                }
            }
            else if (sceneBuilder != null)
            {
                for (var i = 0; i < sceneBuilder.BoxViews.Count; i++)
                {
                    var box = sceneBuilder.BoxViews[i];
                    if (box != null)
                    {
                        snapshot.boxPositions.Add(box.GridPosition);
                    }
                }
            }

            return snapshot;
        }

        private void RestoreSnapshot(SessionSnapshot snapshot)
        {
            StepCount = snapshot.stepCount;
            IsCompleted = snapshot.isCompleted;
            playerGridPosition = snapshot.playerGridPosition;
            playerWorldPosition = snapshot.playerWorldPosition;
            facingDirection = snapshot.facingDirection;
            facingWorldDirection = snapshot.facingWorldDirection;

            if (gameplay.HasLevel)
            {
                gameplay.RestoreState(playerGridPosition, facingDirection, snapshot.boxPositions);
            }

            if (sceneBuilder == null)
            {
                return;
            }

            for (var i = 0; i < sceneBuilder.BoxViews.Count && i < snapshot.boxPositions.Count; i++)
            {
                var box = sceneBuilder.BoxViews[i];
                if (box != null)
                {
                    box.SetGridPosition(snapshot.boxPositions[i], true);
                }
            }

            SyncPlayerTransform();
            RefreshGoalVisuals();
            RefreshFocusedBox();
        }

        private void SyncPlayerTransform()
        {
            if (sceneBuilder == null)
            {
                return;
            }

            if (movementMode == PlayerMovementMode.Continuous)
            {
                sceneBuilder.SetPlayerWorldPosition(playerWorldPosition);
            }
            else
            {
                sceneBuilder.SetPlayerGridPosition(playerGridPosition);
            }

            SyncPlayerFacing();
        }

        private void SyncPlayerFacing()
        {
            if (sceneBuilder != null)
            {
                var facing = facingWorldDirection.sqrMagnitude > 0.0001f ? facingWorldDirection.normalized : Vector3.forward;
                sceneBuilder.SetPlayerFacing(Quaternion.LookRotation(facing, Vector3.up));
            }
        }

        private bool TryLocalMove(Direction direction)
        {
            var next = playerGridPosition + DirectionUtility.ToOffset(direction);
            return IsWalkable(next) && !HasBox(next);
        }

        private bool TryLocalPush(Vector2Int offset, out Vector2Int from, out Vector2Int to)
        {
            from = playerGridPosition + offset;
            to = from + offset;

            return HasBox(from) && IsWalkable(to) && !HasBox(to);
        }

        private bool IsWalkable(Vector2Int position)
        {
            if (levelData == null || !levelData.IsInside(position))
            {
                return false;
            }

            var tile = levelData.GetBaseTile(position.x, position.y);
            return tile == BaseTileType.Floor;
        }

        private bool HasBox(Vector2Int position)
        {
            return sceneBuilder != null && sceneBuilder.TryGetBox(position, out _);
        }

        private bool CanOccupyWorldPosition(Vector3 worldPosition)
        {
            if (sceneBuilder == null)
            {
                return false;
            }

            // Test a small neighborhood instead of the entire map; player radius only overlaps adjacent cells.
            var centerGrid = sceneBuilder.GetNearestGridPosition(worldPosition);
            for (var y = centerGrid.y - 1; y <= centerGrid.y + 1; y++)
            {
                for (var x = centerGrid.x - 1; x <= centerGrid.x + 1; x++)
                {
                    var grid = new Vector2Int(x, y);
                    if (IsWalkable(grid) && !HasBox(grid))
                    {
                        continue;
                    }

                    var cellCenter = sceneBuilder.GetGridWorldPosition(grid);
                    var closestX = Mathf.Clamp(worldPosition.x, cellCenter.x - 0.5f, cellCenter.x + 0.5f);
                    var closestZ = Mathf.Clamp(worldPosition.z, cellCenter.z - 0.5f, cellCenter.z + 0.5f);
                    var dx = worldPosition.x - closestX;
                    var dz = worldPosition.z - closestZ;
                    if (dx * dx + dz * dz < playerRadius * playerRadius)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private bool TryFindFacingBoxPosition(Vector2Int directionOffset, out Vector2Int position)
        {
            position = playerGridPosition + directionOffset;
            if (sceneBuilder == null)
            {
                return false;
            }

            var bestDistance = float.MaxValue;
            var found = false;
            var playerFlat = new Vector3(playerWorldPosition.x, 0f, playerWorldPosition.z);
            var facing = facingWorldDirection.sqrMagnitude > 0.0001f
                ? facingWorldDirection.normalized
                : new Vector3(directionOffset.x, 0f, directionOffset.y).normalized;

            foreach (var boxView in sceneBuilder.BoxViews)
            {
                if (boxView == null)
                {
                    continue;
                }

                var boxFlat = new Vector3(boxView.transform.position.x, 0f, boxView.transform.position.z);
                var toBox = boxFlat - playerFlat;
                var forwardDistance = Vector3.Dot(toBox, facing);
                if (forwardDistance <= 0f || forwardDistance > maxPushDistance)
                {
                    continue;
                }

                var lateralDistance = (toBox - facing * forwardDistance).magnitude;
                if (lateralDistance > 0.45f || forwardDistance >= bestDistance)
                {
                    continue;
                }

                bestDistance = forwardDistance;
                position = boxView.GridPosition;
                found = true;
            }

            return found;
        }

        public void RefreshFocusedBox()
        {
            if (focusedBox != null)
            {
                focusedBox.SetFocused(false);
                focusedBox = null;
            }

            if (IsCompleted)
            {
                return;
            }

            if (TryGetFocusedPush(Vector2Int.zero, out var nextFocusedBox, out _, out _, out _))
            {
                focusedBox = nextFocusedBox;
                focusedBox.SetFocused(true);
            }
        }

        private bool TryGetFocusedPush(Vector2Int forcedDirectionOffset, out BoxView boxView, out Vector2Int directionOffset, out Vector2Int from, out Vector2Int to)
        {
            boxView = null;
            directionOffset = forcedDirectionOffset != Vector2Int.zero ? forcedDirectionOffset : GetInteractionOffset();
            from = Vector2Int.zero;
            to = Vector2Int.zero;

            if (pushBusy || IsCompleted || directionOffset == Vector2Int.zero || sceneBuilder == null)
            {
                return false;
            }

            if (movementMode == PlayerMovementMode.Continuous)
            {
                if (!TryFindFacingBoxPosition(directionOffset, out from))
                {
                    return false;
                }
            }
            else
            {
                from = playerGridPosition + directionOffset;
            }

            to = from + directionOffset;

            if (!sceneBuilder.TryGetBox(from, out boxView) || boxView == null || boxView.IsMoving)
            {
                return false;
            }

            return IsWalkable(to) && !HasBox(to);
        }

        private Vector2Int GetInteractionOffset()
        {
            if (movementMode == PlayerMovementMode.GridStep)
            {
                return DirectionUtility.ToOffset(facingDirection);
            }

            var facing = facingWorldDirection.sqrMagnitude > 0.0001f ? facingWorldDirection.normalized : Vector3.forward;
            var x = Mathf.Abs(facing.x) >= 0.35f ? (int)Mathf.Sign(facing.x) : 0;
            var y = Mathf.Abs(facing.z) >= 0.35f ? (int)Mathf.Sign(facing.z) : 0;
            return new Vector2Int(x, y);
        }

        private static Direction DirectionFromInput(Vector2 input)
        {
            if (Mathf.Abs(input.x) > Mathf.Abs(input.y))
            {
                return input.x >= 0f ? Direction.Right : Direction.Left;
            }

            return input.y >= 0f ? Direction.Up : Direction.Down;
        }

        private static Direction DirectionFromWorldDirection(Vector3 direction)
        {
            if (Mathf.Abs(direction.x) > Mathf.Abs(direction.z))
            {
                return direction.x >= 0f ? Direction.Right : Direction.Left;
            }

            return direction.z >= 0f ? Direction.Up : Direction.Down;
        }

        private static Vector3 WorldDirectionFromOffset(Vector2Int offset)
        {
            var direction = new Vector3(offset.x, 0f, offset.y);
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        }

        private void LoadGameplayLevel()
        {
            if (levelData == null)
            {
                return;
            }

            gameplay.LoadLevel(levelData);
            playerGridPosition = gameplay.Player.Position;
            facingDirection = gameplay.Player.Facing;
            facingWorldDirection = WorldDirectionFromOffset(DirectionUtility.ToOffset(facingDirection));
            UpdateCompletionState();
            RefreshGoalVisuals();
        }

        private void UpdateCompletionState()
        {
            IsCompleted = gameplay.HasLevel
                ? gameplay.IsCompleted
                : IsFallbackCompleted();
        }

        private bool IsFallbackCompleted()
        {
            if (levelData == null || sceneBuilder == null || levelData.GetGoalCount() == 0 || sceneBuilder.BoxViews.Count != levelData.GetGoalCount())
            {
                return false;
            }

            for (var i = 0; i < sceneBuilder.BoxViews.Count; i++)
            {
                var box = sceneBuilder.BoxViews[i];
                if (box == null || !levelData.HasGoal(box.GridPosition.x, box.GridPosition.y))
                {
                    return false;
                }
            }

            return true;
        }

        private void RefreshGoalVisuals()
        {
            if (levelData == null || sceneBuilder == null)
            {
                return;
            }

            for (var i = 0; i < sceneBuilder.BoxViews.Count; i++)
            {
                var box = sceneBuilder.BoxViews[i];
                if (box != null)
                {
                    box.SetOnGoal(levelData.HasGoal(box.GridPosition.x, box.GridPosition.y));
                }
            }
        }
    }
}
