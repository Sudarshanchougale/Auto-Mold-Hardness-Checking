using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HardnessCell.Cell;
using HardnessCell.Measurement;
using HardnessCell.Workflow;
using UnityEngine;

namespace HardnessCell.Analysis
{
    /// <summary>
    /// Connects ResultAnalysis to the workflow: analyses the results when the workflow raises Completed, and clears the
    /// report as soon as the workflow leaves Complete (reset, new test, fault). Owns the configurable acceptance limits.
    /// </summary>
    public sealed class ResultAnalyzer : MonoBehaviour
    {
        [SerializeField] private MeasurementWorkflow workflow;
        [SerializeField] private TestingCell cell;
        [Tooltip("Inclusive hardness acceptance range. A later recipe system can supply these via SetLimits.")]
        [SerializeField] private AcceptanceLimits limits = new AcceptanceLimits(80f, 95f);

        public AcceptanceLimits Limits => limits;
        /// <summary>Report for the last completed test, or null while no completed test is available.</summary>
        public AnalysisReport LatestReport { get; private set; }

        public event Action<AnalysisReport> AnalysisCompleted;
        public event Action AnalysisCleared;

        private void Awake()
        {
            if (cell == null) cell = GetComponentInParent<TestingCell>();
            if (workflow == null && cell != null) workflow = cell.GetComponentInChildren<MeasurementWorkflow>(true);
        }

        private void OnEnable()
        {
            if (workflow == null)
            {
                Debug.LogError($"{name}: no MeasurementWorkflow assigned.", this);
                enabled = false;
                return;
            }
            workflow.StepChanged += OnStepChanged;
            workflow.Completed += OnCompleted;
        }

        private void OnDisable()
        {
            if (workflow == null) return;
            workflow.StepChanged -= OnStepChanged;
            workflow.Completed -= OnCompleted;
        }

        private void OnValidate()
        {
            if (!limits.IsValid) limits = new AcceptanceLimits(limits.Minimum, limits.Minimum);
        }

        public void SetLimits(AcceptanceLimits newLimits)
        {
            if (!newLimits.IsValid) throw new ArgumentException("Acceptance minimum is greater than maximum.", nameof(newLimits));
            limits = newLimits;
        }

        private void OnStepChanged(WorkflowStep step, string pointId)
        {
            if (step != WorkflowStep.Complete) Clear();
        }

        private void OnCompleted(MeasurementResults results)
        {
            LatestReport = ResultAnalysis.Analyze(MoldId(), RequiredPointIds(), results.Readings, limits);
            Debug.Log(Describe(LatestReport), this);
            AnalysisCompleted?.Invoke(LatestReport);
        }

        private void Clear()
        {
            if (LatestReport == null) return;
            LatestReport = null;
            AnalysisCleared?.Invoke();
        }

        private string MoldId() => cell != null && cell.Mold != null ? cell.Mold.MoldId : "";

        private IReadOnlyList<string> RequiredPointIds()
        {
            var ids = new List<string>();
            if (cell != null && cell.Mold != null && cell.Mold.MeasurementPoints != null)
            {
                foreach (var point in cell.Mold.MeasurementPoints.Points)
                    if (point != null) ids.Add(point.PointId);
            }
            return ids;
        }

        private static string Describe(AnalysisReport report)
        {
            var c = CultureInfo.InvariantCulture;
            var text = new StringBuilder();
            text.AppendLine(string.Format(c, "ANALYSIS: {0} ({1}, target {2:0.#}-{3:0.#})",
                report.Verdict.ToString().ToUpperInvariant(), report.MoldId, report.Limits.Minimum, report.Limits.Maximum));
            text.AppendLine(string.Format(c, "  Count {0}/{1}  Avg {2:0.0}  Min {3:0.#}  Max {4:0.#}  Range {5:0.#}  StdDev {6:0.00}",
                report.Count, report.RequiredCount, report.Average, report.Minimum, report.Maximum, report.Range, report.StandardDeviation));
            foreach (var failure in report.Failures)
                text.AppendLine(string.Format(c, "  {0} = {1:0.#}  {2}", failure.PointId, failure.Value, PointFailure.Describe(failure.Reason)));
            foreach (var id in report.MissingPointIds) text.AppendLine("  " + id + " = --  NOT MEASURED");
            return text.ToString().TrimEnd();
        }
    }
}
