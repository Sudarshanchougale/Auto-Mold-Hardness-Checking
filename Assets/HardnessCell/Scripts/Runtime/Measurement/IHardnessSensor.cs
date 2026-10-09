using System;
using System.Collections;
using HardnessCell.Mold;

namespace HardnessCell.Measurement
{
    /// <summary>
    /// Takes a hardness reading while the probe is in contact with a point. Implementations call exactly one of
    /// onComplete (valid reading) or onFailed (reason) per Measure call.
    /// The simulation implements this with generated values; a real tester interface can implement it instead.
    /// </summary>
    public interface IHardnessSensor
    {
        IEnumerator Measure(MeasurementPoint point, Action<HardnessReading> onComplete, Action<string> onFailed);
    }
}
