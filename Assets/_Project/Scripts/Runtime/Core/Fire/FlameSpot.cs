using System;
using UnityEngine;

namespace _Project.Fire
{
    /// <summary>
    /// One flame of a burning building (a fire effect on a wall, the ground or the roof). Its strength (0..1)
    /// shrinks the effect and thins its particles; at 0 it stops emitting.
    /// </summary>
    [Serializable]
    public sealed class FlameSpot
    {
        public Transform root;
        [Tooltip("Roof flames also go out from water on the upper walls below them.")]
        public bool onRoof;
        public float baseScale = 1f;
        public ParticleSystem[] systems;
        [Tooltip("Shown over the flame when only a few are left, so the last ones are easy to find.")]
        public GameObject marker;

        [NonSerialized] public float Strength;
        [NonSerialized] public float LastWetTime;
        [NonSerialized] public float ReigniteAt;
        [NonSerialized] public float[] BaseRates;
        [NonSerialized] public int[] Neighbours;
    }
}
