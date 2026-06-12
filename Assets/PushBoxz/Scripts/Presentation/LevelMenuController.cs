using PushBoxz.Data;
using UnityEngine;

namespace PushBoxz.Presentation
{
    public class LevelMenuController : MonoBehaviour
    {
        private const string RuntimeHostName = "PushBoxz Runtime Host";
        private const string ClearKeyPrefix = "PushBoxz.LevelCleared.";

        [SerializeField] private LevelCatalog catalog;

        private GameObject runtimeHost;
        private GameSession activeSession;
        private LevelDataAsset activeLevel;
        private Vector2 menuScroll;
        private bool menuVisible = true;

        public LevelCatalog Catalog
        {
            get { return catalog; }
            set { catalog = value; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapMenu()
        {
            if (FindObjectOfType<LevelMenuController>() != null)
            {
                return;
            }

            var loadedCatalog = Resources.Load<LevelCatalog>("LevelCatalog");
            if (loadedCatalog == null)
            {
                return;
            }

            var menuObject = new GameObject("PushBoxz Level Menu");
            var controller = menuObject.AddComponent<LevelMenuController>();
            controller.Catalog = loadedCatalog;
        }

        private void Awake()
        {
            if (catalog == null)
            {
                catalog = Resources.Load<LevelCatalog>("LevelCatalog");
            }

            if (menuVisible)
            {
                DestroyRuntimeHost();
            }
        }

        private void OnGUI()
        {
            if (menuVisible)
            {
                DrawLevelMenu();
                return;
            }

            if (activeSession != null && activeSession.IsCompleted)
            {
                DrawCompletedPanel();
            }
        }

        private void DrawLevelMenu()
        {
            const int padding = 18;
            var width = Mathf.Min(420f, Screen.width - padding * 2f);
            var height = Mathf.Min(520f, Screen.height - padding * 2f);
            var rect = new Rect(padding, padding, width, height);

            GUILayout.BeginArea(rect, GUI.skin.box);
            GUILayout.Label("关卡菜单", EditorLikeTitleStyle());
            GUILayout.Space(8f);

            if (catalog == null || catalog.levels == null || catalog.levels.Count == 0)
            {
                GUILayout.Label("未找到可玩关卡目录。");
                GUILayout.Label("请在编辑器中使用 Tools/PushBoxz/Rebuild Level Catalog。");
                GUILayout.EndArea();
                return;
            }

            menuScroll = GUILayout.BeginScrollView(menuScroll);
            for (var i = 0; i < catalog.levels.Count; i++)
            {
                var level = catalog.levels[i];
                if (level == null)
                {
                    continue;
                }

                using (new GUILayout.HorizontalScope())
                {
                    var cleared = IsLevelCleared(level);
                    var label = string.IsNullOrWhiteSpace(level.levelId) ? level.name : level.levelId;
                    if (GUILayout.Button(label, GUILayout.Height(32f)))
                    {
                        StartLevel(level);
                    }

                    GUILayout.Label(cleared ? "已通关" : "未通关", GUILayout.Width(64f));
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawCompletedPanel()
        {
            const int panelWidth = 260;
            const int panelHeight = 126;
            var rect = new Rect((Screen.width - panelWidth) * 0.5f, 24f, panelWidth, panelHeight);

            GUILayout.BeginArea(rect, GUI.skin.box);
            GUILayout.Label("通关完成", EditorLikeTitleStyle());
            GUILayout.Label(activeLevel != null ? activeLevel.levelId : string.Empty);
            GUILayout.Label(activeSession != null ? "步数：" + activeSession.StepCount : string.Empty);
            GUILayout.Space(8f);

            if (GUILayout.Button("继续", GUILayout.Height(32f)))
            {
                MarkLevelCleared(activeLevel);
                ReturnToMenu();
            }

            GUILayout.EndArea();
        }

        private void StartLevel(LevelDataAsset level)
        {
            if (level == null)
            {
                return;
            }

            DestroyRuntimeHost();

            runtimeHost = new GameObject(RuntimeHostName);
            runtimeHost.AddComponent<LevelSceneBuilder>();
            activeSession = runtimeHost.AddComponent<GameSession>();
            runtimeHost.AddComponent<PlayerInputController>();
            runtimeHost.AddComponent<RuntimeHud>();

            activeLevel = level;
            activeSession.BuildOnStart = false;
            activeSession.LevelData = level;
            activeSession.RestartLevel();
            menuVisible = false;
        }

        private void ReturnToMenu()
        {
            DestroyRuntimeHost();
            activeLevel = null;
            activeSession = null;
            menuVisible = true;
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

        private static string GetClearKey(LevelDataAsset level)
        {
            return ClearKeyPrefix + level.levelId;
        }

        private static GUIStyle EditorLikeTitleStyle()
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold
            };
            return style;
        }
    }
}
