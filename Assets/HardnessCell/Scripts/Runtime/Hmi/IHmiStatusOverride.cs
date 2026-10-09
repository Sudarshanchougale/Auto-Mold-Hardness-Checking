using System;
using UnityEngine;

namespace HardnessCell.Hmi
{
    /// <summary>
    /// Optional source of a higher-priority main status (e.g. EMERGENCY STOP) for HmiController, so the controller stays
    /// unaware of where the condition comes from.
    /// </summary>
    public interface IHmiStatusOverride
    {
        /// <summary>True (with label and colour) while an overriding condition is active.</summary>
        bool TryGetStatus(out string label, out Color color);

        /// <summary>Raised whenever the override, or anything affecting start permission, may have changed.</summary>
        event Action StatusChanged;
    }
}
