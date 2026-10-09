using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using HardnessCell.Cell;
using HardnessCell.Measurement;
using HardnessCell.Mold;
using HardnessCell.Robot;
using UnityEngine;

namespace HardnessCell.Workflow
{
    /// <summary>
    /// Automated measurement sequence: for each point in MeasurementPointSet order, approach a safe position above it,
    /// descend to contact, take a reading, retract; then return home and log a summary.
    /// Motion and readings come from IRobotMotion / IHardnessSensor components on the same GameObject.
    /// Stage 6: an optional IWorkflowInterlock gates start/resume/recovery motion; the test can be paused/resumed,
    /// interrupted (immediate stop, discarded) or faulted by a failed measurement (discarded, safe retract + home).
    /// Completed is raised only when every point has exactly one reading.
    /// </summary>
    public sealed class MeasurementWorkflow : MonoBehaviour
    {
        [SerializeField] private TestingCell cell;
        [SerializeField] private bool runOnStart = true;

        [Header("Motion")]
        [Tooltip("Height of the safe transfer position above each point, along its surface normal (m).")]
        [SerializeField] private float safeHeight = 0.15f;
        [Tooltip("Tool speed between safe positions (m/s).")]
        [SerializeField] private float transferSpeed = 0.4f;
        [Tooltip("Tool speed from the safe position down to the probe's approach distance (m/s).")]
        [SerializeField] private float descentSpeed = 0.15f;
        [Tooltip("Tool speed over the final approach distance into contact (m/s).")]
        [SerializeField] private float contactSpeed = 0.03f;
        [Tooltip("Tool speed back up to the safe position (m/s).")]
        [SerializeField] private float retractSpeed = 0.2f;

        private readonly MeasurementResults results = new MeasurementResults();
        private IRobotMotion motion;
        private IHardnessSensor sensor;

        // Test progress, kept across pause/resume.
        private IReadOnlyList<MeasurementPoint> points;
        private int currentIndex;
        private bool leftHome;
        private bool active;      // a test or a recovery is in progress (running, paused or resetting)
        private bool paused;
        private bool recovering;

        public WorkflowStep Step { get; private set; } = WorkflowStep.Idle;
        public string CurrentPointId { get; private set; }
        /// <summary>True while a test is in progress (including paused) or a recovery move is running.</summary>
        public bool IsRunning => active;
        public bool IsPaused => paused;
        /// <summary>The arm may be away from home after an interrupted/faulted run; a reset (recovery) is required before starting.</summary>
        public bool NeedsRecovery { get; private set; }
        public MeasurementResults Results => results;
        /// <summary>Optional start/resume/motion permissive (the safety system). Null means always permitted.</summary>
        public IWorkflowInterlock Interlock { get; set; }

        public event Action<WorkflowStep, string> StepChanged;
        public event Action<HardnessReading> ReadingRecorded;
        public event Action<MeasurementResults> Completed;
        /// <summary>A start was refused; argument is the reason.</summary>
        public event Action<string> StartBlocked;
        /// <summary>An in-progress test was discarded (interrupt or measurement failure); argument is the reason.</summary>
        public event Action<string> TestDiscarded;
        /// <summary>A measurement attempt failed: point ID and reason.</summary>
        public event Action<string, string> MeasurementFailed;

        private void Awake()
        {
            if (cell == null) cell = GetComponentInParent<TestingCell>();
            motion = GetComponent<IRobotMotion>();
            sensor = GetComponent<IHardnessSensor>();
        }

        private void Start()
        {
            if (runOnStart) StartWorkflow();
        }

        private void OnDisable()
        {
            if (!active) return;
            StopSequence();
            Debug.LogWarning("WORKFLOW: ABORTED (component disabled)", this);
            SetStep(WorkflowStep.Idle, null);
        }

        // ---------- Commands ----------

        public bool CanStart(out string reason)
        {
            if (active)
            {
                reason = paused ? "TEST PAUSED" : recovering ? "RECOVERY IN PROGRESS" : "TEST IN PROGRESS";
                return false;
            }
            if (NeedsRecovery)
            {
                reason = "RESET REQUIRED";
                return false;
            }
            if (Interlock != null && !Interlock.CanRun(out reason)) return false;
            reason = null;
            return true;
        }

