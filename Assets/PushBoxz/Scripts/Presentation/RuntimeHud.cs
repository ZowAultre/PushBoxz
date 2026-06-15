using UnityEngine;

namespace PushBoxz.Presentation
{
    /// <summary>
    /// Lightweight fallback HUD used when a Canvas-driven UI is not present.
    /// </summary>
    public class RuntimeHud : MonoBehaviour
    {
        [SerializeField] private GameSession session;

        private void Awake()
        {
            if (session == null)
            {
                session = FindObjectOfType<GameSession>();
            }
        }

        private void OnGUI()
        {
            const int padding = 16;
            const int buttonWidth = 120;
            const int lineHeight = 28;

            GUILayout.BeginArea(new Rect(padding, padding, 260, 160), GUI.skin.box);

            var levelName = session != null && session.LevelData != null
                ? session.LevelData.levelId
                : "No Level";

            GUILayout.Label(levelName);
            GUILayout.Label(session != null ? "Steps: " + session.StepCount : "Steps: 0");
            GUILayout.Label(session != null ? "Move: " + session.MovementMode : "Move: -");
            if (session != null && session.IsCompleted)
            {
                GUILayout.Label("Completed");
            }

            GUILayout.Space(8);

            GUI.enabled = session != null;
            if (GUILayout.Button("Toggle Move", GUILayout.Width(buttonWidth), GUILayout.Height(lineHeight)))
            {
                session.ToggleMovementMode();
            }

            if (GUILayout.Button("Restart", GUILayout.Width(buttonWidth), GUILayout.Height(lineHeight)))
            {
                session.RestartLevel();
            }

            GUI.enabled = true;
            GUILayout.EndArea();
        }
    }
}
