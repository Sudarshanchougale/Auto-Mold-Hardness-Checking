using System;
using System.Collections.Generic;
using HardnessCell.Measurement;

namespace HardnessCell.Analysis
{
    /// <summary>
    /// Pure statistics and acceptance evaluation; no Unity dependencies so storage, export and SPC code can reuse it.
    /// PASS requires a valid reading for every required point and every reading within the inclusive limits;
    /// any reading outside them is FAIL; missing points give Incomplete regardless of the other readings.
    /// </summary>
    public static class ResultAnalysis
    {
        public static AnalysisReport Analyze(string moldId, IReadOnlyList<string> requiredPointIds,
            IEnumerable<HardnessReading> readings, AcceptanceLimits limits)
        {
            if (!limits.IsValid) throw new ArgumentException("Acceptance minimum is greater than maximum.", nameof(limits));

            // Latest valid reading per point; readings for points that are not required are ignored.
            var latest = new Dictionary<string, HardnessReading>();
            foreach (var reading in readings)
            {
                if (reading.PointId != null && IsValidValue(reading.Value)) latest[reading.PointId] = reading;
            }

            var used = new List<HardnessReading>();
            var missing = new List<string>();
            foreach (var id in requiredPointIds)
            {
                if (latest.TryGetValue(id, out var reading)) used.Add(reading);
                else missing.Add(id);
            }

            var failures = new List<PointFailure>();
            double sum = 0;
            float min = float.NaN, max = float.NaN;
            foreach (var reading in used)
            {
                sum += reading.Value;
                min = float.IsNaN(min) ? reading.Value : Math.Min(min, reading.Value);
                max = float.IsNaN(max) ? reading.Value : Math.Max(max, reading.Value);
                if (reading.Value < limits.Minimum) failures.Add(new PointFailure(reading.PointId, reading.Value, FailureReason.BelowMinimum));
                else if (reading.Value > limits.Maximum) failures.Add(new PointFailure(reading.PointId, reading.Value, FailureReason.AboveMaximum));
            }

            int n = used.Count;
            double mean = n > 0 ? sum / n : double.NaN;
            double squares = 0;
            foreach (var reading in used) squares += (reading.Value - mean) * (reading.Value - mean);
            double stdDev = n > 1 ? Math.Sqrt(squares / (n - 1)) : n == 1 ? 0 : double.NaN;

            QualityVerdict verdict = missing.Count > 0 || n == 0 ? QualityVerdict.Incomplete
                : failures.Count > 0 ? QualityVerdict.Fail
                : QualityVerdict.Pass;

            return new AnalysisReport(moldId, limits, requiredPointIds.Count, used, missing, failures,
                (float)mean, min, max, (float)stdDev, verdict);
        }

        private static bool IsValidValue(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
