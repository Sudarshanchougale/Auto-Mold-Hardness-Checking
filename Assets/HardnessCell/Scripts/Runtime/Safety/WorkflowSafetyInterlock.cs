using HardnessCell.Workflow;
using UnityEngine;

namespace HardnessCell.Safety
{
    /// <summary>
    /// Applies SafetyController state to the MeasurementWorkflow (event driven):
    /// - installs the controller as the workflow's start/resume/motion interlock;
    /// - EMERGENCY STOP, SAFETY GATE OPEN and ROBOT FAULT interrupt the workflow immediately (no motion, test discarded);
    /// - a failed measurement latches SENSOR FAULT (the workflow itself discards the test and retracts if motion is allowed);
    /// - after an operator acknowledgement (safety reset, clear robot/sensor fault) and only when everything is safe,
    ///   runs the workflow's recovery (retract, home, READY). Closing the gate alone never moves the robot: the operator
    ///   presses RESET to recover.
    /// </summary>
    public sealed class WorkflowSafetyInterlock : MonoBehaviour
    {
        [SerializeField] private SafetyController safety;
        [SerializeField] private MeasurementWorkflow workflow;

        private void Awake()
        {
            if (safety != null && workflow != null) workflow.Interlock = safety;
        }

        private void OnEnable()
        {
            if (safety == null || workflow == null)
            {
                Debug.LogError($"{name}: safety controller or workflow not assigned; safety interlocks are NOT active.", this);
                enabled = false;
                return;
            }
            workflow.Interlock = safety;
            safety.ConditionRaised += OnConditionRaised;
            safety.ConditionCleared += OnConditionCleared;
            workflow.MeasurementFailed += OnMeasurementFailed;
        }

        private void OnDisable()
        {
            if (safety == null || workflow == null) return;
            safety.ConditionRaised -= OnConditionRaised;
            safety.ConditionCleared -= OnConditionCleared;
            workflow.MeasurementFailed -= OnMeasurementFailed;
        }

        private void OnConditionRaised(SafetyCondition condition)
        {
            switch (condition)
            {
                case SafetyCondition.EmergencyStop:
                case SafetyCondition.GateOpen:
                case SafetyCondition.RobotFault:
                    workflow.Interrupt(SafetyConditionText.Describe(condition));
                    break;
                // SensorFault: motion is still safe; the next measurement attempt fails and the workflow handles it.
                // Start and resume are blocked through the interlock.
            }
        }

        private void OnConditionCleared(SafetyCondition condition)
        {
            if (!workflow.NeedsRecovery && workflow.Step != WorkflowStep.Faulted) return;

            if (condition == SafetyCondition.GateOpen)
            {
                if (workflow.NeedsRecovery) Debug.Log("SAFETY: gate closed - press RESET to recover the robot", this);
                return;
            }
            if (!safety.CanRun(out string reason))
            {
                Debug.Log($"SAFETY: recovery waiting - {reason}", this);
                return;
            }
            workflow.ResetWorkflow();
        }

        private void OnMeasurementFailed(string pointId, string reason) =>
            safety.RaiseSensorFault($"{pointId}: {reason}");
    }
}
