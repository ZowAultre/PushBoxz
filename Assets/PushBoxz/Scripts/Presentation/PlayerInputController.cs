using PushBoxz.Core;
using UnityEngine;

namespace PushBoxz.Presentation
{
    public class PlayerInputController : MonoBehaviour
    {
        [SerializeField] private GameSession session;

        private void Awake()
        {
            if (session == null)
            {
                session = GetComponent<GameSession>();
            }
        }

        private void Update()
        {
            if (session == null)
            {
                return;
            }

            if (session.MovementMode == PlayerMovementMode.Continuous)
            {
                var moveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
                session.TryContinuousMove(moveInput, Time.deltaTime);
            }

            if (Input.GetKeyDown(KeyCode.W))
            {
                TryGridMove(Direction.Up);
            }
            else if (Input.GetKeyDown(KeyCode.S))
            {
                TryGridMove(Direction.Down);
            }
            else if (Input.GetKeyDown(KeyCode.A))
            {
                TryGridMove(Direction.Left);
            }
            else if (Input.GetKeyDown(KeyCode.D))
            {
                TryGridMove(Direction.Right);
            }
            else if (Input.GetKeyDown(KeyCode.F))
            {
                session.TryPush();
            }
        }

        private void TryGridMove(Direction direction)
        {
            if (session.MovementMode == PlayerMovementMode.GridStep)
            {
                session.TryMove(direction);
            }
        }
    }
}
