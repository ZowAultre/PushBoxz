using System;
using System.Collections.Generic;
using UnityEngine;

namespace PushBoxz.Data
{
    [CreateAssetMenu(menuName = "PushBoxz/Level Scene Builder Registry", fileName = "LevelSceneBuilderRegistry")]
    public class LevelSceneBuilderRegistry : ScriptableObject
    {
        public List<LevelSceneBuilderRegistryEntry> levels = new List<LevelSceneBuilderRegistryEntry>();
    }

    [Serializable]
    public class LevelSceneBuilderRegistryEntry
    {
        public LevelDataAsset level;
        public bool enabled = true;
        public bool unlocked;
    }
}