        /// <summary>Checks interlocks, validates the cell and starts the sequence; returns false if blocked or validation fails.</summary>
        public bool StartWorkflow()
        {
            if (!CanStart(out string reason))
            {
                Debug.LogWarning("START BLOCKED: " + reason, this);
                StartBlocked?.Invoke(reason);
                return false;
            }
            results.Clear();
            if (!TryGetValidatedPoints(out var validated))
            {
                SetStep(WorkflowStep.Faulted, null);
                return false;
            }
            points = validated;
            currentIndex = 0;
            leftHome = false;
            active = true;
            Debug.Log($"WORKFLOW: START ({cell.Mold.MoldId}, {points.Count} points)", this);
            StartCoroutine(Run());
            return true;
        }

        public bool CanPause => active && !paused && !recovering;

        /// <summary>Stops the sequence where it is (motion and measurement halt) but keeps the test for Resume.</summary>
        public bool Pause()
        {
            if (!CanPause) return false;
            StopAllCoroutines();
            paused = true;
            Debug.Log($"WORKFLOW: PAUSED{(CurrentPointId != null ? " at " + CurrentPointId : "")}", this);
            SetStep(WorkflowStep.Paused, CurrentPointId);
            return true;
        }

        public bool CanResume(out string reason)
        {
            if (!paused)
            {
                reason = "NOT PAUSED";
                return false;
            }
            if (Interlock != null && !Interlock.CanRun(out reason)) return false;
            reason = null;
            return true;
        }

        /// <summary>
        /// Continues a paused test after re-checking interlocks. A point whose reading was not yet recorded is redone from
        /// its safe position; a point already recorded continues with its retract, so no reading is ever duplicated.
        /// </summary>
        public bool Resume()
        {
            if (!CanResume(out string reason))
            {
                if (paused) Debug.LogWarning("RESUME BLOCKED: " + reason, this);
                return false;
            }
            paused = false;
            Debug.Log($"WORKFLOW: RESUMED{(CurrentPointId != null ? " at " + CurrentPointId : "")}", this);
            StartCoroutine(Run());
            return true;
        }

        /// <summary>
        /// Immediate stop for a safety condition: motion and measurement stop where they are, the test (or recovery)
        /// is discarded, nothing moves, and a reset is required. No-op when nothing is in progress.
        /// </summary>
        public void Interrupt(string reason)
        {
            if (!active) return;
            bool wasRecovery = recovering;
            StopSequence();
            results.Clear();
            NeedsRecovery = true;
            if (wasRecovery)
            {
                Debug.LogWarning("WORKFLOW: RECOVERY STOPPED - " + reason, this);
            }
            else
            {
                Debug.LogWarning("TEST DISCARDED: " + reason, this);
                TestDiscarded?.Invoke(reason);
            }
            SetStep(WorkflowStep.Interrupted, null);
        }

        /// <summary>True when ResetWorkflow would act: not already recovering, and motion permitted if a recovery move is needed.</summary>
        public bool CanReset(out string reason)
        {
            if (recovering)
            {
                reason = "RECOVERY IN PROGRESS";
                return false;
            }
            if ((active || NeedsRecovery) && Interlock != null && !Interlock.CanMove(out reason)) return false;
            reason = null;
            return true;
        }

        /// <summary>
        /// Operator reset: clears results and returns to Idle. If a test is in progress (or the arm needs recovery after an
        /// interruption) the tool retracts straight up to safe travel height and the arm returns home first
        /// (Step is Resetting meanwhile). Refused, without moving, while the interlock does not permit motion.
        /// </summary>
        public bool ResetWorkflow()
        {
            if (!CanReset(out string reason))
            {
                if (!recovering) Debug.LogWarning("RESET BLOCKED: " + reason, this);
                return false;
            }
            bool needsMotion = active || NeedsRecovery;
            if (!needsMotion)
            {
                FinishReset();
                return true;
            }

            bool discardingTest = active;
            // StopAllCoroutines also stops the motion/sensor coroutines nested in the sequence.
            StopSequence();
            active = true;
            recovering = true;
            StartCoroutine(RecoverAndReset(discardingTest));
            return true;
        }

