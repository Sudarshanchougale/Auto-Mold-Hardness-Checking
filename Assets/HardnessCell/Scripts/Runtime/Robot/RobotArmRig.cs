using System.Collections.Generic;
using UnityEngine;

namespace HardnessCell.Robot
{
    /// <summary>Entry point to the arm: ordered joints (base to wrist) and the end effector.</summary>
    public sealed class RobotArmRig : MonoBehaviour
    {
        [SerializeField] private RobotJoint[] joints = new RobotJoint[0];
        [SerializeField] private EndEffector endEffector;

        public IReadOnlyList<RobotJoint> Joints => joints;
        public EndEffector EndEffector => endEffector;

        public float[] GetJointAngles()
        {
            var angles = new float[joints.Length];
            for (int i = 0; i < joints.Length; i++) angles[i] = joints[i].Angle;
            return angles;
        }

        public void SetJointAngles(IReadOnlyList<float> angles)
        {
            int count = Mathf.Min(angles.Count, joints.Length);
            for (int i = 0; i < count; i++) joints[i].SetAngle(angles[i]);
        }
    }
}
