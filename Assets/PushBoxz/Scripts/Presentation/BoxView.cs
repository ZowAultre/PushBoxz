using System.Collections;
using UnityEngine;

namespace PushBoxz.Presentation
{
    public class BoxView : MonoBehaviour
    {
        private GridWorldMapper mapper;
        private Coroutine moveRoutine;
        private Renderer targetRenderer;
        private MaterialPropertyBlock propertyBlock;
        private Color baseColor = Color.white;
        private Color completedColor = new Color(0.25f, 0.85f, 0.45f);
        private readonly Color focusedColor = new Color(1f, 0.95f, 0.18f);
        private bool isFocused;
        private bool isOnGoal;

        public Vector2Int GridPosition { get; private set; }
        public bool IsMoving { get; private set; }

        public void Initialize(GridWorldMapper gridMapper, Vector2Int gridPosition)
        {
            mapper = gridMapper;
            CacheRenderer();
            SetGridPosition(gridPosition, true);
        }

        public void SetGridPosition(Vector2Int gridPosition, bool snap)
        {
            GridPosition = gridPosition;

            if (mapper == null)
            {
                return;
            }

            if (snap)
            {
                transform.position = mapper.GridToWorld(gridPosition) + Vector3.up * 0.45f;
            }
        }

        public Coroutine MoveTo(Vector2Int gridPosition, float duration)
        {
            if (moveRoutine != null)
            {
                StopCoroutine(moveRoutine);
            }

            moveRoutine = StartCoroutine(MoveRoutine(gridPosition, Mathf.Max(0.01f, duration)));
            return moveRoutine;
        }

        public void SetFocused(bool focused)
        {
            isFocused = focused;
            ApplyVisualState();
        }

        public void SetOnGoal(bool onGoal)
        {
            isOnGoal = onGoal;
            ApplyVisualState();
        }

        private void ApplyVisualState()
        {
            CacheRenderer();
            if (targetRenderer != null)
            {
                propertyBlock ??= new MaterialPropertyBlock();
                targetRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor("_Color", isFocused ? focusedColor : isOnGoal ? completedColor : baseColor);
                targetRenderer.SetPropertyBlock(propertyBlock);
            }
        }

        private IEnumerator MoveRoutine(Vector2Int gridPosition, float duration)
        {
            if (mapper == null)
            {
                SetGridPosition(gridPosition, false);
                yield break;
            }

            IsMoving = true;
            var start = transform.position;
            var end = mapper.GridToWorld(gridPosition) + Vector3.up * 0.45f;
            var elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                transform.position = Vector3.Lerp(start, end, SmoothStep(t));
                yield return null;
            }

            transform.position = end;
            GridPosition = gridPosition;
            IsMoving = false;
            moveRoutine = null;
        }

        private static float SmoothStep(float value)
        {
            return value * value * (3f - 2f * value);
        }

        private void CacheRenderer()
        {
            if (targetRenderer != null)
            {
                return;
            }

            targetRenderer = GetComponentInChildren<Renderer>();
            if (targetRenderer == null)
            {
                return;
            }

            var sharedMaterial = targetRenderer.sharedMaterial;
            if (sharedMaterial != null && sharedMaterial.HasProperty("_Color"))
            {
                baseColor = sharedMaterial.color;
            }
        }
    }
}
