using System.Collections.Generic;
using System.Linq;
using PushBoxz.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PushBoxz.Presentation
{
    public class LevelMenuController : MonoBehaviour
    {
        private const string RuntimeHostName = "PushBoxz Runtime Host";
        private const string ClearKeyPrefix = "PushBoxz.LevelCleared.";
        private const string UnlockKeyPrefix = "PushBoxz.LevelUnlocked.";

        [SerializeField] private LevelSceneBuilderRegistry registry;
        [SerializeField] private LevelSceneBuilder sceneBuilderTemplate;
        [SerializeField] private int columnsPerPage = 5;
        [SerializeField] private int rowsPerPage = 4;

        [Header("Canvas UI - Cover")]
        [SerializeField] private GameObject coverRoot;
        [SerializeField] private Button startButton;
        [SerializeField] private Button quitButton;

        [Header("Canvas UI - Level Select")]
        [SerializeField] private GameObject levelSelectRoot;
        [SerializeField] private Transform levelButtonParent;
        [SerializeField] private Button levelButtonPrefab;
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

        [Header("Canvas UI - Level Complete")]
        [SerializeField] private GameObject levelCompleteRoot;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button nextLevelButton;

        private readonly List<LevelEntry> levelEntries = new List<LevelEntry>();
        private readonly List<Button> spawnedLevelButtons = new List<Button>();
        private GameObject runtimeHost;
        private GameSession activeSession;
        private LevelEntry activeEntry;
        private LevelEntry completedNextEntry;
        private AudioSource uiAudioSource;
        private int currentPage;
        private bool activeLevelCompletionHandled;
        private bool levelSelectDirty = true;
        private MenuScreen screen = MenuScreen.Cover;

        private enum MenuScreen
        {
            Cover,
            LevelSelect,
            Playing
        }

        private class LevelEntry
        {
            public LevelSceneBuilderRegistryEntry registryEntry;
            public LevelDataAsset level;
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
            RefreshLevelEntries();
            DestroyRuntimeHost();
            BindCoverButtons();
            BindLevelSelectButtons();
            BindGameplayHudButtons();
            BindLevelCompleteButtons();
            MarkLevelSelectDirty();
            SyncCanvasUi();
        }

        private void OnDestroy()
        {
            UnbindCoverButtons();
            UnbindLevelSelectButtons();
            UnbindGameplayHudButtons();
            UnbindLevelCompleteButtons();
            ClearSpawnedLevelButtons();
        }

        private void OnGUI()
        {
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
            GUILayout.Label("Level Select", TitleStyle());
            GUILayout.Space(12f);

            if (levelEntries.Count == 0)
            {
                GUILayout.Label("No enabled levels. Configure levels in Level Registry.");
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
                StartLevel(entry);
            }

            GUI.enabled = true;
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
            RefreshLevelEntries();
            currentPage = 0;
            screen = MenuScreen.LevelSelect;
            MarkLevelSelectDirty();
            SyncCanvasUi();
        }

        private void ReturnToCover()
        {
            screen = MenuScreen.Cover;
            SyncCanvasUi();
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

        private void BindCoverButtons()
        {
            if (startButton != null)
            {
                startButton.onClick.RemoveListener(OnStartButtonClicked);
                startButton.onClick.AddListener(OnStartButtonClicked);
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

            if (quitButton != null)
            {
                quitButton.onClick.RemoveListener(OnQuitButtonClicked);
            }
        }

        private void BindLevelSelectButtons()
        {
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

        private void OnStartButtonClicked()
        {
            PlayUiButtonSound();
            StartGameFromCover();
        }

        private void OnQuitButtonClicked()
        {
            PlayUiButtonSound();
            QuitGame();
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
                RebuildLevelSelectButtonsIfNeeded();
            }

            if (screen == MenuScreen.Playing && HasCanvasGameplayHud())
            {
                SyncGameplayHud();
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
            button.interactable = unlocked;
            SetButtonLabel(button, BuildLevelButtonLabel(entry.level, unlocked, cleared));

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                PlayUiButtonSound();
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

        private static string BuildLevelButtonLabel(LevelDataAsset level, bool unlocked, bool cleared)
        {
            var label = GetLevelLabel(level);
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
            ApplyGlobalTmpFont(pageTmpText);
            ApplyGlobalTmpFont(currentLevelTmpText);
            ApplyGlobalTmpFont(stepCountTmpText);
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
                    level = entry.level
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
