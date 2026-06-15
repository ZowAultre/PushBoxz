namespace PushBoxz.Core
{
    /// <summary>
    /// Machine-readable reason for a failed push attempt. Useful for debugging and UI feedback.
    /// </summary>
    public enum PushFailReason
    {
        None,
        PlayerBusy,
        NoBoxInFacingDirection,
        TargetOutOfBounds,
        TargetBlocked
    }
}
