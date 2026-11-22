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
        }

        public InputAction Move { get; }
        public InputAction Look { get; }
        public InputAction Jump { get; }
        public InputAction Sprint { get; }
        public InputAction Interact { get; }

        /// <summary>Look delta in degrees for this frame; mouse is per-pixel, stick is per-second.</summary>
        public Vector2 LookDelta(float mouseSensitivity, float stickSpeed)
        {
            var value = Look.ReadValue<Vector2>();
            var fromMouse = Look.activeControl?.device is Pointer;
            return fromMouse ? value * mouseSensitivity : value * (stickSpeed * Time.deltaTime);
        }

        public void Initialize() => _map.Enable();

        public void Dispose() => _map.Disable();
    }
}
