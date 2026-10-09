using System.Collections;
using HardnessCell.Cell;
using UnityEngine;

namespace HardnessCell.Robot
{
    /// <summary>
    /// Simulated robot motion: drives RobotJoint angles directly with eased interpolation, no physics.
    /// Home is the arm pose at Awake (the pose authored in the scene).
    /// </summary>
    public sealed class KinematicRobotMotion : MonoBehaviour, IRobotMotion
    {
        [SerializeField] private RobotArmRig rig;
        [Tooltip("Speed of the fastest joint during point-to-point moves (deg/s).")]
        [SerializeField] private float jointSpeed = 45f;
        [Tooltip("Shortest duration of any move (s), so tiny moves are still visible.")]
        [SerializeField] private float minMoveDuration = 0.2f;
        [Tooltip("Logs a warning if the TCP ends a move further than this from its target (m).")]
        [SerializeField] private float positionTolerance = 0.002f;

        private ArmKinematics kinematics;
        private float[] homeAngles;
        private readonly float[] solveBuffer = new float[ArmKinematics.JointCount];

        public Vector3 ToolPosition => rig.EndEffector.ToolCenterPoint.position;

        private void Awake()
        {
            if (rig == null)
            {
                var cell = GetComponentInParent<TestingCell>();
                if (cell != null) rig = cell.RobotArm;
            }
            if (rig == null)
            {
                Debug.LogError($"{name}: no RobotArmRig assigned.", this);
                enabled = false;
                return;
            }
            homeAngles = rig.GetJointAngles();
            kinematics = new ArmKinematics(rig);
        }

        public bool CanReach(Vector3 toolPosition) => kinematics != null && kinematics.TrySolve(toolPosition, solveBuffer);

        public IEnumerator MoveHome() => MoveJoints(homeAngles);

        public IEnumerator MovePointToPoint(Vector3 toolPosition)
        {
            var target = new float[ArmKinematics.JointCount];
            if (!kinematics.TrySolve(toolPosition, target))
            {
                Debug.LogError($"{name}: target {toolPosition} is out of reach.", this);
                yield break;
            }
            yield return MoveJoints(target);
            CheckArrival(toolPosition);
        }

        public IEnumerator MoveLinear(Vector3 toolPosition, float speed)
        {
            Vector3 start = ToolPosition;
            float duration = Mathf.Max(minMoveDuration, Vector3.Distance(start, toolPosition) / Mathf.Max(speed, 1e-4f));
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                if (!SolveAndApply(Vector3.Lerp(start, toolPosition, Mathf.SmoothStep(0f, 1f, t / duration)))) yield break;
                yield return null;
            }
            if (SolveAndApply(toolPosition)) CheckArrival(toolPosition);
        }

        private IEnumerator MoveJoints(float[] target)
        {
            float[] start = rig.GetJointAngles();
            float largestDelta = 0f;
            for (int i = 0; i < start.Length; i++) largestDelta = Mathf.Max(largestDelta, Mathf.Abs(target[i] - start[i]));
            float duration = Mathf.Max(minMoveDuration, largestDelta / Mathf.Max(jointSpeed, 1e-4f));

            // Plain (not shortest-path) interpolation keeps every joint inside its limits.
            var current = new float[start.Length];
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float s = Mathf.SmoothStep(0f, 1f, t / duration);
                for (int i = 0; i < current.Length; i++) current[i] = Mathf.Lerp(start[i], target[i], s);
                rig.SetJointAngles(current);
                yield return null;
            }
            rig.SetJointAngles(target);
        }

        private bool SolveAndApply(Vector3 toolPosition)
        {
            if (!kinematics.TrySolve(toolPosition, solveBuffer))
            {
                Debug.LogError($"{name}: linear move passes out of reach at {toolPosition}.", this);
                return false;
            }
            rig.SetJointAngles(solveBuffer);
            return true;
        }

        private void CheckArrival(Vector3 target)
        {
            float error = Vector3.Distance(ToolPosition, target);
            if (error > positionTolerance)
                Debug.LogWarning($"{name}: TCP stopped {error * 1000f:0.0} mm from target {target}.", this);
        }
    }
}
