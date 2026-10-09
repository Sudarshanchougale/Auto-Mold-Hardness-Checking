using System;
using System.Collections.Generic;
using HardnessCell.Analysis;

namespace HardnessCell.Records
{
    /// <summary>
    /// Builds a TestRecord from a final AnalysisReport. ResultAnalyzer is the single source of truth: the verdict and
    /// each point's acceptance come from the report's Verdict and Failures; nothing is recalculated here.
    /// </summary>
    public static class TestRecordFactory
    {
        public static TestRecord Create(string testId, AnalysisReport report, DateTimeOffset completedAt,
            IReadOnlyDictionary<string, DateTimeOffset> readingTimes = null)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            if (report.Verdict == QualityVerdict.Incomplete)
                throw new ArgumentException("Only completed PASS/FAIL analyses can become test records.", nameof(report));

            var failures = new Dictionary<string, PointFailure>();
            foreach (var failure in report.Failures) failures[failure.PointId] = failure;

            var measurements = new List<MeasurementRecord>(report.Readings.Count);
            foreach (var reading in report.Readings)
            {
                bool failed = failures.TryGetValue(reading.PointId, out var failure);
                string time = readingTimes != null && readingTimes.TryGetValue(reading.PointId, out var t) ? TestRecord.FormatTimestamp(t) : "";
                measurements.Add(new MeasurementRecord(reading.PointId, reading.Value, time, !failed,
                    failed ? PointFailure.Describe(failure.Reason) : ""));
            }

            return new TestRecord(testId, report.MoldId, completedAt, report.Count,
                report.Average, report.Minimum, report.Maximum, report.Range, report.StandardDeviation,
                report.Limits.Minimum, report.Limits.Maximum, report.Verdict == QualityVerdict.Pass, measurements);
        }
    }
}
