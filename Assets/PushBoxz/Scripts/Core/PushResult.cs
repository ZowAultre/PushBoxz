using UnityEngine;
using PushBoxz.Gameplay;

namespace PushBoxz.Core
{
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

        public static PushResult Success(BoxRuntimeState box, Vector2Int from, Vector2Int to)
        {
            return new PushResult(true, PushFailReason.None, box, from, to);
        }

        public static PushResult Failure(PushFailReason failReason)
        {
            return new PushResult(false, failReason, null, Vector2Int.zero, Vector2Int.zero);
        }
    }
}
