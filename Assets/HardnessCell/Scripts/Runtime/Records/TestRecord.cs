using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HardnessCell.Records
{
    /// <summary>
    /// One completed mold hardness test (PASS or FAIL only). Created from an AnalysisReport by TestRecordFactory;
    /// the verdict and per-point acceptance are copied from the report, never re-evaluated.
    /// Serialized with JsonUtility, so state lives in fields; the public API is read-only.
    /// </summary>
    [Serializable]
    public sealed class TestRecord
    {
        /// <summary>Stored timestamp format: local time with UTC offset, e.g. 2026-10-08T13:30:00+05:30.</summary>
        public const string TimestampFormat = "yyyy-MM-ddTHH:mm:sszzz";
        public const string DisplayTimestampFormat = "yyyy-MM-dd HH:mm:ss";
        public const string PassResult = "PASS";
        public const string FailResult = "FAIL";

        [SerializeField] private string testId;
        [SerializeField] private string moldId;
        [SerializeField] private string timestamp;
        [SerializeField] private int measurementCount;
        [SerializeField] private float average;
        [SerializeField] private float minimum;
        [SerializeField] private float maximum;
        [SerializeField] private float range;
        [SerializeField] private float standardDeviation;
        [SerializeField] private float minimumLimit;
        [SerializeField] private float maximumLimit;
        [SerializeField] private string result;
        [SerializeField] private List<MeasurementRecord> measurements;

        public TestRecord(string testId, string moldId, DateTimeOffset timestamp, int measurementCount,
            float average, float minimum, float maximum, float range, float standardDeviation,
            float minimumLimit, float maximumLimit, bool passed, List<MeasurementRecord> measurements)
        {
            this.testId = testId;
            this.moldId = moldId;
            this.timestamp = FormatTimestamp(timestamp);
            this.measurementCount = measurementCount;
            this.average = average;
            this.minimum = minimum;
            this.maximum = maximum;
            this.range = range;
            this.standardDeviation = standardDeviation;
            this.minimumLimit = minimumLimit;
            this.maximumLimit = maximumLimit;
            result = passed ? PassResult : FailResult;
            this.measurements = measurements;
        }

        public string TestId => testId;
        public string MoldId => moldId;
        public string Timestamp => timestamp ?? "";
        public int MeasurementCount => measurementCount;
        public float Average => average;
        public float Minimum => minimum;
        public float Maximum => maximum;
        public float Range => range;
        public float StandardDeviation => standardDeviation;
        public float MinimumLimit => minimumLimit;
        public float MaximumLimit => maximumLimit;
        /// <summary>"PASS" or "FAIL".</summary>
        public string Result => result ?? "";
        public bool Passed => result == PassResult;
        public IReadOnlyList<MeasurementRecord> Measurements => (IReadOnlyList<MeasurementRecord>)measurements ?? Array.Empty<MeasurementRecord>();

        public static string FormatTimestamp(DateTimeOffset time) => time.ToString(TimestampFormat, CultureInfo.InvariantCulture);

        /// <summary>Stored timestamp as "yyyy-MM-dd HH:mm:ss" (the local time it was recorded in), or the raw text if unparseable.</summary>
        public static string ToDisplayTimestamp(string storedTimestamp)
        {
            if (string.IsNullOrEmpty(storedTimestamp)) return "";
            return DateTimeOffset.TryParseExact(storedTimestamp, TimestampFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)
                ? time.ToString(DisplayTimestampFormat, CultureInfo.InvariantCulture)
                : storedTimestamp;
        }
    }
}
