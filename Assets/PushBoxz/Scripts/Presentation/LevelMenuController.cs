using System.Collections.Generic;
using System.Linq;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
using PushBoxz.Core;
using PushBoxz.Data;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PushBoxz.Presentation
{
    /// <summary>
    /// Drives the product-facing menu flow: cover screen, level select, gameplay HUD,
    /// completion popup, and the runtime custom-level editor.
    /// </summary>
    public class LevelMenuController : MonoBehaviour
    {
        private const string RuntimeHostName = "PushBoxz Runtime Host";
        private const string ClearKeyPrefix = "PushBoxz.LevelCleared.";
        private const string UnlockKeyPrefix = "PushBoxz.LevelUnlocked.";
        private const string WebLevelCodeCallbackName = nameof(OnWebLevelCodeSubmitted);

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void PushBoxzCopyText(string text);

        [DllImport("__Internal")]
        private static extern void PushBoxzPromptLevelCode(string objectName, string methodName);
#endif

        [SerializeField] private LevelSceneBuilderRegistry registry;
        [SerializeField] private LevelSceneBuilder sceneBuilderTemplate;
        [SerializeField] private int columnsPerPage = 5;
        [SerializeField] private int rowsPerPage = 4;

        [Header("Canvas UI - Cover")]
        [SerializeField] private GameObject coverRoot;
        [SerializeField] private Button startButton;
        [SerializeField] private Button createLevelButton;
        [SerializeField] private Button quitButton;

        [Header("Canvas UI - Level Select")]
        [SerializeField] private GameObject levelSelectRoot;
        [SerializeField] private Transform levelButtonParent;
        [SerializeField] private Button levelButtonPrefab;
        [SerializeField] private Button toggleLevelModeButton;
        [SerializeField] private Button deleteCustomLevelButton;
        [SerializeField] private InputField customLevelCodeInput;
        [SerializeField] private TMP_InputField customLevelCodeTmpInput;
        [SerializeField] private Button loadCustomLevelCodeButton;
        [SerializeField] private Text customLevelCodeMessageText;
        [SerializeField] private TMP_Text customLevelCodeMessageTmpText;
        [SerializeField] private Button previousPageButton;
        [SerializeField] private Button nextPageButton;
        [SerializeField] private Text pageText;
        [SerializeField] private TMP_Text pageTmpText;
        [SerializeField] private Button backToCoverButton;
        [SerializeField] private Vector2 levelButtonSize = new Vector2(120f, 48f);
        [SerializeField] private Vector2 levelButtonSpacing = new Vector2(12f, 12f);

        [Header("Canvas UI - Gameplay HUD")]
        [SerializeField] private GameObject gameplayHudRoot;
        [SerializeField] private Text currentLevelText;
        [SerializeField] private TMP_Text currentLevelTmpText;
        [SerializeField] private Text stepCountText;
        [SerializeField] private TMP_Text stepCountTmpText;
        [SerializeField] private Button undoButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button returnToMenuButton;
        [SerializeField] private Button shareButton;

        [Header("Canvas UI - Level Complete")]
        [SerializeField] private GameObject levelCompleteRoot;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button nextLevelButton;

        [Header("Canvas UI - Custom Level Editor")]
        [SerializeField] private GameObject customLevelEditorRoot;
        [SerializeField] private InputField customLevelIdInput;
        [SerializeField] private TMP_InputField customLevelIdTmpInput;
        [SerializeField] private Slider customSizeSlider;
        [SerializeField] private Text customSizeValueText;
        [SerializeField] private TMP_Text customSizeValueTmpText;
        [SerializeField] private Button wallBrushButton;
        [SerializeField] private Button goalBrushButton;
        [SerializeField] private Button boxBrushButton;
        [SerializeField] private Button playerBrushButton;
        [SerializeField] private Button eraseBrushButton;
        [SerializeField] private Button customInteractionModeButton;
        [SerializeField] private Transform customGridParent;
        [SerializeField] private Button customGridCellPrefab;
        [SerializeField] private Sprite customWallCellSprite;
        [SerializeField] private Sprite customGoalCellSprite;
        [SerializeField] private Sprite customBoxCellSprite;
        [SerializeField] private Sprite customPlayerCellSprite;
        [SerializeField] private Sprite customEraseCellSprite;
        [SerializeField] private Text customEditorMessageText;
        [SerializeField] private TMP_Text customEditorMessageTmpText;
        [SerializeField] private Button customEditorBackButton;
        [SerializeField] private Button customEditorSaveButton;

        private readonly List<LevelEntry> levelEntries = new List<LevelEntry>();
        private readonly List<Button> spawnedLevelButtons = new List<Button>();
        private readonly List<Button> spawnedCustomGridButtons = new List<Button>();
        private GameObject runtimeHost;
        private GameSession activeSession;
        private LevelEntry activeEntry;
        private LevelEntry completedNextEntry;
        private AudioSource uiAudioSource;
        private int currentPage;
        private LevelListMode levelListMode = LevelListMode.Official;
        private bool customLevelDeleteMode;
        private bool activeLevelCompletionHandled;
        private bool levelSelectDirty = true;
        private MenuScreen screen = MenuScreen.Cover;

        // Runtime-created levels are kept separate from official LevelDataAsset entries.
        // This lets players build and delete creative-mode levels without touching project assets.
        private int customWidth = 5;
        private int customHeight = 5;
        private string customLevelId = string.Empty;
        private BaseTileType[,] customTiles;
        private readonly HashSet<Vector2Int> customGoals = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> customBoxes = new HashSet<Vector2Int>();
        private Vector2Int customPlayerStart = Vector2Int.zero;
        private bool customHasPlayer;
        private CustomBrush customBrush = CustomBrush.Wall;
        private Direction customPlayerFacing = Direction.Up;
        private CustomInteractionMode customInteractionMode = CustomInteractionMode.Push;
        private bool customInteractionModeTouched;
        private string customEditorMessage = "Create a level, then save it to Creative Mode.";
        private string customLevelCodeInputText = string.Empty;
        private string customLevelCodeMessage = string.Empty;
        private bool customGridPainting;

        private const int MinCustomLevelSize = 2;
        private const int MaxCustomLevelSize = 12;

        private enum MenuScreen
        {
            Cover,
            LevelSelect,
            Playing,
            CustomLevelEditor
        }

        private enum LevelListMode
        {
            Official,
            Custom
        }

        private enum CustomBrush
        {
            Wall,
            Goal,
            Box,
            Player,
            Erase
        }

        private enum CustomInteractionMode
        {
            Push,
            Pull
        }

        private class LevelEntry
        {
            public LevelSceneBuilderRegistryEntry registryEntry;
            public LevelDataAsset level;
            public bool isCustom;
        }

        public LevelSceneBuilderRegistry Registry
        {
            get { return registry; }
            set { registry = value; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapMenu()
        {
            if (FindObjectOfType<LevelMenuController>() != null)
            {
                return;
            }

            var loadedRegistry = Resources.Load<LevelSceneBuilderRegistry>("LevelSceneBuilderRegistry");
            if (loadedRegistry == null)
            {
                return;
            }

            var menuObject = new GameObject("PushBoxz Level Menu");
            var controller = menuObject.AddComponent<LevelMenuController>();
            controller.Registry = loadedRegistry;
        }

        private void Awake()
        {
            columnsPerPage = Mathf.Max(1, columnsPerPage);
            rowsPerPage = Mathf.Max(1, rowsPerPage);

            if (registry == null)
            {
                registry = Resources.Load<LevelSceneBuilderRegistry>("LevelSceneBuilderRegistry");
            }

            ApplyGlobalTmpFontToCanvas();
            EnsureCustomGrid();
            RefreshLevelEntries();
            DestroyRuntimeHost();
            BindCoverButtons();
            BindLevelSelectButtons();
            BindGameplayHudButtons();
            BindLevelCompleteButtons();
            BindCustomLevelEditorButtons();
            MarkLevelSelectDirty();
            SyncCanvasUi();
        }

        private void OnDestroy()
        {
            UnbindCoverButtons();
            UnbindLevelSelectButtons();
            UnbindGameplayHudButtons();
            UnbindLevelCompleteButtons();
            UnbindCustomLevelEditorButtons();
            ClearSpawnedLevelButtons();
            ClearSpawnedCustomGridButtons();
        }

        private void Update()
        {
            if (customGridPainting && !Input.GetMouseButton(0))
            {
                EndCustomGridPaint();
            }

            if (screen == MenuScreen.CustomLevelEditor)
            {
                HandleCustomEditorKeyboardInput();
            }
        }

        private void OnGUI()
        {
            // OnGUI remains as a safety fallback for development scenes that have not
            // wired the Canvas references yet; production UI should use the Canvas fields.
            SyncCanvasUi();
            switch (screen)
            {
                case MenuScreen.Cover:
                    if (!HasCanvasCover())
                    {
                        DrawCover();
                    }

                    break;
                case MenuScreen.LevelSelect:
                    if (!HasCanvasLevelSelect())
                    {
                        DrawLevelSelect();
                    }

                    break;
                case MenuScreen.Playing:
                    if (!HasCanvasGameplayHud())
                    {
                        DrawGameHud();
                    }

                    if (activeSession != null && activeSession.IsCompleted)
                    {
                        HandleLevelCompleted();
                    }
                    break;
                case MenuScreen.CustomLevelEditor:
                    if (customLevelEditorRoot == null)
                    {
                        Debug.LogWarning("Custom Level Editor UI is not assigned.", this);
                        ReturnToCover();
                    }

                    break;
            }
        }

        private void DrawCover()
        {
            var rect = CenteredRect(360f, 220f);
            GUILayout.BeginArea(rect, GUI.skin.box);
            GUILayout.FlexibleSpace();
            GUILayout.Label("PushBoxz", TitleStyle());
            GUILayout.Space(24f);

            if (GUILayout.Button("Start Game", GUILayout.Height(42f)))
            {
                PlayUiButtonSound();
                StartGameFromCover();
            }

            GUILayout.Space(8f);
            if (GUILayout.Button("Create Level", GUILayout.Height(42f)))
            {
                PlayUiButtonSound();
                OpenCustomLevelEditor();
            }

            GUILayout.Space(8f);
            if (GUILayout.Button("Quit", GUILayout.Height(42f)))
            {
                PlayUiButtonSound();
                QuitGame();
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndArea();
        }

        private void DrawLevelSelect()
        {
            RefreshLevelEntries();

            const float buttonWidth = 96f;
            const float buttonHeight = 42f;
            const float gap = 8f;
            var pageSize = Mathf.Max(1, columnsPerPage * rowsPerPage);
            var pageCount = Mathf.Max(1, Mathf.CeilToInt(levelEntries.Count / (float)pageSize));
            currentPage = Mathf.Clamp(currentPage, 0, pageCount - 1);

            var panelWidth = Mathf.Max(520f, columnsPerPage * buttonWidth + (columnsPerPage - 1) * gap + 64f);
            var panelHeight = Mathf.Max(360f, rowsPerPage * buttonHeight + (rowsPerPage - 1) * gap + 150f);
            var rect = CenteredRect(panelWidth, panelHeight);

            GUILayout.BeginArea(rect, GUI.skin.box);
            GUILayout.Label(levelListMode == LevelListMode.Official ? "Story Mode" : "Creative Mode", TitleStyle());
            if (GUILayout.Button(levelListMode == LevelListMode.Official ? "Show Creative Levels" : "Show Story Levels", GUILayout.Height(30f)))
            {
                PlayUiButtonSound();
                ToggleLevelListMode();
            }

            if (levelListMode == LevelListMode.Custom
                && GUILayout.Button(customLevelDeleteMode ? "Select" : "Delete", GUILayout.Height(30f)))
            {
                PlayUiButtonSound();
                ToggleCustomLevelDeleteMode();
            }

            if (levelListMode == LevelListMode.Custom)
            {
                GUILayout.Space(6f);
                GUILayout.Label("Import Level Code");
                customLevelCodeInputText = GUILayout.TextField(customLevelCodeInputText ?? string.Empty);
                if (GUILayout.Button("Load", GUILayout.Height(30f)))
                {
                    PlayUiButtonSound();
                    LoadCustomLevelCode();
                }

                if (!string.IsNullOrEmpty(customLevelCodeMessage))
                {
                    GUILayout.Label(customLevelCodeMessage);
                }
            }

            GUILayout.Space(12f);

            if (levelEntries.Count == 0)
            {
                GUILayout.Label(levelListMode == LevelListMode.Official
                    ? "No enabled story levels. Configure levels in Level Registry."
                    : "No creative levels saved yet.");
            }
            else
            {
                var start = currentPage * pageSize;
                for (var row = 0; row < rowsPerPage; row++)
                {
                    using (new GUILayout.HorizontalScope())
                    {
                        GUILayout.FlexibleSpace();
                        for (var column = 0; column < columnsPerPage; column++)
                        {
                            var index = start + row * columnsPerPage + column;
                            if (index >= levelEntries.Count)
                            {
                                GUILayout.Space(buttonWidth + gap);
                                continue;
                            }

                            DrawLevelButton(index, buttonWidth, buttonHeight);
                            if (column < columnsPerPage - 1)
                            {
                                GUILayout.Space(gap);
                            }
                        }

                        GUILayout.FlexibleSpace();
                    }

                    GUILayout.Space(gap);
                }
            }

            GUILayout.FlexibleSpace();
            using (new GUILayout.HorizontalScope())
            {
                GUI.enabled = currentPage > 0;
                if (GUILayout.Button("Previous", GUILayout.Height(34f)))
                {
                    PlayUiButtonSound();
                    currentPage--;
                }

                GUI.enabled = true;
                GUILayout.Label((currentPage + 1) + " / " + pageCount, CenterLabelStyle(), GUILayout.Width(80f));

                GUI.enabled = currentPage < pageCount - 1;
                if (GUILayout.Button("Next", GUILayout.Height(34f)))
                {
                    PlayUiButtonSound();
                    currentPage++;
                }

                GUI.enabled = true;
            }

            if (GUILayout.Button("Back", GUILayout.Height(32f)))
            {
                PlayUiButtonSound();
                screen = MenuScreen.Cover;
                SyncCanvasUi();
            }

            GUILayout.EndArea();
        }

        private void DrawLevelButton(int index, float width, float height)
        {
            var entry = levelEntries[index];
            var unlocked = IsLevelUnlocked(index);
            var cleared = IsLevelCleared(entry.level);
            var label = GetLevelLabel(entry.level);
            if (!unlocked)
            {
                label += "\nLocked";
            }
            else if (cleared)
            {
                label += "\nCleared";
            }

            GUI.enabled = unlocked;
            if (GUILayout.Button(label, GUILayout.Width(width), GUILayout.Height(height)))
            {
                PlayUiButtonSound();
                if (customLevelDeleteMode && entry.isCustom)
                {
                    DeleteCustomLevel(entry);
                    return;
                }

                StartLevel(entry);
            }

            GUI.enabled = true;
        }

        private string GetCustomCellLabel(Vector2Int position)
        {
            if (customHasPlayer && customPlayerStart == position)
            {
                return GetCustomPlayerLabel();
            }

            var hasBox = customBoxes.Contains(position);
            var hasGoal = customGoals.Contains(position);
            if (hasBox && hasGoal)
            {
                return "B+G";
            }

            if (hasBox)
            {
                return "B";
            }

            if (hasGoal)
            {
                return "G";
            }

            if (customTiles[position.x, position.y] == BaseTileType.Wall)
            {
                return "W";
            }

            return ".";
        }

        private void PaintCustomCell(Vector2Int position)
        {
            if (!IsInsideCustomGrid(position))
            {
                return;
            }

            switch (customBrush)
            {
                case CustomBrush.Wall:
                    customTiles[position.x, position.y] = BaseTileType.Wall;
                    customGoals.Remove(position);
                    customBoxes.Remove(position);
                    if (customHasPlayer && customPlayerStart == position)
                    {
                        customHasPlayer = false;
                    }
                    break;
                case CustomBrush.Goal:
                    customTiles[position.x, position.y] = BaseTileType.Floor;
                    customGoals.Add(position);
                    break;
                case CustomBrush.Box:
                    customTiles[position.x, position.y] = BaseTileType.Floor;
                    if (!(customHasPlayer && customPlayerStart == position))
                    {
                        customBoxes.Add(position);
                    }
                    break;
                case CustomBrush.Player:
                    customTiles[position.x, position.y] = BaseTileType.Floor;
                    customBoxes.Remove(position);
                    customPlayerStart = position;
                    customHasPlayer = true;
                    break;
                case CustomBrush.Erase:
                    customTiles[position.x, position.y] = BaseTileType.Floor;
                    customGoals.Remove(position);
                    customBoxes.Remove(position);
                    if (customHasPlayer && customPlayerStart == position)
                    {
                        customHasPlayer = false;
                    }
                    break;
            }

            SyncCustomGridButtons();
        }

        private void PaintCustomCellFromPointer(Vector2Int position)
        {
            PlayUiButtonSound();
            PaintCustomCell(position);
        }

        private void BeginCustomGridPaint(Vector2Int position)
        {
            customGridPainting = true;
            PaintCustomCellFromPointer(position);
        }

        private void ContinueCustomGridPaint(Vector2Int position)
        {
            if (!customGridPainting || !Input.GetMouseButton(0))
            {
                return;
            }

            PaintCustomCell(position);
        }

        private void EndCustomGridPaint()
        {
            customGridPainting = false;
        }

        private void HandleCustomEditorKeyboardInput()
        {
            // UI5 mirrors the editor's fast design workflow: WASD changes the in-grid
            // player position and F tests the selected push/pull interaction immediately.
            if (IsTypingInInputField())
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.W))
            {
                TryMoveCustomPlayer(Direction.Up);
            }
            else if (Input.GetKeyDown(KeyCode.S))
            {
                TryMoveCustomPlayer(Direction.Down);
            }
            else if (Input.GetKeyDown(KeyCode.A))
            {
                TryMoveCustomPlayer(Direction.Left);
            }
            else if (Input.GetKeyDown(KeyCode.D))
            {
                TryMoveCustomPlayer(Direction.Right);
            }
            else if (Input.GetKeyDown(KeyCode.F))
            {
                TryCustomPlayerInteract();
            }
        }

        private void TryMoveCustomPlayer(Direction direction)
        {
            customPlayerFacing = direction;
            if (!customHasPlayer)
            {
                customEditorMessage = "Place a player first.";
                SyncCustomEditorUi();
                return;
            }

            var target = customPlayerStart + DirectionUtility.ToOffset(direction);
            if (!CanCustomPlayerOccupy(target))
            {
                customEditorMessage = "Player is blocked.";
                SyncCustomEditorUi();
                return;
            }

            customPlayerStart = target;
            customEditorMessage = "Player moved.";
            SyncCustomEditorUi();
        }

        private void TryCustomPlayerInteract()
        {
            if (!customHasPlayer)
            {
                customEditorMessage = "Place a player first.";
                SyncCustomEditorUi();
                return;
            }

            if (customInteractionMode == CustomInteractionMode.Push)
            {
                TryCustomPushBox();
            }
            else
            {
                TryCustomPullBox();
            }
        }

        private void TryCustomPushBox()
        {
            var offset = DirectionUtility.ToOffset(customPlayerFacing);
            var boxPosition = customPlayerStart + offset;
            var boxTarget = boxPosition + offset;
            if (!customBoxes.Contains(boxPosition))
            {
                customEditorMessage = "No box in front.";
                SyncCustomEditorUi();
                return;
            }

            if (!CanCustomBoxOccupy(boxTarget))
            {
                customEditorMessage = "Push is blocked.";
                SyncCustomEditorUi();
                return;
            }

            customBoxes.Remove(boxPosition);
            customBoxes.Add(boxTarget);
            customPlayerStart = boxPosition;
            customEditorMessage = "Box pushed.";
            SyncCustomEditorUi();
        }

        private void TryCustomPullBox()
        {
            var offset = DirectionUtility.ToOffset(customPlayerFacing);
            var boxPosition = customPlayerStart + offset;
            var playerTarget = customPlayerStart - offset;
            if (!customBoxes.Contains(boxPosition))
            {
                customEditorMessage = "No box in front.";
                SyncCustomEditorUi();
                return;
            }

            if (!CanCustomPlayerOccupy(playerTarget))
            {
                customEditorMessage = "Pull is blocked.";
                SyncCustomEditorUi();
                return;
            }

            customBoxes.Remove(boxPosition);
            customBoxes.Add(customPlayerStart);
            customPlayerStart = playerTarget;
            customEditorMessage = "Box pulled.";
            SyncCustomEditorUi();
        }

        private string GetCustomPlayerLabel()
        {
            switch (customPlayerFacing)
            {
                case Direction.Up:
                    return "↑";
                case Direction.Down:
                    return "↓";
                case Direction.Left:
                    return "←";
                case Direction.Right:
                    return "→";
                default:
                    return "↑";
            }
        }

        private bool CanCustomPlayerOccupy(Vector2Int position)
        {
            return IsInsideCustomGrid(position)
                && customTiles[position.x, position.y] == BaseTileType.Floor
                && !customBoxes.Contains(position);
        }

        private bool CanCustomBoxOccupy(Vector2Int position)
        {
            return IsInsideCustomGrid(position)
                && customTiles[position.x, position.y] == BaseTileType.Floor
                && !customBoxes.Contains(position)
                && !(customHasPlayer && customPlayerStart == position);
        }

        private static bool IsTypingInInputField()
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            return selected != null
                && (selected.GetComponent<InputField>() != null || selected.GetComponent<TMP_InputField>() != null);
        }

        private void DrawGameHud()
        {
            const int padding = 16;
            var rect = new Rect(padding, padding, 360f, 150f);
            GUILayout.BeginArea(rect, GUI.skin.box);

            GUILayout.Label(activeEntry != null ? GetLevelLabel(activeEntry.level) : "No Level Selected", TitleStyle(16));
            GUILayout.Label(activeSession != null ? "Steps: " + activeSession.StepCount : "Steps: 0");
            GUILayout.Space(8f);

            using (new GUILayout.HorizontalScope())
            {
                GUI.enabled = activeSession != null;
                if (GUILayout.Button("Undo", GUILayout.Height(34f)))
                {
                    PlayUiButtonSound();
                    UndoActiveStep();
                }

                if (GUILayout.Button("Restart", GUILayout.Height(34f)))
                {
                    PlayUiButtonSound();
                    RestartActiveLevel();
                }

                GUI.enabled = true;
                if (GUILayout.Button("Menu", GUILayout.Height(34f)))
                {
                    PlayUiButtonSound();
                    ReturnToMenu();
                }
            }

            GUILayout.EndArea();
        }

        private void HandleLevelCompleted()
        {
            if (activeEntry == null || activeEntry.level == null)
            {
                return;
            }

            if (activeLevelCompletionHandled)
            {
                return;
            }

            activeLevelCompletionHandled = true;
            if (activeEntry.isCustom)
            {
                completedNextEntry = null;
                SyncCanvasUi();
                return;
            }

            MarkLevelCleared(activeEntry.level);
            completedNextEntry = null;
            RefreshLevelEntries();
            var activeIndex = FindLevelEntryIndex(activeEntry);
            if (activeIndex >= 0 && activeIndex + 1 < levelEntries.Count)
            {
                completedNextEntry = levelEntries[activeIndex + 1];
                SetRegistryEntryUnlocked(completedNextEntry.level);
                MarkLevelUnlocked(completedNextEntry.level);
            }

            SyncCanvasUi();
        }

        private void StartLevel(LevelEntry entry)
        {
            if (entry == null || entry.level == null)
            {
                return;
            }

            // A fresh runtime host is created for every level load so old terrain,
            // objects, events, and undo history cannot leak into the next level.
            DestroyRuntimeHost();

            runtimeHost = new GameObject(RuntimeHostName);
            var builder = runtimeHost.AddComponent<LevelSceneBuilder>();
            builder.CopyConfigurationFrom(GetSceneBuilderTemplate());
            builder.ApplyRegistryAssets(registry);
            activeSession = runtimeHost.AddComponent<GameSession>();
            runtimeHost.AddComponent<PlayerInputController>();

            activeEntry = entry;
            completedNextEntry = null;
            activeLevelCompletionHandled = false;
            activeSession.BuildOnStart = false;
            activeSession.LevelData = entry.level;
            activeSession.RestartLevel();
            screen = MenuScreen.Playing;
            SyncCanvasUi();
        }

        private void ReturnToMenu()
        {
            DestroyRuntimeHost();
            activeEntry = null;
            completedNextEntry = null;
            activeSession = null;
            activeLevelCompletionHandled = false;
            RefreshLevelEntries();
            screen = MenuScreen.LevelSelect;
            MarkLevelSelectDirty();
            SyncCanvasUi();
        }

        private void StartGameFromCover()
        {
            levelListMode = LevelListMode.Official;
            customLevelDeleteMode = false;
            RefreshLevelEntries();
            currentPage = 0;
            screen = MenuScreen.LevelSelect;
            MarkLevelSelectDirty();
            SyncCanvasUi();
        }

        private void ReturnToCover()
        {
            customLevelDeleteMode = false;
            screen = MenuScreen.Cover;
            SyncCanvasUi();
        }

        private void OpenCustomLevelEditor()
        {
            EnsureCustomGrid();
            customInteractionModeTouched = false;
            customEditorMessage = "Create a level, then save it to Creative Mode.";
            screen = MenuScreen.CustomLevelEditor;
            SyncCustomEditorUi();
            SyncCanvasUi();
        }

        private void ToggleLevelListMode()
        {
            levelListMode = levelListMode == LevelListMode.Official ? LevelListMode.Custom : LevelListMode.Official;
            customLevelDeleteMode = false;
            currentPage = 0;
            RefreshLevelEntries();
            MarkLevelSelectDirty();
            SyncCanvasUi();
        }

        private void ToggleCustomLevelDeleteMode()
        {
            if (levelListMode != LevelListMode.Custom)
            {
                customLevelDeleteMode = false;
                SyncCanvasUi();
                return;
            }

            // Delete mode is intentionally limited to creative-mode entries;
            // official levels remain managed by the project registry.
            customLevelDeleteMode = !customLevelDeleteMode;
            MarkLevelSelectDirty();
            SyncCanvasUi();
        }

        private void DeleteCustomLevel(LevelEntry entry)
        {
            if (entry == null || !entry.isCustom || entry.level == null)
            {
                return;
            }

            var levelId = entry.level.levelId;
            if (!CustomLevelStorage.DeleteLevel(levelId))
            {
                return;
            }

            if (activeEntry != null && activeEntry.level != null && activeEntry.level.levelId == levelId)
            {
                activeEntry = null;
            }

            RefreshLevelEntries();
            var pageCount = GetLevelSelectPageCount();
            currentPage = Mathf.Clamp(currentPage, 0, pageCount - 1);
            if (levelEntries.Count == 0)
            {
                customLevelDeleteMode = false;
            }

            MarkLevelSelectDirty();
            SyncCanvasUi();
        }

        private void SaveCustomLevel()
        {
            if (TryLoadCustomLevelCodeIntoEditor())
            {
                return;
            }

            var level = BuildCustomLevelData();
            if (!CustomLevelStorage.ValidatePlayable(level, out var message))
            {
                customEditorMessage = message;
                Destroy(level);
                return;
            }

            var savedLevel = CustomLevelStorage.SaveLevel(level);
            Destroy(level);
            if (savedLevel == null)
            {
                customEditorMessage = "Save failed.";
                return;
            }

            customLevelId = savedLevel.levelId;
            var copiedCode = TryCopyLevelCodeToClipboard(savedLevel, out var copyMessage);
            customEditorMessage = copiedCode
                ? "Saved and copied level code: " + customLevelId
                : "Saved to Creative Mode: " + customLevelId + ". " + copyMessage;
            levelListMode = LevelListMode.Custom;
            RefreshLevelEntries();
            MarkLevelSelectDirty();
            SyncCustomEditorUi();
        }

        private bool TryLoadCustomLevelCodeIntoEditor()
        {
            var rawInput = customLevelId != null ? customLevelId.Trim() : string.Empty;
            if (!LooksLikeLevelCode(rawInput))
            {
                return false;
            }

            if (!CustomLevelCodeCodec.TryDecode(rawInput, out var level, out var message))
            {
                customEditorMessage = message;
                SyncCustomEditorUi();
                return true;
            }

            LoadCustomLevelIntoEditor(level);
            Destroy(level);
            customEditorMessage = "Level code loaded into editor.";
            SyncCustomEditorUi();
            return true;
        }

        private static bool LooksLikeLevelCode(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var trimmed = value.Trim();
            return trimmed.StartsWith("PBZ1", System.StringComparison.OrdinalIgnoreCase);
        }

        private void LoadCustomLevelIntoEditor(LevelDataAsset level)
        {
            if (level == null)
            {
                return;
            }

            var size = Mathf.Clamp(Mathf.Max(level.width, level.height), MinCustomLevelSize, MaxCustomLevelSize);
            ResizeCustomGrid(size, size);
            for (var y = 0; y < customHeight; y++)
            {
                for (var x = 0; x < customWidth; x++)
                {
                    customTiles[x, y] = BaseTileType.Floor;
                }
            }

            customGoals.Clear();
            if (level.cells != null)
            {
                for (var i = 0; i < level.cells.Count; i++)
                {
                    var cell = level.cells[i];
                    if (cell == null || cell.x < 0 || cell.y < 0 || cell.x >= customWidth || cell.y >= customHeight)
                    {
                        continue;
                    }

                    customTiles[cell.x, cell.y] = cell.baseType;
                    if (cell.hasGoal)
                    {
                        customGoals.Add(new Vector2Int(cell.x, cell.y));
                    }
                }
            }

            customBoxes.Clear();
            if (level.boxStarts != null)
            {
                for (var i = 0; i < level.boxStarts.Count; i++)
                {
                    var box = level.boxStarts[i];
                    if (IsInsideCustomGrid(box))
                    {
                        customBoxes.Add(box);
                    }
                }
            }

            customPlayerStart = level.playerStart;
            customHasPlayer = IsInsideCustomGrid(customPlayerStart);
            customPlayerFacing = Direction.Up;
            customLevelId = string.Empty;
            RebuildCustomGridButtons();
        }

        private void ShareActiveLevelCode()
        {
            if (activeEntry == null || activeEntry.level == null)
            {
                Debug.LogWarning("No active level to share.", this);
                return;
            }

            if (!TryCopyLevelCodeToClipboard(activeEntry.level, out var message))
            {
                Debug.LogWarning("Failed to copy level code: " + message, this);
                return;
            }

            Debug.Log(message, this);
        }

        private bool TryCopyLevelCodeToClipboard(LevelDataAsset level, out string message)
        {
            if (!CustomLevelCodeCodec.TryEncode(level, out var code, out message))
            {
                return false;
            }

            GUIUtility.systemCopyBuffer = code;
#if UNITY_WEBGL && !UNITY_EDITOR
            PushBoxzCopyText(code);
#endif
            message = "Level code copied: " + code;
            return true;
        }

        private void LoadCustomLevelCode()
        {
            customLevelCodeInputText = GetCustomLevelCodeInputText();
            if (string.IsNullOrWhiteSpace(customLevelCodeInputText) && TryOpenWebLevelCodePrompt())
            {
                return;
            }

            LoadCustomLevelCode(customLevelCodeInputText);
        }

        private void LoadCustomLevelCode(string code)
        {
            customLevelCodeInputText = code;
            if (!CustomLevelCodeCodec.TryDecode(customLevelCodeInputText, out var importedLevel, out var message))
            {
                customLevelCodeMessage = message;
                SyncCustomLevelCodeImportUi();
                return;
            }

            var savedLevel = CustomLevelStorage.SaveLevel(importedLevel);
            Destroy(importedLevel);
            if (savedLevel == null)
            {
                customLevelCodeMessage = "Load failed.";
                SyncCustomLevelCodeImportUi();
                return;
            }

            var savedId = savedLevel.levelId;
            Destroy(savedLevel);
            customLevelCodeInputText = string.Empty;
            customLevelCodeMessage = "Loaded: " + savedId;
            levelListMode = LevelListMode.Custom;
            customLevelDeleteMode = false;
            RefreshLevelEntries();
            currentPage = GetPageForLevelId(savedId);
            MarkLevelSelectDirty();
            SyncCanvasUi();
        }

        private string GetCustomLevelCodeInputText()
        {
            if (customLevelCodeTmpInput != null)
            {
                return customLevelCodeTmpInput.text;
            }

            return customLevelCodeInput != null ? customLevelCodeInput.text : customLevelCodeInputText;
        }

        private bool TryOpenWebLevelCodePrompt()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            PushBoxzPromptLevelCode(gameObject.name, WebLevelCodeCallbackName);
            return true;
#else
            return false;
#endif
        }

        public void OnWebLevelCodeSubmitted(string code)
        {
            customLevelCodeInputText = code;
            LoadCustomLevelCode(code);
        }

        private void StartNextLevel()
        {
            if (completedNextEntry == null || completedNextEntry.level == null)
            {
                return;
            }

            StartLevel(completedNextEntry);
        }

        private static void QuitGame()
        {
            Application.Quit();
        }

        private bool HasCanvasCover()
        {
            return coverRoot != null;
        }

        private bool HasCanvasLevelSelect()
        {
            return levelSelectRoot != null;
        }

        private bool HasCanvasGameplayHud()
        {
            return gameplayHudRoot != null;
        }

        private void EnsureCustomGrid()
        {
            if (customTiles != null && customTiles.GetLength(0) == customWidth && customTiles.GetLength(1) == customHeight)
            {
                return;
            }

            ResizeCustomGrid(customWidth, customHeight);
        }

        private void ResizeCustomGrid(int nextWidth, int nextHeight)
        {
            nextWidth = Mathf.Clamp(nextWidth, MinCustomLevelSize, MaxCustomLevelSize);
            nextHeight = Mathf.Clamp(nextHeight, MinCustomLevelSize, MaxCustomLevelSize);

            var nextTiles = new BaseTileType[nextWidth, nextHeight];
            for (var y = 0; y < nextHeight; y++)
            {
                for (var x = 0; x < nextWidth; x++)
                {
                    nextTiles[x, y] = BaseTileType.Floor;
                    if (customTiles != null && x < customWidth && y < customHeight)
                    {
                        nextTiles[x, y] = customTiles[x, y];
                    }
                }
            }

            customWidth = nextWidth;
            customHeight = nextHeight;
            customTiles = nextTiles;
            customGoals.RemoveWhere(goal => !IsInsideCustomGrid(goal));
            customBoxes.RemoveWhere(box => !IsInsideCustomGrid(box));
            if (customHasPlayer && !IsInsideCustomGrid(customPlayerStart))
            {
                customHasPlayer = false;
            }
        }

        private bool IsInsideCustomGrid(Vector2Int position)
        {
            return position.x >= 0 && position.y >= 0 && position.x < customWidth && position.y < customHeight;
        }

        private LevelDataAsset BuildCustomLevelData()
        {
            EnsureCustomGrid();

            var cells = new List<TileCell>(customWidth * customHeight);
            for (var y = 0; y < customHeight; y++)
            {
                for (var x = 0; x < customWidth; x++)
                {
                    cells.Add(new TileCell(x, y, customTiles[x, y], customGoals.Contains(new Vector2Int(x, y))));
                }
            }

            var levelId = string.IsNullOrWhiteSpace(customLevelId) ? null : customLevelId.Trim();
            return CustomLevelStorage.CreateRuntimeLevel(
                levelId,
                customWidth,
                customHeight,
                customHasPlayer ? customPlayerStart : new Vector2Int(-1, -1),
                cells,
                customBoxes);
        }

        private void BindCoverButtons()
        {
            if (startButton != null)
            {
                startButton.onClick.RemoveListener(OnStartButtonClicked);
                startButton.onClick.AddListener(OnStartButtonClicked);
            }

            if (createLevelButton != null)
            {
                createLevelButton.onClick.RemoveListener(OnCreateLevelButtonClicked);
                createLevelButton.onClick.AddListener(OnCreateLevelButtonClicked);
            }

            if (quitButton != null)
            {
                quitButton.onClick.RemoveListener(OnQuitButtonClicked);
                quitButton.onClick.AddListener(OnQuitButtonClicked);
            }
        }

        private void UnbindCoverButtons()
        {
            if (startButton != null)
            {
                startButton.onClick.RemoveListener(OnStartButtonClicked);
            }

            if (createLevelButton != null)
            {
                createLevelButton.onClick.RemoveListener(OnCreateLevelButtonClicked);
            }

            if (quitButton != null)
            {
                quitButton.onClick.RemoveListener(OnQuitButtonClicked);
            }
        }

        private void BindLevelSelectButtons()
        {
            if (toggleLevelModeButton != null)
            {
                toggleLevelModeButton.onClick.RemoveListener(OnToggleLevelModeButtonClicked);
                toggleLevelModeButton.onClick.AddListener(OnToggleLevelModeButtonClicked);
            }

            if (deleteCustomLevelButton != null)
            {
                deleteCustomLevelButton.onClick.RemoveListener(OnDeleteCustomLevelButtonClicked);
                deleteCustomLevelButton.onClick.AddListener(OnDeleteCustomLevelButtonClicked);
            }

            if (loadCustomLevelCodeButton != null)
            {
                loadCustomLevelCodeButton.onClick.RemoveListener(OnLoadCustomLevelCodeButtonClicked);
                loadCustomLevelCodeButton.onClick.AddListener(OnLoadCustomLevelCodeButtonClicked);
            }

            if (customLevelCodeInput != null)
            {
                customLevelCodeInput.onValueChanged.RemoveListener(OnCustomLevelCodeInputChanged);
                customLevelCodeInput.onValueChanged.AddListener(OnCustomLevelCodeInputChanged);
            }

            if (customLevelCodeTmpInput != null)
            {
                customLevelCodeTmpInput.onValueChanged.RemoveListener(OnCustomLevelCodeInputChanged);
                customLevelCodeTmpInput.onValueChanged.AddListener(OnCustomLevelCodeInputChanged);
            }

            if (previousPageButton != null)
            {
                previousPageButton.onClick.RemoveListener(OnPreviousPageButtonClicked);
                previousPageButton.onClick.AddListener(OnPreviousPageButtonClicked);
            }

            if (nextPageButton != null)
            {
                nextPageButton.onClick.RemoveListener(OnNextPageButtonClicked);
                nextPageButton.onClick.AddListener(OnNextPageButtonClicked);
            }

            if (backToCoverButton != null)
            {
                backToCoverButton.onClick.RemoveListener(OnBackToCoverButtonClicked);
                backToCoverButton.onClick.AddListener(OnBackToCoverButtonClicked);
            }
        }

        private void UnbindLevelSelectButtons()
        {
            if (toggleLevelModeButton != null)
            {
                toggleLevelModeButton.onClick.RemoveListener(OnToggleLevelModeButtonClicked);
            }

            if (deleteCustomLevelButton != null)
            {
                deleteCustomLevelButton.onClick.RemoveListener(OnDeleteCustomLevelButtonClicked);
            }

            if (loadCustomLevelCodeButton != null)
            {
                loadCustomLevelCodeButton.onClick.RemoveListener(OnLoadCustomLevelCodeButtonClicked);
            }

            if (customLevelCodeInput != null)
            {
                customLevelCodeInput.onValueChanged.RemoveListener(OnCustomLevelCodeInputChanged);
            }

            if (customLevelCodeTmpInput != null)
            {
                customLevelCodeTmpInput.onValueChanged.RemoveListener(OnCustomLevelCodeInputChanged);
            }

            if (previousPageButton != null)
            {
                previousPageButton.onClick.RemoveListener(OnPreviousPageButtonClicked);
            }

            if (nextPageButton != null)
            {
                nextPageButton.onClick.RemoveListener(OnNextPageButtonClicked);
            }

            if (backToCoverButton != null)
            {
                backToCoverButton.onClick.RemoveListener(OnBackToCoverButtonClicked);
            }
        }

        private void BindGameplayHudButtons()
        {
            if (undoButton != null)
            {
                undoButton.onClick.RemoveListener(OnUndoButtonClicked);
                undoButton.onClick.AddListener(OnUndoButtonClicked);
            }

            if (restartButton != null)
            {
                restartButton.onClick.RemoveListener(OnRestartButtonClicked);
                restartButton.onClick.AddListener(OnRestartButtonClicked);
            }

            if (returnToMenuButton != null)
            {
                returnToMenuButton.onClick.RemoveListener(OnReturnToMenuButtonClicked);
                returnToMenuButton.onClick.AddListener(OnReturnToMenuButtonClicked);
            }

            if (shareButton != null)
            {
                shareButton.onClick.RemoveListener(OnShareButtonClicked);
                shareButton.onClick.AddListener(OnShareButtonClicked);
            }
        }

        private void UnbindGameplayHudButtons()
        {
            if (undoButton != null)
            {
                undoButton.onClick.RemoveListener(OnUndoButtonClicked);
            }

            if (restartButton != null)
            {
                restartButton.onClick.RemoveListener(OnRestartButtonClicked);
            }

            if (returnToMenuButton != null)
            {
                returnToMenuButton.onClick.RemoveListener(OnReturnToMenuButtonClicked);
            }

            if (shareButton != null)
            {
                shareButton.onClick.RemoveListener(OnShareButtonClicked);
            }
        }

        private void BindLevelCompleteButtons()
        {
            if (continueButton != null)
            {
                continueButton.onClick.RemoveListener(OnContinueButtonClicked);
                continueButton.onClick.AddListener(OnContinueButtonClicked);
            }

            if (nextLevelButton != null)
            {
                nextLevelButton.onClick.RemoveListener(OnNextLevelButtonClicked);
                nextLevelButton.onClick.AddListener(OnNextLevelButtonClicked);
            }
        }

        private void UnbindLevelCompleteButtons()
        {
            if (continueButton != null)
            {
                continueButton.onClick.RemoveListener(OnContinueButtonClicked);
            }

            if (nextLevelButton != null)
            {
                nextLevelButton.onClick.RemoveListener(OnNextLevelButtonClicked);
            }
        }

        private void BindCustomLevelEditorButtons()
        {
            BindInput(customLevelIdInput, OnCustomLevelIdChanged);
            BindInput(customLevelIdTmpInput, OnCustomLevelIdChanged);
            BindSlider(customSizeSlider, OnCustomSizeChanged);
            BindBrushButton(wallBrushButton, CustomBrush.Wall);
            BindBrushButton(goalBrushButton, CustomBrush.Goal);
            BindBrushButton(boxBrushButton, CustomBrush.Box);
            BindBrushButton(playerBrushButton, CustomBrush.Player);
            BindBrushButton(eraseBrushButton, CustomBrush.Erase);
            if (customInteractionModeButton != null)
            {
                customInteractionModeButton.onClick.RemoveListener(OnCustomInteractionModeButtonClicked);
                customInteractionModeButton.onClick.AddListener(OnCustomInteractionModeButtonClicked);
            }

            if (customEditorBackButton != null)
            {
                customEditorBackButton.onClick.RemoveListener(OnCustomEditorBackButtonClicked);
                customEditorBackButton.onClick.AddListener(OnCustomEditorBackButtonClicked);
            }

            if (customEditorSaveButton != null)
            {
                customEditorSaveButton.onClick.RemoveListener(OnCustomEditorSaveButtonClicked);
                customEditorSaveButton.onClick.AddListener(OnCustomEditorSaveButtonClicked);
            }
        }

        private void UnbindCustomLevelEditorButtons()
        {
            UnbindInput(customLevelIdInput, OnCustomLevelIdChanged);
            UnbindInput(customLevelIdTmpInput, OnCustomLevelIdChanged);
            UnbindSlider(customSizeSlider, OnCustomSizeChanged);
            UnbindBrushButton(wallBrushButton);
            UnbindBrushButton(goalBrushButton);
            UnbindBrushButton(boxBrushButton);
            UnbindBrushButton(playerBrushButton);
            UnbindBrushButton(eraseBrushButton);
            if (customInteractionModeButton != null)
            {
                customInteractionModeButton.onClick.RemoveListener(OnCustomInteractionModeButtonClicked);
            }

            if (customEditorBackButton != null)
            {
                customEditorBackButton.onClick.RemoveListener(OnCustomEditorBackButtonClicked);
            }

            if (customEditorSaveButton != null)
            {
                customEditorSaveButton.onClick.RemoveListener(OnCustomEditorSaveButtonClicked);
            }
        }

        private void BindInput(InputField input, UnityEngine.Events.UnityAction<string> callback)
        {
            if (input == null)
            {
                return;
            }

            input.onValueChanged.RemoveListener(callback);
            input.onValueChanged.AddListener(callback);
        }

        private void BindInput(TMP_InputField input, UnityEngine.Events.UnityAction<string> callback)
        {
            if (input == null)
            {
                return;
            }

            input.onValueChanged.RemoveListener(callback);
            input.onValueChanged.AddListener(callback);
        }

        private void UnbindInput(InputField input, UnityEngine.Events.UnityAction<string> callback)
        {
            if (input != null)
            {
                input.onValueChanged.RemoveListener(callback);
            }
        }

        private void UnbindInput(TMP_InputField input, UnityEngine.Events.UnityAction<string> callback)
        {
            if (input != null)
            {
                input.onValueChanged.RemoveListener(callback);
            }
        }

        private void BindSlider(Slider slider, UnityEngine.Events.UnityAction<float> callback)
        {
            if (slider == null)
            {
                return;
            }

            slider.wholeNumbers = true;
            slider.minValue = MinCustomLevelSize;
            slider.maxValue = MaxCustomLevelSize;
            slider.onValueChanged.RemoveListener(callback);
            slider.onValueChanged.AddListener(callback);
        }

        private void UnbindSlider(Slider slider, UnityEngine.Events.UnityAction<float> callback)
        {
            if (slider != null)
            {
                slider.onValueChanged.RemoveListener(callback);
            }
        }

        private void BindBrushButton(Button button, CustomBrush brush)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                PlayUiButtonSound();
                SetCustomBrush(brush);
            });
        }

        private void UnbindBrushButton(Button button)
        {
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
            }
        }

        private void OnStartButtonClicked()
        {
            PlayUiButtonSound();
            StartGameFromCover();
        }

        private void OnCreateLevelButtonClicked()
        {
            PlayUiButtonSound();
            OpenCustomLevelEditor();
        }

        private void OnQuitButtonClicked()
        {
            PlayUiButtonSound();
            QuitGame();
        }

        private void OnToggleLevelModeButtonClicked()
        {
            PlayUiButtonSound();
            ToggleLevelListMode();
        }

        private void OnDeleteCustomLevelButtonClicked()
        {
            PlayUiButtonSound();
            ToggleCustomLevelDeleteMode();
        }

        private void OnLoadCustomLevelCodeButtonClicked()
        {
            PlayUiButtonSound();
            LoadCustomLevelCode();
        }

        private void OnCustomLevelCodeInputChanged(string value)
        {
            customLevelCodeInputText = value;
            customLevelCodeMessage = string.Empty;
        }

        private void OnPreviousPageButtonClicked()
        {
            PlayUiButtonSound();
            GoToPreviousPage();
        }

        private void OnNextPageButtonClicked()
        {
            PlayUiButtonSound();
            GoToNextPage();
        }

        private void OnBackToCoverButtonClicked()
        {
            PlayUiButtonSound();
            ReturnToCover();
        }

        private void OnUndoButtonClicked()
        {
            PlayUiButtonSound();
            UndoActiveStep();
        }

        private void OnRestartButtonClicked()
        {
            PlayUiButtonSound();
            RestartActiveLevel();
        }

        private void OnReturnToMenuButtonClicked()
        {
            PlayUiButtonSound();
            ReturnToMenu();
        }

        private void OnShareButtonClicked()
        {
            PlayUiButtonSound();
            ShareActiveLevelCode();
        }

        private void OnContinueButtonClicked()
        {
            PlayUiButtonSound();
            ReturnToMenu();
        }

        private void OnNextLevelButtonClicked()
        {
            PlayUiButtonSound();
            StartNextLevel();
        }

        private void OnCustomEditorBackButtonClicked()
        {
            PlayUiButtonSound();
            ReturnToCover();
        }

        private void OnCustomEditorSaveButtonClicked()
        {
            PlayUiButtonSound();
            SaveCustomLevel();
        }

        private void OnCustomInteractionModeButtonClicked()
        {
            PlayUiButtonSound();
            customInteractionModeTouched = true;
            customInteractionMode = customInteractionMode == CustomInteractionMode.Push
                ? CustomInteractionMode.Pull
                : CustomInteractionMode.Push;
            SyncCustomInteractionModeButton();
        }

        private void OnCustomLevelIdChanged(string value)
        {
            customLevelId = value;
        }

        private void OnCustomSizeChanged(float value)
        {
            var nextSize = Mathf.Clamp(Mathf.RoundToInt(value), MinCustomLevelSize, MaxCustomLevelSize);
            if (nextSize != customWidth || nextSize != customHeight)
            {
                ResizeCustomGrid(nextSize, nextSize);
                RebuildCustomGridButtons();
            }

            SyncCustomSizeControls();
        }

        private void SetCustomBrush(CustomBrush brush)
        {
            customBrush = brush;
            SyncCustomBrushButtons();
        }

        private void PlayUiButtonSound()
        {
            var clip = registry != null ? registry.uiButtonClip : null;
            if (clip == null)
            {
                return;
            }

            if (uiAudioSource == null)
            {
                uiAudioSource = GetComponent<AudioSource>();
                if (uiAudioSource == null)
                {
                    uiAudioSource = gameObject.AddComponent<AudioSource>();
                }

                uiAudioSource.playOnAwake = false;
            }

            uiAudioSource.PlayOneShot(clip);
        }

        private void UndoActiveStep()
        {
            if (activeSession == null)
            {
                return;
            }

            activeSession.UndoLastStep();
            SyncGameplayHud();
        }

        private void RestartActiveLevel()
        {
            if (activeSession == null)
            {
                return;
            }

            activeSession.RestartLevel();
            SyncGameplayHud();
        }

        private int FindLevelEntryIndex(LevelEntry targetEntry)
        {
            if (targetEntry == null || targetEntry.level == null)
            {
                return -1;
            }

            for (var i = 0; i < levelEntries.Count; i++)
            {
                var entry = levelEntries[i];
                if (entry == targetEntry || entry.level == targetEntry.level)
                {
                    return i;
                }
            }

            return -1;
        }

        private void GoToPreviousPage()
        {
            if (currentPage <= 0)
            {
                return;
            }

            currentPage--;
            MarkLevelSelectDirty();
            SyncCanvasUi();
        }

        private void GoToNextPage()
        {
            var pageCount = GetLevelSelectPageCount();
            if (currentPage >= pageCount - 1)
            {
                return;
            }

            currentPage++;
            MarkLevelSelectDirty();
            SyncCanvasUi();
        }

        private int GetPageForLevelId(string levelId)
        {
            if (string.IsNullOrWhiteSpace(levelId))
            {
                return currentPage;
            }

            var pageSize = Mathf.Max(1, columnsPerPage * rowsPerPage);
            for (var i = 0; i < levelEntries.Count; i++)
            {
                var entry = levelEntries[i];
                if (entry != null && entry.level != null && entry.level.levelId == levelId)
                {
                    return i / pageSize;
                }
            }

            return currentPage;
        }

        private void MarkLevelSelectDirty()
        {
            levelSelectDirty = true;
        }

        private void SyncCanvasUi()
        {
            if (coverRoot != null && coverRoot.activeSelf != (screen == MenuScreen.Cover))
            {
                coverRoot.SetActive(screen == MenuScreen.Cover);
            }

            if (levelSelectRoot != null && levelSelectRoot.activeSelf != (screen == MenuScreen.LevelSelect))
            {
                levelSelectRoot.SetActive(screen == MenuScreen.LevelSelect);
            }

            if (gameplayHudRoot != null && gameplayHudRoot.activeSelf != (screen == MenuScreen.Playing))
            {
                gameplayHudRoot.SetActive(screen == MenuScreen.Playing);
            }

            if (customLevelEditorRoot != null && customLevelEditorRoot.activeSelf != (screen == MenuScreen.CustomLevelEditor))
            {
                customLevelEditorRoot.SetActive(screen == MenuScreen.CustomLevelEditor);
            }

            var showLevelComplete = screen == MenuScreen.Playing && activeLevelCompletionHandled;
            if (levelCompleteRoot != null && levelCompleteRoot.activeSelf != showLevelComplete)
            {
                levelCompleteRoot.SetActive(showLevelComplete);
            }

            if (nextLevelButton != null)
            {
                nextLevelButton.interactable = showLevelComplete && completedNextEntry != null && completedNextEntry.level != null;
            }

            if (screen == MenuScreen.LevelSelect && HasCanvasLevelSelect())
            {
                UpdateLevelModeButtonLabel();
                SyncCustomLevelDeleteButton();
                SyncCustomLevelCodeImportUi();
                RebuildLevelSelectButtonsIfNeeded();
            }

            if (screen == MenuScreen.Playing && HasCanvasGameplayHud())
            {
                SyncGameplayHud();
            }

            if (screen == MenuScreen.CustomLevelEditor)
            {
                SyncCustomEditorUi();
            }
        }

        private void RebuildLevelSelectButtonsIfNeeded()
        {
            if (!levelSelectDirty)
            {
                return;
            }

            levelSelectDirty = false;
            RebuildLevelSelectButtons();
        }

        private void SyncGameplayHud()
        {
            var levelLabel = activeEntry != null ? GetLevelLabel(activeEntry.level) : "No Level Selected";
            var stepLabel = activeSession != null ? "Steps: " + activeSession.StepCount : "Steps: 0";

            SetLabelText(currentLevelText, currentLevelTmpText, levelLabel);
            SetLabelText(stepCountText, stepCountTmpText, stepLabel);

            var hasSession = activeSession != null;
            if (undoButton != null)
            {
                undoButton.interactable = hasSession;
            }

            if (restartButton != null)
            {
                restartButton.interactable = hasSession;
            }

            if (shareButton != null)
            {
                shareButton.interactable = hasSession && activeEntry != null && activeEntry.level != null;
            }
        }

        private void SyncCustomEditorUi()
        {
            if (customLevelEditorRoot == null)
            {
                return;
            }

            EnsureCustomGrid();
            SetInputTextWithoutNotify(customLevelIdInput, customLevelId);
            SetInputTextWithoutNotify(customLevelIdTmpInput, customLevelId);
            SyncCustomSizeControls();
            SetLabelText(customEditorMessageText, customEditorMessageTmpText, customEditorMessage);
            SyncCustomBrushButtons();
            SyncCustomInteractionModeButton();
            if (spawnedCustomGridButtons.Count != customWidth * customHeight)
            {
                RebuildCustomGridButtons();
            }
            else
            {
                SyncCustomGridButtons();
            }
        }

        private void SyncCustomSizeControls()
        {
            SetSliderValueWithoutNotify(customSizeSlider, customWidth);
            SetLabelText(customSizeValueText, customSizeValueTmpText, customWidth.ToString());
        }

        private void RebuildCustomGridButtons()
        {
            ClearSpawnedCustomGridButtons();
            HideSceneCustomGridCellTemplate();
            if (customGridParent == null || customGridCellPrefab == null)
            {
                return;
            }

            SyncCustomGridLayoutGroup();
            for (var y = customHeight - 1; y >= 0; y--)
            {
                for (var x = 0; x < customWidth; x++)
                {
                    var position = new Vector2Int(x, y);
                    var button = Instantiate(customGridCellPrefab, customGridParent);
                    button.gameObject.SetActive(true);
                    spawnedCustomGridButtons.Add(button);
                    ApplyCustomGridCellLayout(button, x, customHeight - 1 - y);
                    ClearButtonLabel(button);
                    ApplyGlobalTmpFontToRoot(button.gameObject);
                    button.onClick.RemoveAllListeners();
                    BindCustomGridPaintEvents(button, position);
                }
            }

            SyncCustomGridButtons();
        }

        private void BindCustomGridPaintEvents(Button button, Vector2Int position)
        {
            if (button == null)
            {
                return;
            }

            var trigger = button.GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = button.gameObject.AddComponent<EventTrigger>();
            }

            trigger.triggers.Clear();
            AddCustomGridEventTrigger(trigger, EventTriggerType.PointerDown, _ => BeginCustomGridPaint(position));
            AddCustomGridEventTrigger(trigger, EventTriggerType.PointerEnter, _ => ContinueCustomGridPaint(position));
            AddCustomGridEventTrigger(trigger, EventTriggerType.PointerUp, _ => EndCustomGridPaint());
            AddCustomGridEventTrigger(trigger, EventTriggerType.PointerExit, _ =>
            {
                if (!Input.GetMouseButton(0))
                {
                    EndCustomGridPaint();
                }
            });
        }

        private static void AddCustomGridEventTrigger(EventTrigger trigger, EventTriggerType eventType, UnityEngine.Events.UnityAction<BaseEventData> callback)
        {
            var entry = new EventTrigger.Entry
            {
                eventID = eventType
            };
            entry.callback.AddListener(callback);
            trigger.triggers.Add(entry);
        }

        private void SyncCustomGridLayoutGroup()
        {
            if (customGridParent == null)
            {
                return;
            }

            var gridLayout = customGridParent.GetComponent<GridLayoutGroup>();
            if (gridLayout == null)
            {
                return;
            }

            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = Mathf.Max(1, customWidth);
            gridLayout.cellSize = GetCustomGridCellSize();
            gridLayout.spacing = GetCustomGridCellSpacing();
        }

        private void ApplyCustomGridCellLayout(Button button, int column, int row)
        {
            if (button == null || customGridParent == null || customGridParent.GetComponent<LayoutGroup>() != null)
            {
                return;
            }

            var rectTransform = button.transform as RectTransform;
            if (rectTransform == null)
            {
                return;
            }

            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(0f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);
            var cellSize = GetCustomGridCellSize();
            var cellSpacing = GetCustomGridCellSpacing();
            rectTransform.sizeDelta = cellSize;
            rectTransform.anchoredPosition = new Vector2(
                column * (cellSize.x + cellSpacing.x),
                -row * (cellSize.y + cellSpacing.y));
        }

        private Vector2 GetCustomGridCellSize()
        {
            var size = 800f / (1.1f * Mathf.Max(1, customWidth));
            return new Vector2(size, size);
        }

        private Vector2 GetCustomGridCellSpacing()
        {
            var spacing = GetCustomGridCellSize().x * 0.1f;
            return new Vector2(spacing, spacing);
        }

        private void SyncCustomGridButtons()
        {
            if (spawnedCustomGridButtons.Count != customWidth * customHeight)
            {
                return;
            }

            var index = 0;
            for (var y = customHeight - 1; y >= 0; y--)
            {
                for (var x = 0; x < customWidth; x++)
                {
                    SyncCustomGridButtonImage(spawnedCustomGridButtons[index], new Vector2Int(x, y));
                    index++;
                }
            }
        }

        private void SyncCustomGridButtonImage(Button button, Vector2Int position)
        {
            if (button == null)
            {
                return;
            }

            // The in-game editor uses sprites rather than text labels so UI5 stays
            // consistent with the shipped tile art and works well on touch/canvas UI.
            ClearButtonLabel(button);

            var image = button.targetGraphic as Image;
            if (image == null)
            {
                image = button.GetComponent<Image>();
            }

            if (image == null)
            {
                return;
            }

            image.sprite = GetCustomCellSprite(position);
            image.color = GetCustomCellColor(position);
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.transform.localRotation = Quaternion.Euler(0f, 0f, GetCustomCellRotation(position));
        }

        private Sprite GetCustomCellSprite(Vector2Int position)
        {
            if (customHasPlayer && customPlayerStart == position)
            {
                return customPlayerCellSprite != null ? customPlayerCellSprite : customEraseCellSprite;
            }

            if (customBoxes.Contains(position))
            {
                return customBoxCellSprite != null ? customBoxCellSprite : customEraseCellSprite;
            }

            if (customGoals.Contains(position))
            {
                return customGoalCellSprite != null ? customGoalCellSprite : customEraseCellSprite;
            }

            if (customTiles[position.x, position.y] == BaseTileType.Wall)
            {
                return customWallCellSprite != null ? customWallCellSprite : customEraseCellSprite;
            }

            return customEraseCellSprite;
        }

        private Color GetCustomCellColor(Vector2Int position)
        {
            if (GetCustomCellSprite(position) != null)
            {
                return Color.white;
            }

            if (customHasPlayer && customPlayerStart == position)
            {
                return new Color(1f, 0.55f, 0.18f);
            }

            if (customBoxes.Contains(position))
            {
                return new Color(0.75f, 0.45f, 0.18f);
            }

            if (customGoals.Contains(position))
            {
                return new Color(0.25f, 0.82f, 1f);
            }

            if (customTiles[position.x, position.y] == BaseTileType.Wall)
            {
                return new Color(0.35f, 0.36f, 0.35f);
            }

            return new Color(0.45f, 0.68f, 0.38f);
        }

        private float GetCustomCellRotation(Vector2Int position)
        {
            if (!customHasPlayer || customPlayerStart != position)
            {
                return 0f;
            }

            switch (customPlayerFacing)
            {
                case Direction.Right:
                    return -90f;
                case Direction.Down:
                    return 180f;
                case Direction.Left:
                    return 90f;
                default:
                    return 0f;
            }
        }

        private void ClearSpawnedCustomGridButtons()
        {
            for (var i = spawnedCustomGridButtons.Count - 1; i >= 0; i--)
            {
                var button = spawnedCustomGridButtons[i];
                if (button == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(button.gameObject);
                }
                else
                {
                    DestroyImmediate(button.gameObject);
                }
            }

            spawnedCustomGridButtons.Clear();
        }

        private void HideSceneCustomGridCellTemplate()
        {
            if (customGridCellPrefab == null || customGridParent == null)
            {
                return;
            }

            if (customGridCellPrefab.gameObject.scene.IsValid()
                && customGridCellPrefab.transform.IsChildOf(customGridParent)
                && customGridCellPrefab.gameObject.activeSelf)
            {
                customGridCellPrefab.gameObject.SetActive(false);
            }
        }

        private void SyncCustomBrushButtons()
        {
            SetButtonInteractable(wallBrushButton, customBrush != CustomBrush.Wall);
            SetButtonInteractable(goalBrushButton, customBrush != CustomBrush.Goal);
            SetButtonInteractable(boxBrushButton, customBrush != CustomBrush.Box);
            SetButtonInteractable(playerBrushButton, customBrush != CustomBrush.Player);
            SetButtonInteractable(eraseBrushButton, customBrush != CustomBrush.Erase);
        }

        private void SyncCustomInteractionModeButton()
        {
            if (customInteractionModeButton != null)
            {
                SetButtonLabel(customInteractionModeButton, customInteractionModeTouched
                    ? (customInteractionMode == CustomInteractionMode.Push ? "Push" : "Pull")
                    : "Mode");
            }
        }

        private void RebuildLevelSelectButtons()
        {
            RefreshLevelEntries();
            ClearSpawnedLevelButtons();
            HideSceneLevelButtonTemplate();

            var pageCount = GetLevelSelectPageCount();
            currentPage = Mathf.Clamp(currentPage, 0, pageCount - 1);

            if (levelButtonParent == null || levelButtonPrefab == null)
            {
                UpdateLevelSelectNavigation(pageCount);
                return;
            }

            var pageSize = Mathf.Max(1, columnsPerPage * rowsPerPage);
            var start = currentPage * pageSize;
            var end = Mathf.Min(levelEntries.Count, start + pageSize);
            for (var index = start; index < end; index++)
            {
                CreateLevelSelectButton(index, index - start);
            }

            UpdateLevelSelectNavigation(pageCount);
        }

        private void CreateLevelSelectButton(int levelIndex, int pageIndex)
        {
            var entry = levelEntries[levelIndex];
            var button = Instantiate(levelButtonPrefab, levelButtonParent);
            button.gameObject.SetActive(true);
            spawnedLevelButtons.Add(button);
            ApplyGlobalTmpFontToRoot(button.gameObject);
            ApplyFallbackLevelButtonLayout(button, pageIndex);

            var unlocked = IsLevelUnlocked(levelIndex);
            var cleared = IsLevelCleared(entry.level);
            button.interactable = unlocked || (customLevelDeleteMode && entry.isCustom);
            SetButtonLabel(button, BuildLevelButtonLabel(entry, unlocked, cleared));

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                PlayUiButtonSound();
                if (customLevelDeleteMode && entry.isCustom)
                {
                    DeleteCustomLevel(entry);
                    return;
                }

                StartLevel(entry);
            });
        }

        private void ApplyFallbackLevelButtonLayout(Button button, int pageIndex)
        {
            if (button == null || levelButtonParent == null)
            {
                return;
            }

            if (levelButtonParent.GetComponent<LayoutGroup>() != null)
            {
                return;
            }

            var rectTransform = button.transform as RectTransform;
            if (rectTransform == null)
            {
                return;
            }

            var columns = Mathf.Max(1, columnsPerPage);
            var row = pageIndex / columns;
            var column = pageIndex % columns;

            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(0f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.sizeDelta = levelButtonSize;
            rectTransform.anchoredPosition = new Vector2(
                column * (levelButtonSize.x + levelButtonSpacing.x),
                -row * (levelButtonSize.y + levelButtonSpacing.y));
        }

        private void UpdateLevelSelectNavigation(int pageCount)
        {
            pageCount = Mathf.Max(1, pageCount);
            currentPage = Mathf.Clamp(currentPage, 0, pageCount - 1);
            UpdateLevelModeButtonLabel();

            if (previousPageButton != null)
            {
                previousPageButton.interactable = currentPage > 0;
            }

            if (nextPageButton != null)
            {
                nextPageButton.interactable = currentPage < pageCount - 1;
            }

            var pageLabel = (currentPage + 1) + " / " + pageCount;
            if (pageText != null)
            {
                pageText.text = pageLabel;
            }

            if (pageTmpText != null)
            {
                ApplyGlobalTmpFont(pageTmpText);
                pageTmpText.text = pageLabel;
            }
        }

        private void UpdateLevelModeButtonLabel()
        {
            if (toggleLevelModeButton == null)
            {
                return;
            }

            SetButtonLabel(toggleLevelModeButton, levelListMode == LevelListMode.Official ? "Creative Mode" : "Story Mode");
        }

        private void SyncCustomLevelDeleteButton()
        {
            if (deleteCustomLevelButton == null)
            {
                return;
            }

            var show = levelListMode == LevelListMode.Custom;
            if (deleteCustomLevelButton.gameObject.activeSelf != show)
            {
                deleteCustomLevelButton.gameObject.SetActive(show);
            }

            if (show)
            {
                SetButtonLabel(deleteCustomLevelButton, customLevelDeleteMode ? "Select" : "Delete");
            }
        }

        private void SyncCustomLevelCodeImportUi()
        {
            var show = levelListMode == LevelListMode.Custom;
            SetGameObjectActive(customLevelCodeInput != null ? customLevelCodeInput.gameObject : null, show);
            SetGameObjectActive(customLevelCodeTmpInput != null ? customLevelCodeTmpInput.gameObject : null, show);
            SetGameObjectActive(loadCustomLevelCodeButton != null ? loadCustomLevelCodeButton.gameObject : null, show);
            SetGameObjectActive(customLevelCodeMessageText != null ? customLevelCodeMessageText.gameObject : null, show);
            SetGameObjectActive(customLevelCodeMessageTmpText != null ? customLevelCodeMessageTmpText.gameObject : null, show);

            if (!show)
            {
                return;
            }

            SetInputTextWithoutNotify(customLevelCodeInput, customLevelCodeInputText);
            SetInputTextWithoutNotify(customLevelCodeTmpInput, customLevelCodeInputText);
            SetLabelText(customLevelCodeMessageText, customLevelCodeMessageTmpText, customLevelCodeMessage);
            if (loadCustomLevelCodeButton != null)
            {
                SetButtonLabel(loadCustomLevelCodeButton, "Load");
            }
        }

        private static void SetGameObjectActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }

        private void ClearSpawnedLevelButtons()
        {
            for (var i = spawnedLevelButtons.Count - 1; i >= 0; i--)
            {
                var button = spawnedLevelButtons[i];
                if (button == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(button.gameObject);
                }
                else
                {
                    DestroyImmediate(button.gameObject);
                }
            }

            spawnedLevelButtons.Clear();
        }

        private void HideSceneLevelButtonTemplate()
        {
            if (levelButtonPrefab == null || levelButtonParent == null)
            {
                return;
            }

            if (levelButtonPrefab.gameObject.scene.IsValid()
                && levelButtonPrefab.transform.IsChildOf(levelButtonParent)
                && levelButtonPrefab.gameObject.activeSelf)
            {
                levelButtonPrefab.gameObject.SetActive(false);
            }
        }

        private int GetLevelSelectPageCount()
        {
            var pageSize = Mathf.Max(1, columnsPerPage * rowsPerPage);
            return Mathf.Max(1, Mathf.CeilToInt(levelEntries.Count / (float)pageSize));
        }

        private void SetButtonLabel(Button button, string label)
        {
            var text = button.GetComponentInChildren<Text>(true);
            if (text != null)
            {
                text.text = label;
            }

            var tmpText = button.GetComponentInChildren<TMP_Text>(true);
            if (tmpText != null)
            {
                ApplyGlobalTmpFont(tmpText);
                tmpText.text = label;
            }
        }

        private void ClearButtonLabel(Button button)
        {
            if (button == null)
            {
                return;
            }

            SetButtonLabel(button, string.Empty);
        }

        private static string BuildLevelButtonLabel(LevelEntry entry, bool unlocked, bool cleared)
        {
            var label = GetLevelLabel(entry != null ? entry.level : null);
            if (entry != null && entry.isCustom)
            {
                return label;
            }

            if (!unlocked)
            {
                return label + "\nLocked";
            }

            return cleared ? label + "\nCleared" : label;
        }

        private void SetLabelText(Text text, TMP_Text tmpText, string label)
        {
            if (text != null)
            {
                text.text = label;
            }

            if (tmpText != null)
            {
                ApplyGlobalTmpFont(tmpText);
                tmpText.text = label;
            }
        }

        private void ApplyGlobalTmpFontToCanvas()
        {
            var font = GetGlobalTmpFont();
            if (font == null)
            {
                return;
            }

            EnsureTmpFallback(font);
            ApplyGlobalTmpFontToRoot(coverRoot);
            ApplyGlobalTmpFontToRoot(levelSelectRoot);
            ApplyGlobalTmpFontToRoot(gameplayHudRoot);
            ApplyGlobalTmpFontToRoot(levelCompleteRoot);
            ApplyGlobalTmpFontToRoot(customLevelEditorRoot);
            ApplyGlobalTmpFont(pageTmpText);
            ApplyGlobalTmpFont(currentLevelTmpText);
            ApplyGlobalTmpFont(stepCountTmpText);
            ApplyGlobalTmpFont(customEditorMessageTmpText);
        }

        private void ApplyGlobalTmpFontToRoot(GameObject root)
        {
            var font = GetGlobalTmpFont();
            if (root == null || font == null)
            {
                return;
            }

            var texts = root.GetComponentsInChildren<TMP_Text>(true);
            for (var i = 0; i < texts.Length; i++)
            {
                ApplyGlobalTmpFont(texts[i], font);
            }
        }

        private void ApplyGlobalTmpFont(TMP_Text text)
        {
            var font = GetGlobalTmpFont();
            if (font != null)
            {
                ApplyGlobalTmpFont(text, font);
            }
        }

        private static void ApplyGlobalTmpFont(TMP_Text text, TMP_FontAsset font)
        {
            if (text == null || font == null || text.font == font)
            {
                return;
            }

            text.font = font;
            text.fontSharedMaterial = font.material;
        }

        private TMP_FontAsset GetGlobalTmpFont()
        {
            return registry != null ? registry.globalTmpFont : null;
        }

        private static void SetInputTextWithoutNotify(InputField input, string value)
        {
            if (input != null && input.text != value)
            {
                input.SetTextWithoutNotify(value);
            }
        }

        private static void SetInputTextWithoutNotify(TMP_InputField input, string value)
        {
            if (input != null && input.text != value)
            {
                input.SetTextWithoutNotify(value);
            }
        }

        private static void SetSliderValueWithoutNotify(Slider slider, int value)
        {
            if (slider == null)
            {
                return;
            }

            slider.wholeNumbers = true;
            slider.minValue = MinCustomLevelSize;
            slider.maxValue = MaxCustomLevelSize;
            if (!Mathf.Approximately(slider.value, value))
            {
                slider.SetValueWithoutNotify(value);
            }
        }

        private static void SetButtonInteractable(Button button, bool interactable)
        {
            if (button != null)
            {
                button.interactable = interactable;
            }
        }

        private static void EnsureTmpFallback(TMP_FontAsset font)
        {
            if (font == null || TMP_Settings.fallbackFontAssets.Contains(font))
            {
                return;
            }

            TMP_Settings.fallbackFontAssets.Add(font);
        }

        private void DestroyRuntimeHost()
        {
            var existingHost = runtimeHost != null ? runtimeHost : GameObject.Find(RuntimeHostName);
            if (existingHost == null)
            {
                return;
            }

            var builder = existingHost.GetComponent<LevelSceneBuilder>();
            if (builder != null)
            {
                builder.Clear();
            }

            if (Application.isPlaying)
            {
                Destroy(existingHost);
            }
            else
            {
                DestroyImmediate(existingHost);
            }

            runtimeHost = null;
        }

        private void RefreshLevelEntries()
        {
            levelEntries.Clear();

            if (levelListMode == LevelListMode.Custom)
            {
                // Creative-mode levels are loaded from JSON in persistentDataPath.
                // They never require AssetDatabase and therefore work in player builds.
                var customLevels = CustomLevelStorage.LoadLevels();
                for (var i = 0; i < customLevels.Count; i++)
                {
                    var level = customLevels[i];
                    if (level == null)
                    {
                        continue;
                    }

                    levelEntries.Add(new LevelEntry
                    {
                        level = level,
                        isCustom = true
                    });
                }

                return;
            }

            if (registry == null)
            {
                registry = Resources.Load<LevelSceneBuilderRegistry>("LevelSceneBuilderRegistry");
            }

            if (registry == null || registry.levels == null)
            {
                return;
            }

            for (var i = 0; i < registry.levels.Count; i++)
            {
                var entry = registry.levels[i];
                if (entry == null || !entry.enabled || entry.level == null)
                {
                    continue;
                }

                levelEntries.Add(new LevelEntry
                {
                    registryEntry = entry,
                    level = entry.level,
                    isCustom = false
                });
            }
        }

        private LevelSceneBuilder GetSceneBuilderTemplate()
        {
            if (sceneBuilderTemplate != null)
            {
                return sceneBuilderTemplate;
            }

            sceneBuilderTemplate = FindObjectsOfType<LevelSceneBuilder>(true)
                .FirstOrDefault(builder => builder != null && builder.gameObject.name != RuntimeHostName);
            return sceneBuilderTemplate;
        }

        private bool IsLevelUnlocked(int index)
        {
            if (index < 0 || index >= levelEntries.Count)
            {
                return false;
            }

            var entry = levelEntries[index];
            if (entry.isCustom)
            {
                return true;
            }

            if (entry.registryEntry == null)
            {
                return index == 0 || IsLevelUnlocked(entry.level);
            }

            return entry.registryEntry.unlocked || IsLevelUnlocked(entry.level);
        }

        private void SetRegistryEntryUnlocked(LevelDataAsset level)
        {
            if (registry == null || registry.levels == null || level == null)
            {
                return;
            }

            for (var i = 0; i < registry.levels.Count; i++)
            {
                var entry = registry.levels[i];
                if (entry != null && entry.level == level)
                {
                    entry.unlocked = true;
                    return;
                }
            }
        }

        private static bool IsLevelUnlocked(LevelDataAsset level)
        {
            return level != null && PlayerPrefs.GetInt(GetUnlockKey(level), 0) == 1;
        }

        private static bool IsLevelCleared(LevelDataAsset level)
        {
            return level != null && PlayerPrefs.GetInt(GetClearKey(level), 0) == 1;
        }

        private static void MarkLevelCleared(LevelDataAsset level)
        {
            if (level == null)
            {
                return;
            }

            PlayerPrefs.SetInt(GetClearKey(level), 1);
            PlayerPrefs.Save();
        }

        private static void MarkLevelUnlocked(LevelDataAsset level)
        {
            if (level == null)
            {
                return;
            }

            PlayerPrefs.SetInt(GetUnlockKey(level), 1);
            PlayerPrefs.Save();
        }

        public static void ClearStoredLevelUnlock(LevelDataAsset level)
        {
            if (level == null)
            {
                return;
            }

            PlayerPrefs.DeleteKey(GetUnlockKey(level));
            PlayerPrefs.Save();
        }

        private static string GetClearKey(LevelDataAsset level)
        {
            return ClearKeyPrefix + level.levelId;
        }

        private static string GetUnlockKey(LevelDataAsset level)
        {
            return UnlockKeyPrefix + level.levelId;
        }

        private static string GetLevelLabel(LevelDataAsset level)
        {
            return level == null || string.IsNullOrWhiteSpace(level.levelId) ? "Untitled Level" : level.levelId;
        }

        private static Rect CenteredRect(float width, float height)
        {
            return new Rect(
                (Screen.width - width) * 0.5f,
                (Screen.height - height) * 0.5f,
                width,
                height);
        }

        private static GUIStyle TitleStyle(int size = 22)
        {
            return new GUIStyle(GUI.skin.label)
            {
                fontSize = size,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
        }

        private static GUIStyle CenterLabelStyle()
        {
            return new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter
            };
        }
    }
}