        // ---------- Sequence ----------

        private IEnumerator Run()
        {
            var probe = cell.Probe;

            for (; currentIndex < points.Count; currentIndex++)
            {
                var point = points[currentIndex];
                Vector3 safe = point.Position + point.SurfaceNormal * safeHeight;
                Vector3 approach = point.Position + point.SurfaceNormal * probe.ApproachDistance;

                if (!IsMeasured(point.PointId))
                {
                    SetStep(WorkflowStep.Approach, point.PointId);
                    // First move leaves home in joint space; after that the tool travels in straight lines at safe height
                    // (after a resume this lifts the tool straight up above the current point).
                    yield return leftHome ? motion.MoveLinear(safe, transferSpeed) : motion.MovePointToPoint(safe);
                    leftHome = true;

                    SetStep(WorkflowStep.Descend, point.PointId);
                    yield return motion.MoveLinear(approach, descentSpeed);
                    yield return motion.MoveLinear(point.Position, contactSpeed);

                    SetStep(WorkflowStep.Contact, point.PointId);
                    HardnessReading? reading = null;
                    string failure = null;
                    yield return sensor.Measure(point, r => reading = r, f => failure = f);
                    if (failure != null || !reading.HasValue || !IsValidValue(reading.Value.Value))
                    {
                        yield return FailMeasurement(point.PointId, failure ?? "NO VALID READING");
                        yield break;
                    }
                    results.Add(reading.Value);
                    Debug.Log($"{point.PointId}: HARDNESS = {reading.Value.Value:0.#}", this);
                    ReadingRecorded?.Invoke(reading.Value);
                }

                SetStep(WorkflowStep.Retract, point.PointId);
                yield return motion.MoveLinear(safe, retractSpeed);
            }

            SetStep(WorkflowStep.ReturnHome, null);
            yield return motion.MoveHome();

            if (results.Count != points.Count)
            {
                // Defensive: never report a test as complete unless every point has exactly one reading.
                Debug.LogError($"WORKFLOW: FAULT - {results.Count}/{points.Count} readings at end of sequence; test discarded", this);
                results.Clear();
                EndSequence();
                SetStep(WorkflowStep.Faulted, null);
                yield break;
            }

            EndSequence();
            SetStep(WorkflowStep.Complete, null);
            Debug.Log(BuildSummary(), this);
            Completed?.Invoke(results);
        }

        /// <summary>Measurement failure: discard the test, retract and return home if motion is permitted, then FAULT.</summary>
        private IEnumerator FailMeasurement(string pointId, string reason)
        {
            results.Clear();
            Debug.LogError($"FAULT: MEASUREMENT FAILED at {pointId} - {reason}", this);
            Debug.LogWarning("TEST DISCARDED: MEASUREMENT FAILED", this);
            MeasurementFailed?.Invoke(pointId, reason);
            TestDiscarded?.Invoke("MEASUREMENT FAILED");

            if (Interlock == null || Interlock.CanMove(out _))
            {
                recovering = true;
                SetStep(WorkflowStep.Resetting, null);
                yield return RetractAndHome();
                NeedsRecovery = false;
            }
            else
            {
                NeedsRecovery = true;
            }
            EndSequence();
            SetStep(WorkflowStep.Faulted, pointId);
        }

        private IEnumerator RecoverAndReset(bool discardingTest)
        {
            if (discardingTest) Debug.LogWarning("WORKFLOW: RESET - test stopped, retracting and returning home", this);
            else Debug.Log("WORKFLOW: RECOVERY - retracting and returning home", this);
            results.Clear();
            SetStep(WorkflowStep.Resetting, null);

            yield return RetractAndHome();

            NeedsRecovery = false;
            EndSequence();
            FinishReset();
        }

