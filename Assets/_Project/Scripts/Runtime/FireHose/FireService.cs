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

        public IReadOnlyList<BuildingFire> Fires => _fires;
        public int Total => _fires.Count;
        public int Burning { get; private set; }

        public void Initialize()
        {
            Burning = _fires.Count;
            foreach (var fire in _fires)
            {
                fire.Extinguished += OnExtinguished;
            }
        }

        public void Dispose()
        {
            foreach (var fire in _fires)
            {
                fire.Extinguished -= OnExtinguished;
            }
        }

        public void Tick()
        {
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

            _steamTimer -= dt;
            if (!hitFlame || _steamTimer > 0f)
            {
                return;
            }

            _steamTimer = SteamInterval;
            var jitter = Vector3.ProjectOnPlane(UnityEngine.Random.insideUnitSphere * SteamJitter, hit.normal);
            _fx.SpawnParticle(WaterFxId.Steam, hit.point + hit.normal * 0.4f + jitter);
        }

        private void OnExtinguished(BuildingFire fire)
        {
            Burning--;
            FireOut?.Invoke(Burning);
        }
    }
}
