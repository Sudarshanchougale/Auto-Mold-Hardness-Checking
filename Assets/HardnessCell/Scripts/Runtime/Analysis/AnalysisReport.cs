using System.Collections.Generic;
using HardnessCell.Measurement;

namespace HardnessCell.Analysis
{
    /// <summary>
    /// Immutable result of analysing one mold's readings. Holds its own snapshot of the analysed readings so it stays
    /// valid after the workflow clears its results (reset / next test); later storage and reports can use it as-is.
    /// Statistics are NaN when Count is 0. StandardDeviation is the sample standard deviation (n - 1), 0 when Count &lt; 2.
    /// </summary>
    public sealed class AnalysisReport
    {
        public AnalysisReport(string moldId, AcceptanceLimits limits, int requiredCount,
            IReadOnlyList<HardnessReading> readings, IReadOnlyList<string> missingPointIds, IReadOnlyList<PointFailure> failures,
            float average, float minimum, float maximum, float standardDeviation, QualityVerdict verdict)
        {
            MoldId = moldId;
            Limits = limits;
            RequiredCount = requiredCount;
            Readings = readings;
            MissingPointIds = missingPointIds;
            Failures = failures;
            Average = average;
            Minimum = minimum;
            Maximum = maximum;
            StandardDeviation = standardDeviation;
            Verdict = verdict;
        }

        public string MoldId { get; }
        public AcceptanceLimits Limits { get; }
        public int RequiredCount { get; }
        /// <summary>Valid readings used for the analysis, in required-point order.</summary>
        public IReadOnlyList<HardnessReading> Readings { get; }
        public IReadOnlyList<string> MissingPointIds { get; }
        public IReadOnlyList<PointFailure> Failures { get; }
        public int Count => Readings.Count;
        public float Average { get; }
        public float Minimum { get; }
        public float Maximum { get; }
        public float Range => Maximum - Minimum;
        public float StandardDeviation { get; }
        public QualityVerdict Verdict { get; }
    }
}
