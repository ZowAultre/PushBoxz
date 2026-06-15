namespace PushBoxz.Gameplay
{
    /// <summary>
    /// Small state machine used to block input while push/cooldown animation is in progress.
    /// </summary>
    public enum PlayerActionState
    {
        Idle,
        Pushing,
        Cooldown
    }
}
