using System.Collections;
using UnityEngine;

namespace HardnessCell.Robot
{
    /// <summary>
    /// Motion commands the workflow issues to the robot. Moves are coroutines that finish when the robot is in position.
    /// Positions are tool center point (probe tip) targets in world space; the tool is held vertical, pointing down.
    /// The simulation implements this kinematically; a real robot driver can implement it instead.
    /// </summary>
    public interface IRobotMotion
    {
        Vector3 ToolPosition { get; }
        bool CanReach(Vector3 toolPosition);
        /// <summary>Joint-space (point-to-point) move; the tool path between start and target is not a straight line.</summary>
        IEnumerator MovePointToPoint(Vector3 toolPosition);
        /// <summary>Straight-line tool move at the given speed (m/s).</summary>
        IEnumerator MoveLinear(Vector3 toolPosition, float speed);
        IEnumerator MoveHome();
    }
}
