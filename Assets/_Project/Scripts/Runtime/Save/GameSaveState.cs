using System;

namespace _Project.Save
{
    [Serializable]
    public sealed class GameSaveState
    {
        public int Version { get; set; }

        public float MasterVolume { get; set; } = 1f;

        public float MusicVolume { get; set; } = 1f;

        public float SfxVolume { get; set; } = 1f;
    }
}
