using UnityEngine;
using PushBoxz.Gameplay;

namespace PushBoxz.Core
{
    /// <summary>
    /// Immutable result object returned by push validation/execution.
    /// Carries both failure diagnostics and successful box movement data.
    /// </summary>
    public readonly struct PushResult
    {
        public readonly bool success;
        public readonly PushFailReason failReason;
        public readonly BoxRuntimeState box;
        public readonly Vector2Int from;
        public readonly Vector2Int to;

        private PushResult(bool success, PushFailReason failReason, BoxRuntimeState box, Vector2Int from, Vector2Int to)
        {
            this.success = success;
            this.failReason = failReason;
            this.box = box;
            this.from = from;
            this.to = to;
        }

        /// <summary>
        /// Creates a successful push result with the moved box and its source/target cells.
        /// </summary>
        public static PushResult Success(BoxRuntimeState box, Vector2Int from, Vector2Int to)
        {
            return new PushResult(true, PushFailReason.None, box, from, to);
        }

        /// <summary>
        /// Creates a failed push result with a stable failure reason for callers to inspect.
        /// </summary>
        public static PushResult Failure(PushFailReason failReason)
        {
            return new PushResult(false, failReason, null, Vector2Int.zero, Vector2Int.zero);
        }
    }
}
