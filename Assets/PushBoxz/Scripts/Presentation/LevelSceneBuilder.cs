using System.Collections.Generic;
using PushBoxz.Data;
using UnityEngine;

namespace PushBoxz.Presentation
{
    /// <summary>
    /// Builds a playable scene hierarchy from LevelDataAsset.
    /// It is responsible for visuals only; gameplay state remains in GameSession/PushGameplayController.
    /// </summary>
    public class LevelSceneBuilder : MonoBehaviour
    {
        [Header("Mapping")]
        [SerializeField] private GridWorldMapper mapper = new GridWorldMapper();

        [Header("Level State")]
        [SerializeField] private LevelDataAsset levelData;

        [Header("Optional Prefabs")]
        [SerializeField] private GameObject floorPrefab;
        [SerializeField] private GameObject wallPrefab;
        [SerializeField] private GameObject goalPrefab;
        [SerializeField] private GameObject boxPrefab;
        [SerializeField] private GameObject playerPrefab;

        [Header("Materials")]
        [SerializeField] private Material floorMaterial;
        [SerializeField] private Material wallMaterial;
        [SerializeField] private Material goalMaterial;
        [SerializeField] private Material boxMaterial;
        [SerializeField] private Material playerMaterial;

        [Header("Audio")]
        [SerializeField] private AudioClip moveClip;
        [SerializeField] private AudioClip pushClip;
        [SerializeField] private AudioClip boxOnGoalClip;
        [SerializeField] private AudioClip levelCompleteClip;

        [Header("Camera")]
        [SerializeField] private bool frameMainCamera = true;

        [Header("UI Framing Offset")]
        [SerializeField] private bool applyUiSafeAreaOffset = true;
        [SerializeField] private Vector2 uiOffsetAtSmallLevel = new Vector2(1.4f, 0.4f);
        [SerializeField] private Vector2 uiOffsetAtLargeLevel = new Vector2(3f, 2f);
        [SerializeField] private Vector2Int uiOffsetLevelSizeRange = new Vector2Int(5, 16);

        private readonly List<BoxView> boxViews = new List<BoxView>();
        private Transform terrainRoot;
        private Transform objectRoot;
        private Transform playerRoot;
        private Vector3 lastAppliedUiOffset;

        public GridWorldMapper Mapper
        {
            get { return mapper; }
        }

        public LevelDataAsset LevelData
        {
            get { return levelData; }
            set { levelData = value; }
        }

        public IReadOnlyList<BoxView> BoxViews
        {
            get { return boxViews; }
        }

        public Transform PlayerRoot
        {
            get { return playerRoot; }
        }

        public AudioClip MoveClip
        {
            get { return moveClip; }
        }

        public AudioClip PushClip
        {
            get { return pushClip; }
        }

        public AudioClip BoxOnGoalClip
        {
            get { return boxOnGoalClip; }
        }

        public AudioClip LevelCompleteClip
        {
            get { return levelCompleteClip; }
        }

        public void ConfigureOptionalAssets(
            GameObject floorPrefab,
            GameObject wallPrefab,
            GameObject goalPrefab,
            GameObject boxPrefab,
            GameObject playerPrefab,
            Material floorMaterial,
            Material wallMaterial,
            Material goalMaterial,
            Material boxMaterial,
            Material playerMaterial)
        {
            this.floorPrefab = floorPrefab;
            this.wallPrefab = wallPrefab;
            this.goalPrefab = goalPrefab;
            this.boxPrefab = boxPrefab;
            this.playerPrefab = playerPrefab;
            this.floorMaterial = floorMaterial;
            this.wallMaterial = wallMaterial;
            this.goalMaterial = goalMaterial;
            this.boxMaterial = boxMaterial;
            this.playerMaterial = playerMaterial;
        }

