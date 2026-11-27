using UnityEngine;

namespace _Project.Fire
{
    /// <summary>Emission helpers for groups of particle systems that fade with a strength.</summary>
    public static class ParticleRates
    {
        public static void Clear(ParticleSystem[] systems)
        {
            foreach (var system in systems)
            {
                system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        public static void Play(ParticleSystem[] systems)
        {
            foreach (var system in systems)
            {
                system.Play(false);
            }
        }

        public static void Stop(ParticleSystem[] systems)
        {
            foreach (var system in systems)
            {
                system.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        /// <summary>The emission rate each system was authored with.</summary>
        public static float[] BaseRates(ParticleSystem[] systems)
        {
            var rates = new float[systems.Length];
            for (var i = 0; i < systems.Length; i++)
            {
                rates[i] = systems[i].emission.rateOverTimeMultiplier;
            }

            return rates;
        }

        public static void SetRates(ParticleSystem[] systems, float[] baseRates, float factor)
        {
            for (var i = 0; i < systems.Length; i++)
            {
                var emission = systems[i].emission;
                emission.rateOverTimeMultiplier = baseRates[i] * factor;
            }
        }
    }
}
