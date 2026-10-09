using System;
using UnityEngine;

namespace HardnessCell.Records
{
    /// <summary>One measured point inside a TestRecord. Serialized with JsonUtility, so state lives in fields.</summary>
    [Serializable]
    public sealed class MeasurementRecord
    {
        [SerializeField] private string pointId;
        [SerializeField] private float hardness;
        [Tooltip("ISO 8601 with offset (TestRecord.TimestampFormat); empty if unknown.")]
        [SerializeField] private string timestamp;
        [SerializeField] private bool withinLimits;
        [Tooltip("Empty when within limits, otherwise e.g. BELOW MINIMUM.")]
        [SerializeField] private string failureReason;

        public MeasurementRecord(string pointId, float hardness, string timestamp, bool withinLimits, string failureReason)
        {
            this.pointId = pointId;
            this.hardness = hardness;
            this.timestamp = timestamp ?? "";
            this.withinLimits = withinLimits;
            this.failureReason = failureReason ?? "";
        }

        public string PointId => pointId;
        public float Hardness => hardness;
        public string Timestamp => timestamp ?? "";
        public bool WithinLimits => withinLimits;
        public string FailureReason => failureReason ?? "";
    }
}