        public void ConfigureAudio(
            AudioClip moveClip,
            AudioClip pushClip,
            AudioClip boxOnGoalClip,
            AudioClip levelCompleteClip)
        {
            this.moveClip = moveClip;
            this.pushClip = pushClip;
            this.boxOnGoalClip = boxOnGoalClip;
            this.levelCompleteClip = levelCompleteClip;
        }

        public void ApplyRegistryAssets(LevelSceneBuilderRegistry registry)
        {
            if (registry == null)
            {
                return;
            }

            floorPrefab = registry.floorPrefab != null ? registry.floorPrefab : floorPrefab;
            wallPrefab = registry.wallPrefab != null ? registry.wallPrefab : wallPrefab;
            goalPrefab = registry.goalPrefab != null ? registry.goalPrefab : goalPrefab;
            boxPrefab = registry.boxPrefab != null ? registry.boxPrefab : boxPrefab;
            playerPrefab = registry.playerPrefab != null ? registry.playerPrefab : playerPrefab;
            moveClip = registry.moveClip != null ? registry.moveClip : moveClip;
            pushClip = registry.pushClip != null ? registry.pushClip : pushClip;
            boxOnGoalClip = registry.boxOnGoalClip != null ? registry.boxOnGoalClip : boxOnGoalClip;
            levelCompleteClip = registry.levelCompleteClip != null ? registry.levelCompleteClip : levelCompleteClip;
        }

        public void CopyConfigurationFrom(LevelSceneBuilder source)
        {
            if (source == null)
            {
                return;
            }

            levelData = source.levelData;
            mapper.CellSize = source.mapper.CellSize;
            mapper.Origin = source.mapper.Origin;
            lastAppliedUiOffset = Vector3.zero;
            applyUiSafeAreaOffset = source.applyUiSafeAreaOffset;
            uiOffsetAtSmallLevel = source.uiOffsetAtSmallLevel;
            uiOffsetAtLargeLevel = source.uiOffsetAtLargeLevel;
            uiOffsetLevelSizeRange = source.uiOffsetLevelSizeRange;
            ConfigureOptionalAssets(
                source.floorPrefab,
                source.wallPrefab,
                source.goalPrefab,
                source.boxPrefab,
                source.playerPrefab,
                source.floorMaterial,
                source.wallMaterial,
                source.goalMaterial,
                source.boxMaterial,
                source.playerMaterial);
            ConfigureAudio(
                source.moveClip,
                source.pushClip,
                source.boxOnGoalClip,
                source.levelCompleteClip);
        }

        public Vector3 GetGridWorldPosition(Vector2Int gridPosition)
        {
            return mapper.GridToWorld(gridPosition);
        }

        public Vector2Int GetNearestGridPosition(Vector3 worldPosition)
        {
            return mapper.WorldToGrid(worldPosition);
        }

        public void Clear()
        {
            boxViews.Clear();
            playerRoot = null;

            terrainRoot = terrainRoot != null ? terrainRoot : transform.Find("Terrain");
            objectRoot = objectRoot != null ? objectRoot : transform.Find("Objects");

            DestroyChildRoot(terrainRoot);
            DestroyChildRoot(objectRoot);
            terrainRoot = null;
            objectRoot = null;
        }

        public void Build(LevelDataAsset level)
        {
            levelData = level;
            Clear();
            RemoveLastAppliedUiOffset();

            if (level == null)
            {
                Debug.LogWarning("LevelSceneBuilder.Build called without a LevelDataAsset.", this);
                return;
            }

            // Offset the spawned level for left-side UI while keeping camera framing anchored to the original map center.
            var cameraFrameOrigin = mapper.Origin;
            ApplyUiOffsetForLevel(level);

            terrainRoot = CreateRoot("Terrain");
            objectRoot = CreateRoot("Objects");

            for (var y = 0; y < level.height; y++)
            {
                for (var x = 0; x < level.width; x++)
                {
                    var tile = level.GetBaseTile(x, y);
                    if (tile == BaseTileType.Empty)
                    {
                        continue;
                    }

                    CreateFloor(new Vector2Int(x, y));

                    if (tile == BaseTileType.Floor && level.HasGoal(x, y))
                    {
                        CreateGoal(new Vector2Int(x, y));
                    }

                    if (tile == BaseTileType.Wall)
                    {
                        CreateWall(new Vector2Int(x, y));
                    }

                }
            }

            for (var i = 0; i < level.boxStarts.Count; i++)
            {
                CreateBox(level.boxStarts[i]);
            }

            CreatePlayer(level.playerStart);

            if (frameMainCamera)
            {
                FrameMainCamera(level, cameraFrameOrigin);
            }
        }

