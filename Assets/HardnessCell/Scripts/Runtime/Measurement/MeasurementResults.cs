using System.Collections.Generic;

namespace HardnessCell.Measurement
{
    /// <summary>In-memory readings for the current mold, in measurement order.</summary>
    public sealed class MeasurementResults
    {
        private readonly List<HardnessReading> readings = new List<HardnessReading>();

        public IReadOnlyList<HardnessReading> Readings => readings;
        public int Count => readings.Count;

        public void Add(HardnessReading reading) => readings.Add(reading);
        public void Clear() => readings.Clear();
    }
}
