using System;
using System.Collections.Generic;
using _Project.City;
using MyToolkit.ParticleFx;
using UnityEngine;
using Zenject;

namespace _Project.FireHose
{
    /// <summary>
    /// Knows every burning building and feeds them the hose water: each frame the water arrives, the fires
    /// whose area holds the landing point are doused there, and steam puffs where it hits a live flame.
    /// Buildings start dormant; <see cref="Burning"/> counts only those on fire right now.
    /// </summary>
    public sealed class FireService : IInitializable, ITickable, IDisposable
    {
        private const float SteamInterval = 0.16f;
        private const float SteamJitter = 0.6f;

        private readonly List<BuildingFire> _fires;
        private readonly WaterNozzle _nozzle;
        private readonly IParticleFxSpawnService<WaterFxId> _fx;
        private float _steamTimer;

        public FireService(List<BuildingFire> fires, WaterNozzle nozzle, IParticleFxSpawnService<WaterFxId> fx)
        {
            _fires = fires;
            _nozzle = nozzle;
            _fx = fx;
        }

        /// <summary>Raised when a building is put out; carries the number still burning.</summary>
        public event Action<int> FireOut;

        /// <summary>Raised when a dormant building catches fire.</summary>
        public event Action<BuildingFire> BuildingIgnited;

        /// <summary>Raised when a building is put out, before <see cref="FireOut"/>.</summary>
        public event Action<BuildingFire> BuildingSaved;

        public IReadOnlyList<BuildingFire> Fires => _fires;
        public int Total => _fires.Count;
        public int Burning { get; private set; }

        /// <summary>The hose stream lands in flames this frame (steam hisses).</summary>
        public bool IsDousing { get; private set; }

        /// <summary>How deep in the flames of any building a point is, 0..1.</summary>
        public float HeatAt(Vector3 point)
        {
            var heat = 0f;
            foreach (var fire in _fires)
            {
                heat = Mathf.Max(heat, fire.HeatAt(point));
            }

            return heat;
        }

        public void Initialize()
        {
            foreach (var fire in _fires)
            {
                Burning += fire.IsBurning ? 1 : 0;
                fire.Ignited += OnIgnited;
                fire.Extinguished += OnExtinguished;
            }
        }

        public void Dispose()
        {
            foreach (var fire in _fires)
            {
                fire.Ignited -= OnIgnited;
                fire.Extinguished -= OnExtinguished;
            }
        }

        public void Tick()
        {
            IsDousing = false;
            if (!_nozzle.IsWaterArriving)
            {
                _steamTimer = 0f;
                return;
            }

            var dt = Time.deltaTime;
            var hit = _nozzle.Hit;
            var hitFlame = false;
            foreach (var fire in _fires)
            {
                if (fire.IsBurning && fire.Area.Contains(hit.point))
                {
                    hitFlame |= fire.Douse(hit.point, hit.normal, dt);
                }
            }

            IsDousing = hitFlame;
            _steamTimer -= dt;
            if (!hitFlame || _steamTimer > 0f)
            {
                return;
            }

            _steamTimer = SteamInterval;
            var jitter = Vector3.ProjectOnPlane(UnityEngine.Random.insideUnitSphere * SteamJitter, hit.normal);
            _fx.SpawnParticle(WaterFxId.Steam, hit.point + hit.normal * 0.4f + jitter);
        }

        private void OnIgnited(BuildingFire fire)
        {
            Burning++;
            BuildingIgnited?.Invoke(fire);
        }

        private void OnExtinguished(BuildingFire fire)
        {
            Burning--;
            BuildingSaved?.Invoke(fire);
            FireOut?.Invoke(Burning);
        }
    }
}
