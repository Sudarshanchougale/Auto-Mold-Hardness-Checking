using System.Collections.Generic;
using System.Globalization;
using HardnessCell.Analysis;
using HardnessCell.Cell;
using HardnessCell.Measurement;
using HardnessCell.Workflow;
using UnityEngine;
using UnityEngine.UI;

namespace HardnessCell.Hmi
{
    /// <summary>
    /// Operator HMI. Observes MeasurementWorkflow and redraws everything from its state (Step, CurrentPointId,
    /// IsRunning, Results) on each workflow event; holds no test state of its own. Buttons only issue
    /// StartWorkflow / ResetWorkflow commands.
    /// Main status priority: safety override (E-STOP, GATE OPEN, ROBOT FAULT, SENSOR FAULT) > workflow state;
    /// on Complete it shows ANALYZING until ResultAnalyzer has a report, then PASS / FAIL.
    /// </summary>
    public sealed class HmiController : MonoBehaviour
    {
        [SerializeField] private MeasurementWorkflow workflow;
        [SerializeField] private TestingCell cell;

        [Header("Header")]
        [SerializeField] private Text moldIdText;

        [Header("Status")]
        [SerializeField] private Image statusBadge;
        [SerializeField] private Text statusText;

        [Header("Current measurement")]
        [SerializeField] private Text currentPointText;
        [SerializeField] private Text currentStepText;
        [SerializeField] private Text currentReadingText;

        [Header("Progress")]
        [SerializeField] private Text progressText;
        [Tooltip("Fill bar; its anchorMax.x is set to the completed fraction.")]
        [SerializeField] private RectTransform progressFill;

        [Header("Results")]
        [SerializeField] private PointResultTile[] pointTiles = new PointResultTile[0];

        [Header("Controls")]
        [SerializeField] private Button startButton;
        [SerializeField] private Button resetButton;

        [Header("Status override (optional)")]
        [Tooltip("Component implementing IHmiStatusOverride (e.g. the safety panel); its status takes priority over the workflow status.")]
        [SerializeField] private MonoBehaviour statusOverrideSource;

        [Header("Result (optional)")]
        [Tooltip("When assigned, the main status shows ANALYZING / PASS / FAIL instead of COMPLETE.")]
        [SerializeField] private ResultAnalyzer resultAnalyzer;

        private readonly Dictionary<string, float> valuesByPoint = new Dictionary<string, float>();
        private IHmiStatusOverride statusOverride;

        private void OnValidate()
        {
            if (statusOverrideSource != null && !(statusOverrideSource is IHmiStatusOverride))
            {
                Debug.LogWarning($"{name}: {statusOverrideSource.GetType().Name} does not implement IHmiStatusOverride.", this);
                statusOverrideSource = null;
            }
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
            workflow.ReadingRecorded += OnReadingRecorded;
            workflow.Completed += OnCompleted;
            startButton.onClick.AddListener(OnStartPressed);
            resetButton.onClick.AddListener(OnResetPressed);
            statusOverride = statusOverrideSource as IHmiStatusOverride;
            if (statusOverride != null) statusOverride.StatusChanged += Refresh;
            // The analyzer may handle workflow.Completed after this component does, so refresh on its own events too.
            if (resultAnalyzer != null)
            {
                resultAnalyzer.AnalysisCompleted += OnAnalysisCompleted;
                resultAnalyzer.AnalysisCleared += Refresh;
            }
            Refresh();
        }

        private void OnDisable()
        {
            if (workflow == null) return;
            workflow.StepChanged -= OnStepChanged;
            workflow.ReadingRecorded -= OnReadingRecorded;
            workflow.Completed -= OnCompleted;
            startButton.onClick.RemoveListener(OnStartPressed);
            resetButton.onClick.RemoveListener(OnResetPressed);
            if (statusOverride != null) statusOverride.StatusChanged -= Refresh;
            if (resultAnalyzer != null)
            {
                resultAnalyzer.AnalysisCompleted -= OnAnalysisCompleted;
                resultAnalyzer.AnalysisCleared -= Refresh;
            }
        }

        // Covers the workflow auto-starting before or after this component's OnEnable.
        private void Start() => Refresh();

        private void OnStepChanged(WorkflowStep step, string pointId) => Refresh();
        private void OnReadingRecorded(HardnessReading reading) => Refresh();
        private void OnCompleted(MeasurementResults results) => Refresh();
        private void OnAnalysisCompleted(AnalysisReport report) => Refresh();

        private void OnStartPressed()
        {
            // StartWorkflow checks interlocks itself (and logs START BLOCKED); the button is also disabled when not permitted.
            workflow.StartWorkflow();
            Refresh();
        }

        private void OnResetPressed()
        {
            workflow.ResetWorkflow();
            Refresh();
        }

