using UnityEngine;

namespace HardnessCell.Robot
{
    /// <summary>
    /// Closed-form position solver for the six-joint placeholder arm with the tool held vertical.
    /// J1 yaws the arm toward the target, J2/J3 solve a two-link triangle to the wrist (elbow up),
    /// J5 cancels J2 + J3 so the tool stays vertical, and J4/J6 stay at 0.
    /// Link geometry is measured from the joint transforms, which must have identity rest rotations (as built in Stage 1).
    /// </summary>
    public sealed class ArmKinematics
    {
        public const int JointCount = 6;

        private readonly Transform armRoot;
        private readonly RobotJoint[] joints = new RobotJoint[JointCount];
        private readonly Vector3 j1Position;  // in arm root space
        private readonly Vector3 j2Offset;    // J2 relative to J1
        private readonly Vector3 toolOffset;  // J5 to TCP, in J5 space
        private readonly float upperArmLength, forearmLength;
        private readonly float upperArmRestAngle, forearmRestAngle;  // radians, in the arm's (forward, up) plane

        public ArmKinematics(RobotArmRig rig)
        {
            if (rig.Joints.Count != JointCount)
                throw new System.ArgumentException($"ArmKinematics expects {JointCount} joints, rig has {rig.Joints.Count}.");
            for (int i = 0; i < JointCount; i++) joints[i] = rig.Joints[i];

            armRoot = rig.transform;
            j1Position = joints[0].transform.localPosition;
            j2Offset = joints[1].transform.localPosition;
            Vector3 upperArm = joints[2].transform.localPosition;
            Vector3 forearm = joints[3].transform.localPosition + joints[4].transform.localPosition;
            toolOffset = joints[4].transform.InverseTransformPoint(rig.EndEffector.ToolCenterPoint.position);

            upperArmLength = new Vector2(upperArm.z, upperArm.y).magnitude;
            forearmLength = new Vector2(forearm.z, forearm.y).magnitude;
            upperArmRestAngle = Mathf.Atan2(upperArm.y, upperArm.z);
            forearmRestAngle = Mathf.Atan2(forearm.y, forearm.z);
        }

        /// <summary>Joint angles (degrees, J1..J6) that put the TCP at the target with the tool vertical.</summary>
        public bool TrySolve(Vector3 toolWorldPosition, float[] angles)
        {
            Vector3 target = armRoot.InverseTransformPoint(toolWorldPosition);
            float yaw = Mathf.Atan2(target.x, target.z) * Mathf.Rad2Deg;
            Quaternion yawRotation = Quaternion.AngleAxis(yaw, Vector3.up);

            Vector3 shoulder = j1Position + yawRotation * j2Offset;
            Vector3 wrist = target - yawRotation * toolOffset;
            Vector3 toWrist = wrist - shoulder;
            float reach = Vector3.Dot(toWrist, yawRotation * Vector3.forward);
            float height = toWrist.y;
            float distance = Mathf.Sqrt(reach * reach + height * height);
            if (distance > upperArmLength + forearmLength || distance < Mathf.Abs(upperArmLength - forearmLength))
                return false;

            // Joint rotation about +X turns the arm clockwise in the (forward, up) plane, hence rest - actual.
            float cosShoulder = (upperArmLength * upperArmLength + distance * distance - forearmLength * forearmLength)
                                / (2f * upperArmLength * distance);
            float upperArmAngle = Mathf.Atan2(height, reach) + Mathf.Acos(Mathf.Clamp(cosShoulder, -1f, 1f));
            float elbowReach = upperArmLength * Mathf.Cos(upperArmAngle);
            float elbowHeight = upperArmLength * Mathf.Sin(upperArmAngle);
            float forearmAngle = Mathf.Atan2(height - elbowHeight, reach - elbowReach);

            float shoulderPitch = (upperArmRestAngle - upperArmAngle) * Mathf.Rad2Deg;
            float elbowPitch = (forearmRestAngle - forearmAngle) * Mathf.Rad2Deg - shoulderPitch;

            angles[0] = yaw;
            angles[1] = Mathf.DeltaAngle(0f, shoulderPitch);
            angles[2] = Mathf.DeltaAngle(0f, elbowPitch);
            angles[3] = 0f;
            angles[4] = Mathf.DeltaAngle(0f, -(shoulderPitch + elbowPitch));
            angles[5] = 0f;

            for (int i = 0; i < JointCount; i++)
            {
                if (angles[i] < joints[i].MinAngle || angles[i] > joints[i].MaxAngle) return false;
            }
            return true;
        }
    }
}
