namespace HardnessCell.Workflow
{
    /// <summary>
    /// Permissives the workflow consults before moving or measuring. Implemented by the safety system; when no
    /// interlock is assigned the workflow behaves as before (always permitted). Simulation only, not a safety-rated function.
    /// </summary>
    public interface IWorkflowInterlock
    {
        /// <summary>Robot motion, including recovery moves (retract / home), is permitted.</summary>
        bool CanMove(out string reason);

        /// <summary>A measurement sequence may start or resume (motion permitted and measuring equipment healthy).</summary>
        bool CanRun(out string reason);
    }
}
