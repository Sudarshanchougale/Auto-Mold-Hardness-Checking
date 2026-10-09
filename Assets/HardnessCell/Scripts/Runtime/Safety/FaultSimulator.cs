using HardnessCell.Measurement;
using UnityEngine;

namespace HardnessCell.Safety
{
    /// <summary>
    /// Simulation-only fault injection for testing the safety/fault handling. A simulated robot fault latches ROBOT FAULT;
    /// a simulated sensor fault latches SENSOR FAULT and makes the simulated sensor fail every measurement attempt until
    /// the fault is cleared.
    /// </summary>
    public sealed class FaultSimulator : MonoBehaviour
    {
        [SerializeField] private SafetyController safety;
        [SerializeField] private SimulatedHardnessSensor sensor;

        private void OnEnable()
        {
            if (safety != null) safety.ConditionCleared += OnConditionCleared;
        }

        private void OnDisable()
        {
            if (safety != null) safety.ConditionCleared -= OnConditionCleared;
        }

        public void SimulateRobotFault() => safety.RaiseRobotFault("SIMULATED");

        public void SimulateSensorFault()
        {
            if (sensor != null) sensor.SimulateFault = true;
            safety.RaiseSensorFault("SIMULATED");
        }

        private void OnConditionCleared(SafetyCondition condition)
        {
            // Clearing the sensor fault "repairs" the simulated sensor.
            if (condition == SafetyCondition.SensorFault && sensor != null) sensor.SimulateFault = false;
        }
    }
}
