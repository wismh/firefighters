using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;

namespace _Project.Player
{
    /// <summary>Player action map of InputSystem_Actions. Driving reuses the same actions.</summary>
    public sealed class PlayerInputs : IInitializable, IDisposable
    {
        private readonly InputActionMap _map;

        public PlayerInputs(InputActionAsset asset)
        {
            _map = asset.FindActionMap("Player", true);
            Move = _map.FindAction("Move", true);
            Look = _map.FindAction("Look", true);
            Jump = _map.FindAction("Jump", true);
            Sprint = _map.FindAction("Sprint", true);
            Interact = _map.FindAction("Interact", true);
            Attack = _map.FindAction("Attack", true);
        }

        public InputAction Move { get; }
        public InputAction Look { get; }
        public InputAction Jump { get; }
        public InputAction Sprint { get; }
        public InputAction Interact { get; }
        public InputAction Attack { get; }

        /// <summary>Look delta in degrees for this frame; mouse is per-pixel, stick is per-second.</summary>
        public Vector2 LookDelta(float mouseSensitivity, float stickSpeed)
        {
            var value = Look.ReadValue<Vector2>();
            var fromMouse = Look.activeControl?.device is Pointer;
            return fromMouse ? value * mouseSensitivity : value * (stickSpeed * Time.deltaTime);
        }

        public void Initialize() => _map.Enable();

        /// <summary>Turns the player's controls off and on, e.g. while the dispatcher speaks.</summary>
        public void SetPlayerEnabled(bool enabled)
        {
            if (enabled)
            {
                _map.Enable();
            }
            else
            {
                _map.Disable();
            }
        }

        public void Dispose() => _map.Disable();
    }
}
