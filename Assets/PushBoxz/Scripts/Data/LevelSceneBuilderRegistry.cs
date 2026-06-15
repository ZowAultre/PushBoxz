using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace PushBoxz.Data
{
    /// <summary>
    /// Central registry for playable official levels plus shared presentation assets.
    /// Runtime menus read this asset instead of scanning project folders.
    /// </summary>
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

    /// <summary>
    /// Per-level menu state stored with the official level list.
    /// </summary>
    [Serializable]
    public class LevelSceneBuilderRegistryEntry
    {
        public LevelDataAsset level;
        public bool enabled = true;
        public bool unlocked;
    }
}
