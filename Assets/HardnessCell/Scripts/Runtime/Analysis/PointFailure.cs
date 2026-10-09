namespace HardnessCell.Analysis
{
    public enum FailureReason
    {
        BelowMinimum,
        AboveMaximum,
    }

    /// <summary>A measured point whose reading is outside the acceptance limits.</summary>
    public readonly struct PointFailure
    {
        public PointFailure(string pointId, float value, FailureReason reason)
        {
            PointId = pointId;
            Value = value;
            Reason = reason;
        }

        public string PointId { get; }
        public float Value { get; }
        public FailureReason Reason { get; }

        /// <summary>Operator/report wording, e.g. "BELOW MINIMUM".</summary>
        public static string Describe(FailureReason reason) =>
            reason == FailureReason.BelowMinimum ? "BELOW MINIMUM" : "ABOVE MAXIMUM";
    }
}
