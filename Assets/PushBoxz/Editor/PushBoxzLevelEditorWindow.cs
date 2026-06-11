using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PushBoxz.Core;
using PushBoxz.Data;
using PushBoxz.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PushBoxz.Editor
{
    public class PushBoxzLevelEditorWindow : EditorWindow
    {
        private const string LevelsFolder = "Assets/PushBoxz/Levels";
        private const string DraftSessionKey = "PushBoxz.LevelEditor.Draft.v1";
        private const int MinSize = 1;
        private const int MaxSize = 40;

        private enum Brush
        {
            Floor,
            Wall,
            Goal,
            Box,
            Player,
            Erase
        }

        private readonly Color floorColor = new Color(0.72f, 0.74f, 0.68f);
        private readonly Color wallColor = new Color(0.28f, 0.31f, 0.35f);
        private readonly Color emptyColor = new Color(0.15f, 0.16f, 0.18f);
        private readonly Color goalColor = new Color(0.18f, 0.82f, 0.48f);
        private readonly Color boxColor = new Color(0.78f, 0.48f, 0.22f);
        private readonly Color playerColor = new Color(0.20f, 0.58f, 0.88f);
        private readonly Color gridLineColor = new Color(0f, 0f, 0f, 0.28f);

        private LevelDataAsset loadedAsset;
        private string levelId = "level_001";
        private string displayName = "Level 001";
        private int width = 5;
        private int height = 6;
        private BaseTileType[,] tiles;
        private readonly HashSet<Vector2Int> goals = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> boxes = new HashSet<Vector2Int>();
        private Vector2Int playerStart = Vector2Int.zero;
        private bool hasPlayer;
        private Direction editorPlayerFacing = Direction.Up;
        private bool reverseDesignMode;
        private Brush activeBrush = Brush.Floor;
        private Vector2 scrollPosition;
        private string lastMessage;
        private MessageType lastMessageType = MessageType.Info;

        [Serializable]
        private class DraftState
        {
            public string assetPath;
            public string levelId;
            public string displayName;
            public int width;
            public int height;
            public int playerX;
            public int playerY;
            public bool hasPlayer;
            public int playerFacing;
            public bool reverseDesignMode;
            public int activeBrush;
            public List<TileDraft> tiles = new List<TileDraft>();
            public List<BoxDraft> boxes = new List<BoxDraft>();
        }

        [Serializable]
        private class TileDraft
        {
            public int x;
            public int y;
            public BaseTileType baseType;
            public bool hasGoal;
        }

        [Serializable]
        private class BoxDraft
        {
            public int x;
            public int y;
        }

        [MenuItem("Tools/PushBoxz/Level Editor")]
        public static void ShowWindow()
        {
            var window = GetWindow<PushBoxzLevelEditorWindow>("PushBoxz 关卡编辑器");
            window.minSize = new Vector2(560f, 480f);
            window.EnsureGrid();
            window.Show();
        }

        private void OnEnable()
        {
            if (!RestoreDraftState())
            {
                EnsureGrid();
            }
        }

        private void OnDisable()
        {
            SaveDraftState();
        }

        private void OnGUI()
        {
            EnsureGrid();
            HandleReverseDesignInput();

            EditorGUILayout.Space(8f);
            DrawAssetBar();
            EditorGUILayout.Space(6f);
            DrawMetadata();
            EditorGUILayout.Space(6f);
            DrawToolbar();
            EditorGUILayout.Space(6f);
            DrawValidationPanel();
            EditorGUILayout.Space(8f);
            DrawGrid();
            EditorGUILayout.Space(8f);
            DrawActions();
            SaveDraftState();
        }

        private void DrawAssetBar()
        {
            EditorGUILayout.LabelField("关卡资源", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                var nextAsset = (LevelDataAsset)EditorGUILayout.ObjectField(loadedAsset, typeof(LevelDataAsset), false);
                if (nextAsset != loadedAsset)
                {
                    loadedAsset = nextAsset;
                    if (loadedAsset != null)
                    {
                        LoadFromAsset(loadedAsset);
                    }
                }

                if (GUILayout.Button("加载", GUILayout.Width(72f)))
                {
                    if (loadedAsset == null)
                    {
                        SetMessage("请先指定一个 LevelDataAsset 再加载。", MessageType.Warning);
                    }
                    else
                    {
                        LoadFromAsset(loadedAsset);
                    }
                }

                if (GUILayout.Button("新建", GUILayout.Width(72f)))
                {
                    loadedAsset = null;
                    levelId = "level_001";
                    displayName = "Level 001";
                    width = 5;
                    height = 6;
                    hasPlayer = false;
                    goals.Clear();
                    boxes.Clear();
                    CreateFilledGrid(BaseTileType.Floor);
                    SaveDraftState();
                    SetMessage("已创建新的关卡草稿。", MessageType.Info);
                }
            }
        }

        private void DrawMetadata()
        {
            EditorGUILayout.LabelField("关卡信息", EditorStyles.boldLabel);
            levelId = EditorGUILayout.TextField("关卡 ID", levelId);
            displayName = EditorGUILayout.TextField("显示名称", displayName);

            using (new EditorGUILayout.HorizontalScope())
            {
                var nextWidth = Mathf.Clamp(EditorGUILayout.IntField("宽度", width), MinSize, MaxSize);
                var nextHeight = Mathf.Clamp(EditorGUILayout.IntField("高度", height), MinSize, MaxSize);
                if (nextWidth != width || nextHeight != height)
                {
                    ResizeGrid(nextWidth, nextHeight);
                    SaveDraftState();
                }
            }
        }

        private void DrawToolbar()
        {
            EditorGUILayout.LabelField("画笔", EditorStyles.boldLabel);
            activeBrush = (Brush)GUILayout.Toolbar((int)activeBrush, Enum.GetNames(typeof(Brush)));
            EditorGUILayout.HelpBox("点击格子进行绘制。目的地可以和箱子或玩家初始点在同一格。墙会清空该格所有对象。Erase 会清除对象/目的地，并保留普通 Floor。", MessageType.None);

            EditorGUI.BeginChangeCheck();
            reverseDesignMode = EditorGUILayout.ToggleLeft("倒推设计模式", reverseDesignMode);
            if (EditorGUI.EndChangeCheck())
            {
                GUI.FocusControl(null);
                SaveDraftState();
                Repaint();
            }

            if (reverseDesignMode)
            {
                EditorGUILayout.HelpBox("倒推模式：点击网格让窗口获得焦点，然后用 WASD 移动编辑器内的玩家并改变朝向。当玩家正前方有箱子时按 F，玩家和箱子会一起向玩家身后一格倒退。", MessageType.Info);
            }
        }

        private void DrawValidationPanel()
        {
            var issues = ValidateCurrentLevel();
            if (!string.IsNullOrEmpty(lastMessage))
            {
                EditorGUILayout.HelpBox(lastMessage, lastMessageType);
            }

            if (issues.Count == 0)
            {
                EditorGUILayout.HelpBox("校验通过。", MessageType.Info);
                return;
            }

            foreach (var issue in issues)
            {
                EditorGUILayout.HelpBox(issue, MessageType.Warning);
            }
        }

        private void DrawGrid()
        {
            EditorGUILayout.LabelField("网格", EditorStyles.boldLabel);

            var availableWidth = Mathf.Max(260f, position.width - 44f);
            var cellSize = Mathf.Clamp(Mathf.Floor(availableWidth / Mathf.Max(1, width)), 22f, 48f);
            var gridWidth = cellSize * width;
            var gridHeight = cellSize * height;

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.MinHeight(Mathf.Min(gridHeight + 24f, 420f)));
            var rect = GUILayoutUtility.GetRect(gridWidth, gridHeight, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));
            DrawGridBackground(rect, cellSize);
            HandleGridInput(rect, cellSize);
            EditorGUILayout.EndScrollView();
        }

        private void DrawGridBackground(Rect rect, float cellSize)
        {
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var drawY = height - 1 - y;
                    var cellRect = new Rect(rect.x + x * cellSize, rect.y + drawY * cellSize, cellSize, cellSize);
                    EditorGUI.DrawRect(cellRect, GetTileColor(tiles[x, y]));
                    EditorGUI.DrawRect(new Rect(cellRect.x, cellRect.y, cellRect.width, 1f), gridLineColor);
                    EditorGUI.DrawRect(new Rect(cellRect.x, cellRect.y, 1f, cellRect.height), gridLineColor);

                    var position = new Vector2Int(x, y);
                    if (goals.Contains(position))
                    {
                        var goalRect = RectInset(cellRect, cellSize * 0.25f);
                        EditorGUI.DrawRect(goalRect, goalColor);
                    }

                    if (boxes.Contains(position))
                    {
                        var boxRect = RectInset(cellRect, cellSize * 0.18f);
                        EditorGUI.DrawRect(boxRect, boxColor);
                    }

                    if (hasPlayer && playerStart == position)
                    {
                        var playerRect = RectInset(cellRect, cellSize * 0.26f);
                        EditorGUI.DrawRect(playerRect, playerColor);
                    }

                    if (cellSize >= 30f)
                    {
                        var label = GetCellLabel(position);
                        if (!string.IsNullOrEmpty(label))
                        {
                            var labelStyle = new GUIStyle(EditorStyles.boldLabel)
                            {
                                alignment = TextAnchor.MiddleCenter,
                                normal = { textColor = Color.white }
                            };
                            GUI.Label(cellRect, label, labelStyle);
                        }
                    }
                }
            }

            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), gridLineColor);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), gridLineColor);
        }

        private void HandleGridInput(Rect rect, float cellSize)
        {
            var current = Event.current;
            if (current == null || current.type != EventType.MouseDown && current.type != EventType.MouseDrag)
            {
                return;
            }

            if (!rect.Contains(current.mousePosition) || current.button != 0)
            {
                return;
            }

            var x = Mathf.FloorToInt((current.mousePosition.x - rect.x) / cellSize);
            var drawY = Mathf.FloorToInt((current.mousePosition.y - rect.y) / cellSize);
            var y = height - 1 - drawY;
            if (x < 0 || y < 0 || x >= width || y >= height)
            {
                return;
            }

            PaintCell(new Vector2Int(x, y));
            SaveDraftState();
            current.Use();
            Repaint();
        }

        private void DrawActions()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("保存 LevelDataAsset", GUILayout.Height(30f)))
                {
                    SaveLevel();
                }

                if (GUILayout.Button("生成场景预览", GUILayout.Height(30f)))
                {
                    GeneratePreviewInScene();
                }
            }
        }

        private void PaintCell(Vector2Int position)
        {
            switch (activeBrush)
            {
                case Brush.Floor:
                    tiles[position.x, position.y] = BaseTileType.Floor;
                    break;
                case Brush.Wall:
                    tiles[position.x, position.y] = BaseTileType.Wall;
                    goals.Remove(position);
                    boxes.Remove(position);
                    if (hasPlayer && playerStart == position)
                    {
                        hasPlayer = false;
                    }
                    break;
                case Brush.Goal:
                    tiles[position.x, position.y] = BaseTileType.Floor;
                    goals.Add(position);
                    break;
                case Brush.Box:
                    tiles[position.x, position.y] = BaseTileType.Floor;
                    if (hasPlayer && playerStart == position)
                    {
                        hasPlayer = false;
                    }
                    boxes.Add(position);
                    break;
                case Brush.Player:
                    tiles[position.x, position.y] = BaseTileType.Floor;
                    boxes.Remove(position);
                    playerStart = position;
                    hasPlayer = true;
                    editorPlayerFacing = Direction.Up;
                    break;
                case Brush.Erase:
                    tiles[position.x, position.y] = BaseTileType.Floor;
                    goals.Remove(position);
                    boxes.Remove(position);
                    if (hasPlayer && playerStart == position)
                    {
                        hasPlayer = false;
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void SaveLevel()
        {
            var issues = ValidateCurrentLevel();
            if (issues.Count > 0)
            {
                SetMessage("请先修复校验问题，再保存关卡。", MessageType.Warning);
                return;
            }

            EnsureLevelsFolder();

            var asset = loadedAsset;
            if (asset == null)
            {
                var path = AssetDatabase.GenerateUniqueAssetPath((LevelsFolder + "/" + SanitizeFileName(levelId) + ".asset").Replace("\\", "/"));
                asset = CreateInstance<LevelDataAsset>();
                AssetDatabase.CreateAsset(asset, path);
                loadedAsset = asset;
            }

            ApplyToAsset(asset);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = asset;
            SetMessage("已保存关卡资源：" + AssetDatabase.GetAssetPath(asset), MessageType.Info);
        }

        private void GeneratePreviewInScene()
        {
            var issues = ValidateCurrentLevel();
            if (issues.Count > 0)
            {
                SetMessage("请先修复校验问题，再生成预览。", MessageType.Warning);
                return;
            }

            if (loadedAsset == null)
            {
                SaveLevel();
                if (loadedAsset == null)
                {
                    SetMessage("请先保存关卡，再生成可试玩预览。", MessageType.Warning);
                    return;
                }
            }

            ApplyToAsset(loadedAsset);
            EditorUtility.SetDirty(loadedAsset);
            AssetDatabase.SaveAssets();

            GeneratePlayablePreview(loadedAsset);
            SetMessage("已生成可试玩预览。进入 Play Mode 后使用 WASD / F 进行测试。", MessageType.Info);
        }

        private void GeneratePlayablePreview(LevelDataAsset asset)
        {
            var host = GameObject.Find("PushBoxz Runtime Host");
            if (host == null)
            {
                host = new GameObject("PushBoxz Runtime Host");
            }

            var builder = host.GetComponent<LevelSceneBuilder>();
            if (builder == null)
            {
                builder = host.AddComponent<LevelSceneBuilder>();
            }

            var session = host.GetComponent<GameSession>();
            if (session == null)
            {
                session = host.AddComponent<GameSession>();
            }

            var input = host.GetComponent<PlayerInputController>();
            if (input == null)
            {
                input = host.AddComponent<PlayerInputController>();
            }

            var hud = host.GetComponent<RuntimeHud>();
            if (hud == null)
            {
                hud = host.AddComponent<RuntimeHud>();
            }

            session.LevelData = asset;
            builder.Build(asset);

            EditorUtility.SetDirty(host);
            EditorUtility.SetDirty(builder);
            EditorUtility.SetDirty(session);
            EditorUtility.SetDirty(input);
            EditorUtility.SetDirty(hud);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        private void HandleReverseDesignInput()
        {
            if (!reverseDesignMode || EditorGUIUtility.editingTextField)
            {
                return;
            }

            var current = Event.current;
            if (current == null || current.type != EventType.KeyDown)
            {
                return;
            }

            var handled = true;
            switch (current.keyCode)
            {
                case KeyCode.W:
                    TryReverseModeMove(Direction.Up);
                    break;
                case KeyCode.S:
                    TryReverseModeMove(Direction.Down);
                    break;
                case KeyCode.A:
                    TryReverseModeMove(Direction.Left);
                    break;
                case KeyCode.D:
                    TryReverseModeMove(Direction.Right);
                    break;
                case KeyCode.F:
                    TryReverseModePull();
                    break;
                default:
                    handled = false;
                    break;
            }

            if (!handled)
            {
                return;
            }

            SaveDraftState();
            current.Use();
            Repaint();
        }

        private void TryReverseModeMove(Direction direction)
        {
            editorPlayerFacing = direction;

            if (!hasPlayer)
            {
                SetMessage("倒推模式需要一个玩家初始点。", MessageType.Warning);
                return;
            }

            var target = playerStart + DirectionUtility.ToOffset(direction);
            if (!CanEditorPlayerOccupy(target))
            {
                SetMessage("倒推移动被阻挡：" + FormatPosition(target) + "。", MessageType.Info);
                return;
            }

            playerStart = target;
            SetMessage("倒推模式：玩家移动到 " + FormatPosition(playerStart) + "。", MessageType.Info);
        }

        private void TryReverseModePull()
        {
            if (!hasPlayer)
            {
                SetMessage("倒推模式需要一个玩家初始点。", MessageType.Warning);
                return;
            }

            var facingOffset = DirectionUtility.ToOffset(editorPlayerFacing);
            var boxPosition = playerStart + facingOffset;
            var playerTarget = playerStart - facingOffset;

            if (!boxes.Contains(boxPosition))
            {
                SetMessage("倒拉失败：玩家正前方没有箱子。", MessageType.Info);
                return;
            }

            if (!CanEditorPlayerOccupy(playerTarget))
            {
                SetMessage("倒拉失败：玩家身后一格被阻挡 " + FormatPosition(playerTarget) + "。", MessageType.Info);
                return;
            }

            boxes.Remove(boxPosition);
            boxes.Add(playerStart);
            playerStart = playerTarget;
            SetMessage("倒拉成功：箱子移动到 " + FormatPosition(playerStart + facingOffset) + "。", MessageType.Info);
        }

        private void LoadFromAsset(LevelDataAsset asset)
        {
            levelId = asset.levelId;
            displayName = asset.displayName;
            width = Mathf.Clamp(asset.width, MinSize, MaxSize);
            height = Mathf.Clamp(asset.height, MinSize, MaxSize);
            goals.Clear();
            CreateFilledGrid(BaseTileType.Floor);

            if (asset.cells != null)
            {
                foreach (var cell in asset.cells)
                {
                    if (cell == null || cell.x < 0 || cell.y < 0 || cell.x >= width || cell.y >= height)
                    {
                        continue;
                    }

                    tiles[cell.x, cell.y] = cell.baseType;
                    if (cell.hasGoal)
                    {
                        goals.Add(new Vector2Int(cell.x, cell.y));
                    }
                }
            }

            boxes.Clear();
            if (asset.boxStarts != null)
            {
                foreach (var box in asset.boxStarts)
                {
                    if (box.x >= 0 && box.y >= 0 && box.x < width && box.y < height)
                    {
                        boxes.Add(box);
                    }
                }
            }

            playerStart = asset.playerStart;
            hasPlayer = playerStart.x >= 0 && playerStart.y >= 0 && playerStart.x < width && playerStart.y < height;
            editorPlayerFacing = Direction.Up;
            SetMessage("已加载关卡资源：" + AssetDatabase.GetAssetPath(asset), MessageType.Info);
            SaveDraftState();
            Repaint();
        }

        private void ApplyToAsset(LevelDataAsset asset)
        {
            asset.levelId = levelId.Trim();
            asset.displayName = displayName.Trim();
            asset.width = width;
            asset.height = height;
            asset.playerStart = playerStart;

            asset.cells = new List<TileCell>(width * height);
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    asset.cells.Add(new TileCell(x, y, tiles[x, y], goals.Contains(new Vector2Int(x, y))));
                }
            }

            asset.boxStarts = boxes.OrderBy(b => b.y).ThenBy(b => b.x).ToList();
        }

        private List<string> ValidateCurrentLevel()
        {
            var issues = new List<string>();
            if (string.IsNullOrWhiteSpace(levelId))
            {
                issues.Add("错误：关卡 ID 不能为空。");
            }

            if (width < MinSize || height < MinSize)
            {
                issues.Add("错误：宽度和高度至少为 1。");
            }

            if (!hasPlayer)
            {
                issues.Add("错误：必须且只能有一个玩家初始点。");
            }
            else if (!IsInside(playerStart))
            {
                issues.Add("错误：玩家初始点超出关卡范围。");
            }
            else if (!IsWalkable(playerStart))
            {
                issues.Add("错误：玩家初始点必须在 Floor 格子上。");
            }

            if (boxes.Count == 0)
            {
                issues.Add("错误：至少需要一个箱子。");
            }

            if (goals.Count == 0)
            {
                issues.Add("错误：至少需要一个目的地。");
            }

            if (boxes.Count != goals.Count)
            {
                issues.Add("错误：箱子数量必须等于目的地数量。箱子：" + boxes.Count + "，目的地：" + goals.Count + "。");
            }

            foreach (var box in boxes)
            {
                if (!IsInside(box))
                {
                    issues.Add("错误：箱子超出关卡范围 " + FormatPosition(box) + "。");
                }
                else if (!IsWalkable(box))
                {
                    issues.Add("错误：箱子必须在 Floor 格子上 " + FormatPosition(box) + "。");
                }
            }

            foreach (var goal in goals)
            {
                if (!IsInside(goal))
                {
                    issues.Add("错误：目的地超出关卡范围 " + FormatPosition(goal) + "。");
                }
                else if (!IsWalkable(goal))
                {
                    issues.Add("错误：目的地必须在 Floor 格子上 " + FormatPosition(goal) + "。");
                }
            }

            if (hasPlayer && boxes.Contains(playerStart))
            {
                issues.Add("错误：玩家和箱子不能在同一格。");
            }

            if (!string.IsNullOrWhiteSpace(levelId) && FindExistingAssetByLevelId(levelId.Trim()) != null)
            {
                issues.Add("错误：已有另一个 LevelDataAsset 使用了这个关卡 ID。");
            }

            return issues;
        }

        private LevelDataAsset FindExistingAssetByLevelId(string id)
        {
            if (!AssetDatabase.IsValidFolder(LevelsFolder))
            {
                return null;
            }

            var guids = AssetDatabase.FindAssets("t:LevelDataAsset", new[] { LevelsFolder });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<LevelDataAsset>(path);
                if (asset != null && asset != loadedAsset && asset.levelId == id)
                {
                    return asset;
                }
            }

            return null;
        }

        private void ResizeGrid(int nextWidth, int nextHeight)
        {
            var nextTiles = new BaseTileType[nextWidth, nextHeight];
            for (var y = 0; y < nextHeight; y++)
            {
                for (var x = 0; x < nextWidth; x++)
                {
                    nextTiles[x, y] = x < width && y < height ? tiles[x, y] : BaseTileType.Floor;
                }
            }

            width = nextWidth;
            height = nextHeight;
            tiles = nextTiles;
            goals.RemoveWhere(goal => !IsInside(goal));
            boxes.RemoveWhere(box => !IsInside(box));
            if (hasPlayer && !IsInside(playerStart))
            {
                hasPlayer = false;
            }
        }

        private void EnsureGrid()
        {
            if (tiles == null || tiles.GetLength(0) != width || tiles.GetLength(1) != height)
            {
                CreateFilledGrid(BaseTileType.Floor);
            }
        }

        private void CreateFilledGrid(BaseTileType baseType)
        {
            tiles = new BaseTileType[width, height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    tiles[x, y] = baseType;
                }
            }

            boxes.RemoveWhere(box => !IsInside(box));
            goals.RemoveWhere(goal => !IsInside(goal));
            if (hasPlayer && !IsInside(playerStart))
            {
                hasPlayer = false;
            }
        }

        private static void EnsureLevelsFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/PushBoxz"))
            {
                AssetDatabase.CreateFolder("Assets", "PushBoxz");
            }

            if (!AssetDatabase.IsValidFolder(LevelsFolder))
            {
                AssetDatabase.CreateFolder("Assets/PushBoxz", "Levels");
            }
        }

        private bool IsInside(Vector2Int position)
        {
            return position.x >= 0 && position.y >= 0 && position.x < width && position.y < height;
        }

        private bool IsWalkable(Vector2Int position)
        {
            return IsInside(position) && tiles[position.x, position.y] == BaseTileType.Floor;
        }

        private bool CanEditorPlayerOccupy(Vector2Int position)
        {
            return IsWalkable(position) && !boxes.Contains(position);
        }

        private Color GetTileColor(BaseTileType baseType)
        {
            switch (baseType)
            {
                case BaseTileType.Empty:
                    return emptyColor;
                case BaseTileType.Floor:
                    return floorColor;
                case BaseTileType.Wall:
                    return wallColor;
                default:
                    return Color.magenta;
            }
        }

        private string GetCellLabel(Vector2Int position)
        {
            if (hasPlayer && playerStart == position)
            {
                return "P" + GetDirectionLabel(editorPlayerFacing);
            }

            if (boxes.Contains(position))
            {
                return goals.Contains(position) ? "B*" : "B";
            }

            if (goals.Contains(position))
            {
                return "G";
            }

            return tiles[position.x, position.y] == BaseTileType.Wall ? "W" : string.Empty;
        }

        private static string GetDirectionLabel(Direction direction)
        {
            switch (direction)
            {
                case Direction.Up:
                    return "^";
                case Direction.Down:
                    return "v";
                case Direction.Left:
                    return "<";
                case Direction.Right:
                    return ">";
                default:
                    return string.Empty;
            }
        }

        private static Rect RectInset(Rect rect, float inset)
        {
            return new Rect(rect.x + inset, rect.y + inset, rect.width - inset * 2f, rect.height - inset * 2f);
        }

        private static string FormatPosition(Vector2Int position)
        {
            return "(" + position.x + ", " + position.y + ")";
        }

        private static string SanitizeFileName(string rawName)
        {
            var invalidCharacters = Path.GetInvalidFileNameChars();
            var cleanCharacters = rawName.Trim().Select(c => invalidCharacters.Contains(c) ? '_' : c).ToArray();
            var cleanName = new string(cleanCharacters);
            return string.IsNullOrWhiteSpace(cleanName) ? "LevelData" : cleanName;
        }

        private void SetMessage(string message, MessageType type)
        {
            lastMessage = message;
            lastMessageType = type;
        }

        private void SaveDraftState()
        {
            if (tiles == null)
            {
                return;
            }

            var draft = new DraftState
            {
                assetPath = loadedAsset != null ? AssetDatabase.GetAssetPath(loadedAsset) : string.Empty,
                levelId = levelId,
                displayName = displayName,
                width = width,
                height = height,
                playerX = playerStart.x,
                playerY = playerStart.y,
                hasPlayer = hasPlayer,
                playerFacing = (int)editorPlayerFacing,
                reverseDesignMode = reverseDesignMode,
                activeBrush = (int)activeBrush
            };

            foreach (var box in boxes)
            {
                draft.boxes.Add(new BoxDraft
                {
                    x = box.x,
                    y = box.y
                });
            }

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    draft.tiles.Add(new TileDraft
                    {
                        x = x,
                        y = y,
                        baseType = tiles[x, y],
                        hasGoal = goals.Contains(new Vector2Int(x, y))
                    });
                }
            }

            SessionState.SetString(DraftSessionKey, JsonUtility.ToJson(draft));
        }

        private bool RestoreDraftState()
        {
            var json = SessionState.GetString(DraftSessionKey, string.Empty);
            if (string.IsNullOrEmpty(json))
            {
                return false;
            }

            DraftState draft;
            try
            {
                draft = JsonUtility.FromJson<DraftState>(json);
            }
            catch (ArgumentException)
            {
                return false;
            }

            if (draft == null || draft.width < MinSize || draft.height < MinSize)
            {
                return false;
            }

            loadedAsset = string.IsNullOrEmpty(draft.assetPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<LevelDataAsset>(draft.assetPath);
            levelId = string.IsNullOrEmpty(draft.levelId) ? "level_001" : draft.levelId;
            displayName = string.IsNullOrEmpty(draft.displayName) ? "Level 001" : draft.displayName;
            width = Mathf.Clamp(draft.width, MinSize, MaxSize);
            height = Mathf.Clamp(draft.height, MinSize, MaxSize);
            playerStart = new Vector2Int(draft.playerX, draft.playerY);
            hasPlayer = draft.hasPlayer;
            editorPlayerFacing = Enum.IsDefined(typeof(Direction), draft.playerFacing) ? (Direction)draft.playerFacing : Direction.Up;
            reverseDesignMode = draft.reverseDesignMode;
            activeBrush = Enum.IsDefined(typeof(Brush), draft.activeBrush) ? (Brush)draft.activeBrush : Brush.Floor;

            goals.Clear();
            tiles = new BaseTileType[width, height];
            CreateFilledGrid(BaseTileType.Floor);

            if (draft.tiles != null)
            {
                foreach (var tile in draft.tiles)
                {
                    if (tile != null && tile.x >= 0 && tile.y >= 0 && tile.x < width && tile.y < height)
                    {
                        tiles[tile.x, tile.y] = tile.baseType;
                        if (tile.hasGoal)
                        {
                            goals.Add(new Vector2Int(tile.x, tile.y));
                        }
                    }
                }
            }

            boxes.Clear();
            if (draft.boxes != null)
            {
                foreach (var box in draft.boxes)
                {
                    if (box != null && box.x >= 0 && box.y >= 0 && box.x < width && box.y < height)
                    {
                        boxes.Add(new Vector2Int(box.x, box.y));
                    }
                }
            }

            if (hasPlayer && !IsInside(playerStart))
            {
                hasPlayer = false;
            }

            return true;
        }
    }
}
