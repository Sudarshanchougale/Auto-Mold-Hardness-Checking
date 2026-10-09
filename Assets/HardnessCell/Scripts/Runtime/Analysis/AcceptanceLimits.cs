using System;
using UnityEngine;

namespace HardnessCell.Analysis
{
    /// <summary>Inclusive hardness acceptance range: a reading passes when Minimum &lt;= value &lt;= Maximum.</summary>
    [Serializable]
    public struct AcceptanceLimits
    {
        [SerializeField] private float minimum;
        [SerializeField] private float maximum;

        public AcceptanceLimits(float minimum, float maximum)
        {
            this.minimum = minimum;
            this.maximum = maximum;
        }

        public float Minimum => minimum;
        public float Maximum => maximum;
        public bool IsValid => minimum <= maximum;

        public bool Contains(float value) => value >= minimum && value <= maximum;
    }
}
