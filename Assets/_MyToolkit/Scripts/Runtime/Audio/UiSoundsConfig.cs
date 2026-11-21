using System;
using UnityEngine;

namespace MyToolkit.Audio
{
    [Serializable]
    public sealed class UiSoundsConfig
    {
        [SerializeField] private SoundData _buttonClick;

        public SoundData ButtonClick => _buttonClick;
    }
}
