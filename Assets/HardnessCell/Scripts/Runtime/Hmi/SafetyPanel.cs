using System;
using HardnessCell.Safety;
using HardnessCell.Workflow;
using UnityEngine;
using UnityEngine.UI;

namespace HardnessCell.Hmi
{
    /// <summary>
    /// HMI safety bar: shows the safety status and the reason START/RESUME is blocked, and exposes the safety controls
    /// (E-stop, safety reset, gate, pause/resume, simulated faults and their clear actions). Buttons only issue commands
    /// to SafetyController / FaultSimulator / MeasurementWorkflow; the interlock logic lives there.
    /// Also supplies the main HMI status while a safety condition is active (IHmiStatusOverride).
    /// </summary>
    public sealed class SafetyPanel : MonoBehaviour, IHmiStatusOverride
    {
        [SerializeField] private SafetyController safety;
        [SerializeField] private FaultSimulator faults;
        [SerializeField] private MeasurementWorkflow workflow;

        [Header("Status")]
        [SerializeField] private Image statusBadge;
        [SerializeField] private Text statusText;
        [SerializeField] private Text messageText;

        [Header("Controls")]
        [SerializeField] private Button emergencyStopButton;
        [SerializeField] private Button safetyResetButton;
        [SerializeField] private Button gateButton;
        [SerializeField] private Text gateButtonText;
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button simulateRobotFaultButton;
        [SerializeField] private Button clearRobotFaultButton;
        [SerializeField] private Button simulateSensorFaultButton;
        [SerializeField] private Button clearSensorFaultButton;

        public event Action StatusChanged;

        private void OnEnable()
        {
            if (safety == null || workflow == null)
            {
                Debug.LogError($"{name}: safety controller or workflow not assigned.", this);
                enabled = false;
                return;
            }
            safety.SafetyChanged += Refresh;
            workflow.StepChanged += OnStepChanged;
            workflow.StartBlocked += OnStartBlocked;

            emergencyStopButton.onClick.AddListener(safety.ActivateEmergencyStop);
            safetyResetButton.onClick.AddListener(OnSafetyReset);
            gateButton.onClick.AddListener(safety.ToggleGate);
            pauseButton.onClick.AddListener(OnPause);
            resumeButton.onClick.AddListener(OnResume);
            simulateRobotFaultButton.onClick.AddListener(OnSimulateRobotFault);
            clearRobotFaultButton.onClick.AddListener(OnClearRobotFault);
            simulateSensorFaultButton.onClick.AddListener(OnSimulateSensorFault);
            clearSensorFaultButton.onClick.AddListener(OnClearSensorFault);
            Refresh();
        }

        private void OnDisable()
        {
            if (safety == null || workflow == null) return;
            safety.SafetyChanged -= Refresh;
            workflow.StepChanged -= OnStepChanged;
            workflow.StartBlocked -= OnStartBlocked;
            emergencyStopButton.onClick.RemoveAllListeners();
            safetyResetButton.onClick.RemoveAllListeners();
            gateButton.onClick.RemoveAllListeners();
            pauseButton.onClick.RemoveAllListeners();
            resumeButton.onClick.RemoveAllListeners();
            simulateRobotFaultButton.onClick.RemoveAllListeners();
            clearRobotFaultButton.onClick.RemoveAllListeners();
            simulateSensorFaultButton.onClick.RemoveAllListeners();
            clearSensorFaultButton.onClick.RemoveAllListeners();
        }

        // SafetyController.Awake may set the initial gate state after this OnEnable.
        private void Start() => Refresh();

        public bool TryGetStatus(out string label, out Color color)
        {
            var condition = safety != null ? safety.ActiveCondition : SafetyCondition.None;
            label = SafetyConditionText.Describe(condition);
            color = ConditionColor(condition);
            return condition != SafetyCondition.None;
        }

        private void OnStepChanged(WorkflowStep step, string pointId) => Refresh();
        private void OnStartBlocked(string reason) => Refresh();
        private void OnSafetyReset() => safety.ResetEmergencyStop();
        private void OnPause() { workflow.Pause(); Refresh(); }
        private void OnResume() { workflow.Resume(); Refresh(); }
        private void OnClearRobotFault() => safety.ClearRobotFault();
        private void OnClearSensorFault() => safety.ClearSensorFault();

        private void OnSimulateRobotFault()
        {
            if (faults != null) faults.SimulateRobotFault();
            else safety.RaiseRobotFault("SIMULATED");
        }

        private void OnSimulateSensorFault()
        {
            if (faults != null) faults.SimulateSensorFault();
            else safety.RaiseSensorFault("SIMULATED");
        }

        public void Refresh()
        {
            var condition = safety.ActiveCondition;
            statusText.text = SafetyConditionText.Describe(condition);
            statusBadge.color = ConditionColor(condition);
            messageText.text = BuildMessage();
            messageText.color = condition == SafetyCondition.None && !workflow.NeedsRecovery ? HmiPalette.TextMuted : HmiPalette.FailureText;

            emergencyStopButton.interactable = !safety.EmergencyStopActive;
            safetyResetButton.interactable = safety.EmergencyStopActive;
            gateButtonText.text = safety.GateOpen ? "GATE\nOPEN" : "GATE\nCLOSED";
            gateButton.targetGraphic.color = safety.GateOpen ? HmiPalette.SafetyGateOpen : HmiPalette.GateClosed;
            pauseButton.interactable = workflow.CanPause;
            resumeButton.interactable = workflow.CanResume(out _);
            simulateRobotFaultButton.interactable = !safety.RobotFaultActive;
            clearRobotFaultButton.interactable = safety.RobotFaultActive;
            simulateSensorFaultButton.interactable = !safety.SensorFaultActive;
            clearSensorFaultButton.interactable = safety.SensorFaultActive;

            StatusChanged?.Invoke();
        }

        private string BuildMessage()
        {
            if (workflow.IsPaused)
                return workflow.CanResume(out string resumeReason) ? "TEST PAUSED - PRESS RESUME" : "RESUME BLOCKED: " + resumeReason;
            if (workflow.IsRunning)
                return workflow.Step == WorkflowStep.Resetting ? "RECOVERY: RETURNING HOME" : "TEST IN PROGRESS";
            if (workflow.CanStart(out string startReason)) return "READY - START PERMITTED";
            if (startReason == "RESET REQUIRED")
            {
                return safety.CanMove(out string moveReason)
                    ? "TEST DISCARDED - PRESS RESET TO RECOVER"
                    : "RECOVERY BLOCKED: " + moveReason;
            }
            return "START BLOCKED: " + startReason;
        }

        private static Color ConditionColor(SafetyCondition condition)
        {
            switch (condition)
            {
                case SafetyCondition.EmergencyStop: return HmiPalette.SafetyEmergency;
                case SafetyCondition.GateOpen: return HmiPalette.SafetyGateOpen;
                case SafetyCondition.RobotFault:
                case SafetyCondition.SensorFault: return HmiPalette.SafetyFault;
                default: return HmiPalette.SafetyOk;
            }
        }
    }
}
