using System;
using HardnessCell.Workflow;
using UnityEngine;

namespace HardnessCell.Safety
{
    /// <summary>
    /// Central (simulated) safety/interlock state: emergency stop (latched until SafetyReset), safety gate, robot fault
    /// and sensor fault (latched until cleared). Owns state only; WorkflowSafetyInterlock applies it to the workflow.
    /// Logs transitions, never per frame. This is a simulation, not a safety-rated control function.
    /// </summary>
    public sealed class SafetyController : MonoBehaviour, IWorkflowInterlock
    {
        [Tooltip("Initial safety gate state when Play starts.")]
        [SerializeField] private bool gateOpenAtStart;

        private string robotFaultReason = "";
        private string sensorFaultReason = "";

        public bool EmergencyStopActive { get; private set; }
        public bool GateOpen { get; private set; }
        public bool RobotFaultActive { get; private set; }
        public bool SensorFaultActive { get; private set; }
        public string RobotFaultReason => robotFaultReason;
        public string SensorFaultReason => sensorFaultReason;

        public SafetyState State =>
            EmergencyStopActive ? SafetyState.EmergencyStopped
            : GateOpen ? SafetyState.Unsafe
            : RobotFaultActive || SensorFaultActive ? SafetyState.Faulted
            : SafetyState.Safe;

        /// <summary>Highest-priority active condition (EmergencyStop > GateOpen > RobotFault > SensorFault), or None.</summary>
        public SafetyCondition ActiveCondition =>
            EmergencyStopActive ? SafetyCondition.EmergencyStop
            : GateOpen ? SafetyCondition.GateOpen
            : RobotFaultActive ? SafetyCondition.RobotFault
            : SensorFaultActive ? SafetyCondition.SensorFault
            : SafetyCondition.None;

        public bool IsSafe => ActiveCondition == SafetyCondition.None;

        /// <summary>Any change of state (for displays).</summary>
        public event Action SafetyChanged;
        public event Action<SafetyCondition> ConditionRaised;
        public event Action<SafetyCondition> ConditionCleared;

        private void Awake()
        {
            GateOpen = gateOpenAtStart;
            if (GateOpen) Debug.LogWarning("SAFETY: GATE OPEN at start", this);
        }

        // ---------- Emergency stop ----------

        public void ActivateEmergencyStop()
        {
            if (EmergencyStopActive) return;
            EmergencyStopActive = true;
            Debug.LogError("SAFETY: EMERGENCY STOP ACTIVATED", this);
            Raise(SafetyCondition.EmergencyStop);
        }

        /// <summary>Releases the latched emergency stop. Other conditions (gate, faults) stay as they are.</summary>
        public bool ResetEmergencyStop()
        {
            if (!EmergencyStopActive) return false;
            EmergencyStopActive = false;
            Debug.Log("SAFETY: EMERGENCY STOP RESET", this);
            if (!IsSafe) Debug.LogWarning("SAFETY: still not safe - " + SafetyConditionText.Describe(ActiveCondition), this);
            Clear(SafetyCondition.EmergencyStop);
            return true;
        }

        // ---------- Safety gate ----------

        public void SetGateOpen(bool open)
        {
            if (GateOpen == open) return;
            GateOpen = open;
            if (open)
            {
                Debug.LogWarning("SAFETY: GATE OPENED", this);
                Raise(SafetyCondition.GateOpen);
            }
            else
            {
                Debug.Log("SAFETY: GATE CLOSED", this);
                Clear(SafetyCondition.GateOpen);
            }
        }

        public void ToggleGate() => SetGateOpen(!GateOpen);

        // ---------- Faults ----------

        public void RaiseRobotFault(string reason)
        {
            if (RobotFaultActive) return;
            RobotFaultActive = true;
            robotFaultReason = reason ?? "";
            Debug.LogError($"FAULT: ROBOT FAULT ACTIVATED{Suffix(robotFaultReason)}", this);
            Raise(SafetyCondition.RobotFault);
        }

        public bool ClearRobotFault()
        {
            if (!RobotFaultActive) return false;
            RobotFaultActive = false;
            robotFaultReason = "";
            Debug.Log("FAULT: ROBOT FAULT CLEARED", this);
            Clear(SafetyCondition.RobotFault);
            return true;
        }

        public void RaiseSensorFault(string reason)
        {
            if (SensorFaultActive) return;
            SensorFaultActive = true;
            sensorFaultReason = reason ?? "";
            Debug.LogError($"FAULT: SENSOR FAULT ACTIVATED{Suffix(sensorFaultReason)}", this);
            Raise(SafetyCondition.SensorFault);
        }

        public bool ClearSensorFault()
        {
            if (!SensorFaultActive) return false;
            SensorFaultActive = false;
            sensorFaultReason = "";
            Debug.Log("FAULT: SENSOR FAULT CLEARED", this);
            Clear(SafetyCondition.SensorFault);
            return true;
        }

        // ---------- Interlock (IWorkflowInterlock) ----------

        public bool CanMove(out string reason)
        {
            reason = EmergencyStopActive ? "EMERGENCY STOP ACTIVE"
                : GateOpen ? "SAFETY GATE OPEN"
                : RobotFaultActive ? "ROBOT FAULT"
                : null;
            return reason == null;
        }

        public bool CanRun(out string reason)
        {
            if (!CanMove(out reason)) return false;
            reason = SensorFaultActive ? "SENSOR FAULT" : null;
            return reason == null;
        }

        private void Raise(SafetyCondition condition)
        {
            ConditionRaised?.Invoke(condition);
            SafetyChanged?.Invoke();
        }

        private void Clear(SafetyCondition condition)
        {
            ConditionCleared?.Invoke(condition);
            SafetyChanged?.Invoke();
        }

        private static string Suffix(string reason) => string.IsNullOrEmpty(reason) ? "" : " (" + reason + ")";
    }
}
