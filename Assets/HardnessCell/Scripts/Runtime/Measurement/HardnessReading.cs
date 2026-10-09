namespace HardnessCell.Measurement
{
    /// <summary>One hardness value taken at a measurement point.</summary>
    public readonly struct HardnessReading
    {
        public HardnessReading(string pointId, float value)
        {
            PointId = pointId;
            Value = value;
        }

        public string PointId { get; }
        public float Value { get; }
    }
}
