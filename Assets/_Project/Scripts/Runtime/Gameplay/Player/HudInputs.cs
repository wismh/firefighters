using System;
using UnityEngine.InputSystem;
using Zenject;

namespace _Project.Player
{
    /// <summary>
    /// Hud action map of InputSystem_Actions: turning the dispatcher's lines, skipping a monologue and the cursor.
    /// Stays on while the Player map is off for a monologue.
    /// </summary>
    public sealed class HudInputs : IInitializable, IDisposable
    {
        private readonly InputActionMap _map;

        public HudInputs(InputActionAsset asset)
        {
            _map = asset.FindActionMap("Hud", true);
            Advance = _map.FindAction("Advance", true);
            Skip = _map.FindAction("Skip", true);
            ReleaseCursor = _map.FindAction("ReleaseCursor", true);
            CaptureCursor = _map.FindAction("CaptureCursor", true);
        }

        /// <summary>Finishes the line being typed or turns to the next one.</summary>
        public InputAction Advance { get; }

        /// <summary>Skips the rest of a monologue.</summary>
        public InputAction Skip { get; }

        public InputAction ReleaseCursor { get; }

        public InputAction CaptureCursor { get; }

        public void Initialize() => _map.Enable();

        public void Dispose() => _map.Disable();
    }
}