        public bool TryGetBox(Vector2Int gridPosition, out BoxView boxView)
        {
            for (var i = 0; i < boxViews.Count; i++)
            {
                if (boxViews[i] != null && boxViews[i].GridPosition == gridPosition)
                {
                    boxView = boxViews[i];
                    return true;
                }
            }

            boxView = null;
            return false;
        }

        public void SetPlayerGridPosition(Vector2Int gridPosition)
        {
            if (playerRoot == null)
            {
                return;
            }

            playerRoot.position = mapper.GridToWorld(gridPosition) + Vector3.up * 0.55f;
        }

        public void SetPlayerWorldPosition(Vector3 worldPosition)
        {
            if (playerRoot == null)
            {
                return;
            }

            playerRoot.position = new Vector3(worldPosition.x, mapper.GridToWorld(Vector2Int.zero).y + 0.55f, worldPosition.z);
        }

        public void SetPlayerFacing(Quaternion rotation)
        {
            if (playerRoot != null)
            {
                playerRoot.rotation = rotation;
            }
        }

        private Transform CreateRoot(string rootName)
        {
            var root = new GameObject(rootName).transform;
            root.SetParent(transform, false);
            return root;
        }

        private void DestroyChildRoot(Transform root)
        {
            if (root == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                root.gameObject.SetActive(false);
                Destroy(root.gameObject);
            }
            else
            {
                DestroyImmediate(root.gameObject);
            }
        }

        private void RemoveLastAppliedUiOffset()
        {
            if (lastAppliedUiOffset == Vector3.zero)
            {
                return;
            }

            mapper.Origin -= lastAppliedUiOffset;
            lastAppliedUiOffset = Vector3.zero;
        }

        private void ApplyUiOffsetForLevel(LevelDataAsset level)
        {
            if (!applyUiSafeAreaOffset || level == null)
            {
                return;
            }

            var offset = CalculateUiOffset(level);
            mapper.Origin += offset;
            lastAppliedUiOffset = offset;
        }

        private Vector3 CalculateUiOffset(LevelDataAsset level)
        {
            var minSize = Mathf.Min(uiOffsetLevelSizeRange.x, uiOffsetLevelSizeRange.y);
            var maxSize = Mathf.Max(uiOffsetLevelSizeRange.x, uiOffsetLevelSizeRange.y);
            var dimension = Mathf.Max(level.width, level.height);
            var t = Mathf.InverseLerp(minSize, maxSize, dimension);
            var offset = Vector2.Lerp(uiOffsetAtSmallLevel, uiOffsetAtLargeLevel, t) * mapper.CellSize;
            return new Vector3(offset.x, 0f, offset.y);
        }

        private void CreateFloor(Vector2Int gridPosition)
        {
            var view = CreateInstance(floorPrefab, PrimitiveType.Cube, "Floor", terrainRoot, out var usesPrefab);
            view.transform.position = mapper.GridToWorld(gridPosition) + Vector3.down * 0.05f;
            view.transform.localScale = new Vector3(mapper.CellSize, 0.1f, mapper.CellSize);
            ApplyFallbackMaterial(view, usesPrefab, floorMaterial, new Color(0.38f, 0.44f, 0.42f));
        }

