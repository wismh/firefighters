using System;
using UnityEngine;
using Zenject;

namespace _Project.Player
{
    /// <summary>The mouse is captured for looking around: Escape lets it go, a click captures it again.</summary>
    public sealed class CursorLock : IInitializable, ITickable, IDisposable
    {
        private readonly HudInputs _inputs;

        public CursorLock(HudInputs inputs)
        {
            _inputs = inputs;
        }

        public bool IsLocked => Cursor.lockState == CursorLockMode.Locked;

        public void Initialize()
        {
            Set(true);
        }

        public void Tick()
        {
            if (_inputs.ReleaseCursor.WasPressedThisFrame())
            {
                Set(false);
            }
            else if (!IsLocked && _inputs.CaptureCursor.WasPressedThisFrame())
            {
                Set(true);
            }
        }

        public void Dispose()
        {
            Set(false);
        }

        private static void Set(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
