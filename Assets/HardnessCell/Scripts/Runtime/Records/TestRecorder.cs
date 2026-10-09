using System;
using System.Collections.Generic;
using HardnessCell.Analysis;
using HardnessCell.Measurement;
using HardnessCell.Workflow;
using UnityEngine;

namespace HardnessCell.Records
{
    /// <summary>
    /// Turns each final analysis (PASS/FAIL) into a TestRecord and saves it to TestHistory. Analyses only exist for
    /// fully completed sequences (a reset stops the workflow before Completed), so aborted tests are never saved.
    /// Also notes when each reading arrived, for per-point timestamps.
    /// </summary>
    public sealed class TestRecorder : MonoBehaviour
    {
        [SerializeField] private MeasurementWorkflow workflow;
        [SerializeField] private ResultAnalyzer analyzer;
        [SerializeField] private TestHistory history;

        private readonly Dictionary<string, DateTimeOffset> readingTimes = new Dictionary<string, DateTimeOffset>();

        private void OnEnable()
        {
            if (workflow == null || analyzer == null || history == null)
            {
                Debug.LogError($"{name}: workflow, analyzer or history not assigned; test records will not be saved.", this);
                enabled = false;
                return;
            }
            workflow.StepChanged += OnStepChanged;
            workflow.ReadingRecorded += OnReadingRecorded;
            analyzer.AnalysisCompleted += OnAnalysisCompleted;
        }

        private void OnDisable()
        {
            if (workflow != null)
            {
                workflow.StepChanged -= OnStepChanged;
                workflow.ReadingRecorded -= OnReadingRecorded;
            }
            if (analyzer != null) analyzer.AnalysisCompleted -= OnAnalysisCompleted;
        }

        private void OnStepChanged(WorkflowStep step, string pointId)
        {
            // Discard timestamps of a reset/faulted/interrupted run.
            if (step == WorkflowStep.Idle || step == WorkflowStep.Faulted || step == WorkflowStep.Interrupted) readingTimes.Clear();
        }

        private void OnReadingRecorded(HardnessReading reading) => readingTimes[reading.PointId] = DateTimeOffset.Now;

        private void OnAnalysisCompleted(AnalysisReport report)
        {
            try
            {
                // Defensive: only a sequence that genuinely reached Complete may be recorded.
                if (workflow.Step != WorkflowStep.Complete)
                {
                    Debug.LogWarning($"RECORD: not saved - workflow is {workflow.Step}, not Complete.", this);
                    return;
                }
                if (report.Verdict == QualityVerdict.Incomplete)
                {
                    Debug.LogWarning($"RECORD: not saved - analysis is incomplete ({report.Count}/{report.RequiredCount} points).", this);
                    return;
                }
                string testId = history.NextTestId();
                if (testId == null)
                {
                    Debug.LogError("RECORD: not saved - no test ID available (history storage unavailable).", this);
                    return;
                }
                history.Save(TestRecordFactory.Create(testId, report, DateTimeOffset.Now, readingTimes));
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
                Debug.LogError("RECORD: failed to create the test record.", this);
            }
            finally
            {
                readingTimes.Clear();
            }
        }
    }
}
