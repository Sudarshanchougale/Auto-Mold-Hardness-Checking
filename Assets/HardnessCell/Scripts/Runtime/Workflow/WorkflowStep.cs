namespace HardnessCell.Workflow
{
    public enum WorkflowStep
    {
        Idle,
        Approach,
        Descend,
        Contact,
        Retract,
        ReturnHome,
        Complete,
        Faulted,
        /// <summary>Operator reset or recovery in progress: the tool is retracting and the arm is returning home.</summary>
        Resetting,
        /// <summary>Test paused by the operator; the sequence can be resumed from the current point.</summary>
        Paused,
        /// <summary>Test stopped immediately by a safety condition and discarded; a reset (recovery) is required.</summary>
        Interrupted,
    }
}
