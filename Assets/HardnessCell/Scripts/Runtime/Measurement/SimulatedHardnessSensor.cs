using System;
using System.Collections;
using HardnessCell.Mold;
using UnityEngine;

namespace HardnessCell.Measurement
{
    /// <summary>
    /// Deterministic simulated tester: the value depends only on the seed and the point ID,
    /// so every run produces the same readings. Values are whole scale units.
    /// </summary>
    public sealed class SimulatedHardnessSensor : MonoBehaviour, IHardnessSensor
    {
        [SerializeField] private float nominalHardness = 80f;
        [Tooltip("Readings are spread uniformly within nominal ± variation.")]
        [SerializeField] private float variation = 8f;
        [SerializeField] private int seed = 2026;
        [Tooltip("Time the probe dwells in contact while measuring (s).")]
        [SerializeField] private float measurementDuration = 0.75f;
        [Tooltip("Simulation-only fault injection: while set, every measurement attempt fails (no value is produced).")]
        [SerializeField] private bool simulateFault;

        public bool SimulateFault
        {
            get => simulateFault;
            set => simulateFault = value;
        }

        public IEnumerator Measure(MeasurementPoint point, Action<HardnessReading> onComplete, Action<string> onFailed)
        {
            yield return new WaitForSeconds(measurementDuration);
            if (simulateFault)
            {
                onFailed("SENSOR FAULT - NO VALID READING");
                yield break;
            }
            float value = Mathf.Round(nominalHardness + variation * (2f * Noise01(point.PointId) - 1f));
            onComplete(new HardnessReading(point.PointId, value));
        }

        // Stable hash (FNV-1a + murmur finalizer) so values don't depend on string.GetHashCode.
        private float Noise01(string key)
        {
            unchecked
            {
                uint hash = 2166136261u ^ (uint)seed;
                foreach (char c in key)
                {
                    hash ^= c;
                    hash *= 16777619u;
                }
                hash ^= hash >> 16;
                hash *= 0x85ebca6bu;
                hash ^= hash >> 13;
                hash *= 0xc2b2ae35u;
                hash ^= hash >> 16;
                return hash / (float)uint.MaxValue;
            }
        }
    }
}
