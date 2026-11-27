using System.Collections.Generic;
using _Project.Player;
using UnityEngine;
using Zenject;

namespace _Project.Comedy
{
    /// <summary>A world-space trail of stars behind the player's car while it flies.</summary>
    public sealed class FlightTrail : MonoBehaviour
    {
        [Tooltip("Seconds in the air before the trail starts, so a bump in the road leaves none.")]
        [SerializeField] private float airTimeForTrail = 0.2f;

        private readonly List<ParticleSystem> _systems = new List<ParticleSystem>(4);

        private IPlayerState _player;
        private bool _on = true;

        [Inject]
        public void Construct(IPlayerState player)
        {
            _player = player;
        }

        private void Start()
        {
            GetComponentsInChildren(true, _systems);
            Set(false);
        }

        private void Update()
        {
            var car = _player.Driven;
            var flying = car != null && car.AirTime > airTimeForTrail;
            Set(flying);
            if (flying)
            {
                transform.position = car.CameraTarget.position;
            }
        }

        private void Set(bool on)
        {
            if (on == _on)
            {
                return;
            }

            _on = on;
            for (var i = 0; i < _systems.Count; i++)
            {
                var emission = _systems[i].emission;
                emission.enabled = on;
            }

            if (on)
            {
                transform.position = _player.Driven.CameraTarget.position;
                _systems[0].Play(true);
            }
        }
    }
}
