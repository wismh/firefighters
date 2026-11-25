using System;
using _Project.Player;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Zenject;

namespace _Project.FireHose
{
    /// <summary>
    /// What the screen does when the player stands in a fire: flames around the edges, hot colours and a wobbling
    /// picture, then soot that clears slowly. Harmless: a fireman is hard to burn.
    /// </summary>
    public sealed class BurningView : MonoBehaviour
    {
        private const float HeatRise = 3.5f;
        private const float HeatFall = 1.4f;
        private const float SootRise = 0.45f;
        private const float SootFall = 0.2f;
        private const float BurningHeat = 0.45f;
        private const float WobbleSpeed = 7f;
        private const float WobbleAmount = 0.22f;
        // Where on the body the heat is felt.
        private const float ChestHeight = 1f;

        private static readonly int HeatId = Shader.PropertyToID("_Heat");
        private static readonly int SootId = Shader.PropertyToID("_Soot");

        [SerializeField] private RawImage overlay;
        [SerializeField] private Volume volume;
        [SerializeField] private FirstPersonController walker;

        private FireService _fires;
        private Material _material;
        private LensDistortion _distortion;
        private float _soot;

        /// <summary>Raised when the player steps into the flames.</summary>
        public event Action CaughtFire;

        /// <summary>How much the player is burning, 0..1, smoothed.</summary>
        public float Heat { get; private set; }

        public bool IsBurning { get; private set; }

        [Inject]
        public void Construct(FireService fires)
        {
            _fires = fires;
        }

        private void Awake()
        {
            _material = new Material(overlay.material);
            overlay.material = _material;
            // The profile property is a runtime copy, so animating it never touches the asset.
            if (!volume.profile.TryGet(out _distortion))
            {
                throw new InvalidOperationException("[burning] the volume profile has no lens distortion");
            }
        }

        private void OnDestroy()
        {
            Destroy(_material);
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            var onFoot = walker.isActiveAndEnabled;
            var target = onFoot ? _fires.HeatAt(walker.transform.position + Vector3.up * ChestHeight) : 0f;
            Heat = Mathf.MoveTowards(Heat, target, (target > Heat ? HeatRise : HeatFall) * dt);
            _soot = Mathf.MoveTowards(_soot, Heat > BurningHeat ? 1f : 0f, (Heat > BurningHeat ? SootRise : SootFall) * dt);

            var burning = Heat > BurningHeat;
            if (burning && !IsBurning)
            {
                CaughtFire?.Invoke();
            }

            IsBurning = burning;
            var visible = Heat > 0f || _soot > 0f;
            overlay.enabled = visible;
            volume.weight = Heat;
            if (!visible)
            {
                return;
            }

            _material.SetFloat(HeatId, Heat);
            _material.SetFloat(SootId, _soot);
            _distortion.intensity.value = Mathf.Sin(Time.time * WobbleSpeed) * WobbleAmount;
        }
    }
}
