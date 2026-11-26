using System;
using _Project.Audio;
using MyToolkit.Audio;

namespace _Project.Comedy
{
    /// <summary>The sound for each cartoon moment.</summary>
    [Serializable]
    public sealed class ComedySounds
    {
        public SoundData bigCrash;
        public SoundData bigCrashThud;
        public SoundData smallCrash;
        public SoundData propDown;
        public SoundData knockout;
        public SoundData geyser;
        public SoundData driverSoaked;
        public SoundData coins;
        public SoundData buildingSaved;
        public SoundData allSaved;
        public SoundData firework;
        public SoundData horn;
        public SoundData boing;
        public SoundData whoosh;
        public SoundBank moneyDrop;

        [UnityEngine.Header("Voices")]
        public SoundBank pedestrianScream;
        public SoundBank pedestrianHit;
        public SoundBank pedestrianSoaked;
        public SoundBank ghostLaugh;
        public SoundBank driverAngry;
        public SoundBank driverKnocked;
        public SoundBank crowdCheer;
        public SoundBank playerBurning;
        public SoundBank playerCheer;
        public SoundBank playerThrown;
    }
}
