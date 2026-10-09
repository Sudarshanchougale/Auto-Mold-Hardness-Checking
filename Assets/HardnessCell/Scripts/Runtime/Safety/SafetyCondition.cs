namespace HardnessCell.Safety
{
    /// <summary>Overall cell safety state.</summary>
    public enum SafetyState
    {
        Safe,
        /// <summary>A guard is open (safety gate).</summary>
        Unsafe,
        EmergencyStopped,
        /// <summary>Robot or sensor fault latched.</summary>
        Faulted,
    }

    /// <summary>Individual safety/fault conditions, declared in display priority order (highest first after None).</summary>
    public enum SafetyCondition
    {
        None,
        EmergencyStop,
        GateOpen,
        RobotFault,
        SensorFault,
    }

    public static class SafetyConditionText
    {
        /// <summary>Operator wording, e.g. "EMERGENCY STOP", "SAFETY GATE OPEN".</summary>
        public static string Describe(SafetyCondition condition)
        {
            switch (condition)
            {
                case SafetyCondition.EmergencyStop: return "EMERGENCY STOP";
                case SafetyCondition.GateOpen: return "SAFETY GATE OPEN";
                case SafetyCondition.RobotFault: return "ROBOT FAULT";
                case SafetyCondition.SensorFault: return "SENSOR FAULT";
                default: return "SAFE";
            }
        }
    }
}