        private void CreateWall(Vector2Int gridPosition)
        {
            var view = CreateInstance(wallPrefab, PrimitiveType.Cube, "Wall", terrainRoot, out var usesPrefab);
            view.transform.position = mapper.GridToWorld(gridPosition) + Vector3.up * 0.5f;
            view.transform.localScale = Vector3.one * mapper.CellSize;
            ApplyFallbackMaterial(view, usesPrefab, wallMaterial, new Color(0.18f, 0.2f, 0.23f));
        }

        private void CreateGoal(Vector2Int gridPosition)
        {
            var view = CreateInstance(goalPrefab, PrimitiveType.Cylinder, "Goal", terrainRoot, out var usesPrefab);
            view.transform.position = mapper.GridToWorld(gridPosition) + Vector3.up * 0.012f;
            view.transform.localScale = Vector3.one;
            ApplyFallbackMaterial(view, usesPrefab, goalMaterial, new Color(0.15f, 0.82f, 0.46f));
        }

        private void CreateBox(Vector2Int gridPosition)
        {
            var view = CreateInstance(boxPrefab, PrimitiveType.Cube, "Box", objectRoot, out var usesPrefab);
            view.transform.localScale = Vector3.one * (mapper.CellSize * 0.78f);
            ApplyFallbackMaterial(view, usesPrefab, boxMaterial, new Color(0.85f, 0.57f, 0.2f));

            var boxView = view.GetComponent<BoxView>();
            if (boxView == null)
            {
                boxView = view.AddComponent<BoxView>();
            }

            boxView.Initialize(mapper, gridPosition);
            boxViews.Add(boxView);
        }

        private void CreatePlayer(Vector2Int gridPosition)
        {
            var view = CreateInstance(playerPrefab, PrimitiveType.Capsule, "Player", objectRoot, out var usesPrefab);
            view.transform.localScale = new Vector3(mapper.CellSize * 0.55f, mapper.CellSize * 0.55f, mapper.CellSize * 0.55f);
            ApplyFallbackMaterial(view, usesPrefab, playerMaterial, new Color(0.24f, 0.48f, 0.92f));
            playerRoot = view.transform;
            SetPlayerGridPosition(gridPosition);
        }

        private GameObject CreateInstance(GameObject prefab, PrimitiveType fallbackPrimitive, string objectName, Transform parent, out bool usesPrefab)
        {
            GameObject instance;
            if (prefab != null)
            {
                // Prefabs keep their authored materials; fallback primitives receive generated/configured materials below.
                instance = Instantiate(prefab, parent);
                usesPrefab = true;
            }
            else
            {
                instance = GameObject.CreatePrimitive(fallbackPrimitive);
                instance.transform.SetParent(parent, false);
                usesPrefab = false;
            }

            instance.name = objectName;
            return instance;
        }

        private static void ApplyFallbackMaterial(GameObject target, bool usesPrefab, Material material, Color fallbackColor)
        {
            if (usesPrefab)
            {
                return;
            }

            var renderer = target.GetComponentInChildren<Renderer>();
            if (renderer == null)
            {
                return;
            }

            if (material != null)
            {
                renderer.sharedMaterial = material;
                return;
            }

            var generated = new Material(Shader.Find("Standard"));
            generated.color = fallbackColor;
            renderer.sharedMaterial = generated;
        }

        private void FrameMainCamera(LevelDataAsset level, Vector3 cameraFrameOrigin)
        {
            var camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            var center = cameraFrameOrigin + new Vector3(
                (level.width - 1) * mapper.CellSize * 0.5f,
                0f,
                (level.height - 1) * mapper.CellSize * 0.5f);
            var span = Mathf.Max(level.width, level.height) * mapper.CellSize;
            camera.transform.position = center + new Vector3(0f, span * 1.2f + 2f, -span * 0.35f - 2f);
            camera.transform.rotation = Quaternion.Euler(65f, 0f, 0f);
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max(4f, span * 0.65f);
        }
    }
}
