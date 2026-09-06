using System;
using System.Collections.Generic;
using UnityEngine;

namespace KillOrDead.Combat
{
    [CreateAssetMenu(fileName = "ImpactEffectLibrary", menuName = "KILL OR DEAD/Combat/Impact Effect Library")]
    public class ImpactEffectLibrary : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public SurfaceType surfaceType;
            public GameObject impactPrefab;
        }

        public List<Entry> entries = new();
        public GameObject fallbackPrefab;

        public GameObject GetPrefab(SurfaceType surfaceType)
        {
            foreach (var entry in entries)
            {
                if (entry.surfaceType == surfaceType) return entry.impactPrefab;
            }

            return fallbackPrefab;
        }
    }
}