        public void Refresh()
        {
            WorkflowStep step = workflow.Step;
            bool running = workflow.IsRunning;
            string currentPoint = running ? workflow.CurrentPointId : null;

            var readings = workflow.Results.Readings;
            valuesByPoint.Clear();
            foreach (var reading in readings) valuesByPoint[reading.PointId] = reading.Value;

            if (moldIdText != null) moldIdText.text = cell != null && cell.Mold != null ? cell.Mold.MoldId : "--";

            if (statusOverride != null && statusOverride.TryGetStatus(out string overrideLabel, out Color overrideColor))
            {
                statusText.text = overrideLabel;
                statusBadge.color = overrideColor;
            }
            else
            {
                ResolveWorkflowStatus(step, out string label, out Color color);
                statusText.text = label;
                statusBadge.color = color;
            }

            currentPointText.text = currentPoint ?? "--";
            currentStepText.text = StepLabel(step);
            // While testing, show the active point's reading; otherwise the most recent one.
            float? currentValue = null;
            if (currentPoint != null)
            {
                if (valuesByPoint.TryGetValue(currentPoint, out float value)) currentValue = value;
            }
            else if (readings.Count > 0)
            {
                currentValue = readings[readings.Count - 1].Value;
            }
            currentReadingText.text = Format(currentValue);

            int total = TotalPoints();
            progressText.text = $"{readings.Count} / {total}";
            progressFill.anchorMax = new Vector2(total > 0 ? Mathf.Clamp01(readings.Count / (float)total) : 0f, 1f);

            foreach (var tile in pointTiles)
            {
                if (tile == null) continue;
                float? tileValue = valuesByPoint.TryGetValue(tile.PointId, out float v) ? v : (float?)null;
                tile.Show(tileValue, tile.PointId == currentPoint);
            }

            startButton.interactable = workflow.CanStart(out _);
            resetButton.interactable = workflow.CanReset(out _);
        }

        private void ResolveWorkflowStatus(WorkflowStep step, out string label, out Color color)
        {
            if (step == WorkflowStep.Complete && resultAnalyzer != null)
            {
                var report = resultAnalyzer.LatestReport;
                if (report == null)
                {
                    label = "ANALYZING";
                    color = HmiPalette.StatusTesting;
                }
                else if (report.Verdict == QualityVerdict.Pass)
                {
                    label = "PASS";
                    color = HmiPalette.ResultPass;
                }
                else if (report.Verdict == QualityVerdict.Fail)
                {
                    label = "FAIL";
                    color = HmiPalette.ResultFail;
                }
                else
                {
                    label = "INCOMPLETE";
                    color = HmiPalette.ResultIncomplete;
                }
                return;
            }
            label = StatusLabel(step);
            color = StatusColor(step);
        }

        private int TotalPoints()
        {
            if (cell != null && cell.Mold != null && cell.Mold.MeasurementPoints != null) return cell.Mold.MeasurementPoints.Count;
            return pointTiles.Length;
        }

        private static string Format(float? value) =>
            value.HasValue ? value.Value.ToString("0.#", CultureInfo.InvariantCulture) : "--";

        private static string StatusLabel(WorkflowStep step)
        {
            switch (step)
            {
                case WorkflowStep.Idle: return "READY";
                case WorkflowStep.Contact: return "MEASURING";
                case WorkflowStep.Resetting: return "RESETTING";
                case WorkflowStep.Complete: return "COMPLETE";
                case WorkflowStep.Faulted: return "FAULT";
                case WorkflowStep.Paused: return "PAUSED";
                case WorkflowStep.Interrupted: return "RESET REQUIRED";
                default: return "RUNNING";
            }
        }

        private static Color StatusColor(WorkflowStep step)
        {
            switch (step)
            {
                case WorkflowStep.Idle: return HmiPalette.StatusReady;
                case WorkflowStep.Contact: return HmiPalette.StatusMeasuring;
                case WorkflowStep.Resetting: return HmiPalette.StatusResetting;
                case WorkflowStep.Complete: return HmiPalette.StatusComplete;
                case WorkflowStep.Faulted: return HmiPalette.StatusFault;
                case WorkflowStep.Paused: return HmiPalette.StatusPaused;
                case WorkflowStep.Interrupted: return HmiPalette.StatusResetting;
                default: return HmiPalette.StatusTesting;
            }
        }

        private static string StepLabel(WorkflowStep step)
        {
            switch (step)
            {
                case WorkflowStep.Approach: return "APPROACH";
                case WorkflowStep.Descend: return "DESCEND";
                case WorkflowStep.Contact: return "CONTACT";
                case WorkflowStep.Retract: return "RETRACT";
                case WorkflowStep.ReturnHome:
                case WorkflowStep.Resetting: return "RETURN HOME";
                case WorkflowStep.Paused: return "PAUSED";
                default: return "--";
            }
        }
    }
}
