using UnityEngine;

namespace PushBoxz.Presentation
{
    [System.Serializable]
    public class GridWorldMapper
    {
        [SerializeField] private float cellSize = 1f;
        [SerializeField] private Vector3 origin = Vector3.zero;

        public float CellSize
        {
            get { return Mathf.Max(0.01f, cellSize); }
            set { cellSize = Mathf.Max(0.01f, value); }
        }

        public Vector3 Origin
        {
            get { return origin; }
            set { origin = value; }
        }

        public Vector3 GridToWorld(Vector2Int gridPosition)
        {
            return origin + new Vector3(gridPosition.x * CellSize, 0f, gridPosition.y * CellSize);
        }

        public Vector3 GridToWorld(int x, int y)
        {
            return GridToWorld(new Vector2Int(x, y));
        }

        public Vector2Int WorldToGrid(Vector3 worldPosition)
        {
            var local = worldPosition - origin;
            return new Vector2Int(
                Mathf.RoundToInt(local.x / CellSize),
                Mathf.RoundToInt(local.z / CellSize));
        }
    }
}
