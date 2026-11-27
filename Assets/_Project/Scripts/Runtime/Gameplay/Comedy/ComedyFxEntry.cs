using System;
using MyToolkit.ParticleFx;

namespace _Project.Comedy
{
    /// <summary>One pooled cartoon effect for the installer.</summary>
    [Serializable]
    public struct ComedyFxEntry
    {
        public ComedyFxId id;
        public PooledParticleFxView prefab;
        public int poolSize;
    }
}
