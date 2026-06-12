using System.Collections.Generic;
using UnityEngine;

namespace PushBoxz.Data
{
    [CreateAssetMenu(menuName = "PushBoxz/Level Catalog", fileName = "LevelCatalog")]
    public class LevelCatalog : ScriptableObject
    {
        public List<LevelDataAsset> levels = new List<LevelDataAsset>();
    }
}
