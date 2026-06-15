using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace PushBoxz.Data
{
    [CreateAssetMenu(menuName = "PushBoxz/Level Scene Builder Registry", fileName = "LevelSceneBuilderRegistry")]
    public class LevelSceneBuilderRegistry : ScriptableObject
    {
        [Header("Optional Prefabs")]
        public GameObject floorPrefab;
        public GameObject wallPrefab;
        public GameObject goalPrefab;
        public GameObject boxPrefab;
        public GameObject playerPrefab;

        [Header("Audio")]
        public AudioClip moveClip;
        public AudioClip pushClip;
        public AudioClip boxOnGoalClip;
        public AudioClip levelCompleteClip;
        public AudioClip uiButtonClip;

        [Header("UI")]
        public TMP_FontAsset globalTmpFont;

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