        private IEnumerator RetractAndHome()
        {
            Vector3 tool = motion.ToolPosition;
            var retract = new Vector3(tool.x, SafeTravelHeight(), tool.z);
            if (tool.y < retract.y && motion.CanReach(retract)) yield return motion.MoveLinear(retract, retractSpeed);
            yield return motion.MoveHome();
        }

        private void FinishReset()
        {
            results.Clear();
            SetStep(WorkflowStep.Idle, null);
            Debug.Log("WORKFLOW: READY", this);
        }

        /// <summary>Stops every coroutine of this workflow (including nested motion / measurement) and clears run flags.</summary>
        private void StopSequence()
        {
            StopAllCoroutines();
            EndSequence();
        }

        private void EndSequence()
        {
            active = false;
            paused = false;
            recovering = false;
        }

        private bool IsMeasured(string pointId)
        {
            foreach (var reading in results.Readings)
                if (reading.PointId == pointId) return true;
            return false;
        }

        private static bool IsValidValue(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private float SafeTravelHeight()
        {
            float height = float.MinValue;
            foreach (var point in cell.Mold.MeasurementPoints.Points)
                height = Mathf.Max(height, (point.Position + point.SurfaceNormal * safeHeight).y);
            return height;
        }

        private bool TryGetValidatedPoints(out IReadOnlyList<MeasurementPoint> validated)
        {
            validated = null;
            if (cell == null || cell.Mold == null || cell.Probe == null || cell.Mold.MeasurementPoints == null)
                return Fault("TestingCell, mold, probe or measurement point set is not assigned");
            if (motion == null) return Fault($"no {nameof(IRobotMotion)} component on {name}");
            if (sensor == null) return Fault($"no {nameof(IHardnessSensor)} component on {name}");

            validated = cell.Mold.MeasurementPoints.Points;
            if (validated.Count == 0) return Fault("mold has no measurement points");

            foreach (var point in validated)
            {
                if (point == null) return Fault("measurement point list contains an empty entry");
                // Stage 2 motion holds the tool vertical, so points must face up.
                if (Vector3.Angle(point.SurfaceNormal, Vector3.up) > 1f)
                    return Fault($"{point.PointId} does not face up; vertical-tool motion cannot reach it");
                if (!motion.CanReach(point.Position) || !motion.CanReach(point.Position + point.SurfaceNormal * safeHeight))
                    return Fault($"{point.PointId} is out of the robot's reach");
            }
            return true;
        }

        private bool Fault(string reason)
        {
            Debug.LogError("WORKFLOW: FAULT - " + reason, this);
            return false;
        }

        private void SetStep(WorkflowStep step, string pointId)
        {
            Step = step;
            CurrentPointId = pointId;
            if (pointId != null && IsPointStep(step)) Debug.Log($"{pointId}: {StepLabel(step)}", this);
            else if (step == WorkflowStep.ReturnHome) Debug.Log("WORKFLOW: RETURN HOME", this);
            StepChanged?.Invoke(step, pointId);
        }

        private static bool IsPointStep(WorkflowStep step) =>
            step == WorkflowStep.Approach || step == WorkflowStep.Descend || step == WorkflowStep.Contact || step == WorkflowStep.Retract;

        private static string StepLabel(WorkflowStep step)
        {
            switch (step)
            {
                case WorkflowStep.Approach: return "APPROACH";
                case WorkflowStep.Descend: return "DESCEND";
                case WorkflowStep.Contact: return "CONTACT";
                case WorkflowStep.Retract: return "RETRACT";
                default: return step.ToString().ToUpperInvariant();
            }
        }

        private string BuildSummary()
        {
            var summary = new StringBuilder();
            summary.AppendLine($"WORKFLOW: COMPLETE - MEASUREMENT SUMMARY ({cell.Mold.MoldId}, {results.Count} readings)");
            foreach (var reading in results.Readings) summary.AppendLine($"  {reading.PointId}: {reading.Value:0.#}");
            return summary.ToString().TrimEnd();
        }
    }
}
