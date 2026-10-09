using UnityEngine;

namespace HardnessCell.Robot
{
    /// <summary>
    /// Single-axis revolute joint driven kinematically: local rotation = rest rotation * AngleAxis(angle, localAxis).
    /// Visual geometry lives on child objects so joint transforms stay unscaled.
    /// </summary>
    public sealed class RobotJoint : MonoBehaviour
    {
        [SerializeField] private string jointName = "J1";
        [SerializeField] private Vector3 localAxis = Vector3.up;
        [SerializeField] private float minAngle = -180f;
        [SerializeField] private float maxAngle = 180f;
        [Tooltip("Current joint angle in degrees. Editing this in the inspector poses the arm.")]
        [SerializeField] private float angle;
        [SerializeField, HideInInspector] private Quaternion restLocalRotation = Quaternion.identity;

        public string JointName => jointName;
        public Vector3 LocalAxis => localAxis;
        public float MinAngle => minAngle;
        public float MaxAngle => maxAngle;
        public float Angle => angle;
        public Vector3 WorldAxis => transform.parent != null
            ? transform.parent.rotation * (restLocalRotation * localAxis)
            : restLocalRotation * localAxis;

        public void SetAngle(float degrees)
        {
            angle = Mathf.Clamp(degrees, minAngle, maxAngle);
            ApplyAngle();
        }

        private void ApplyAngle()
        {
            transform.localRotation = restLocalRotation * Quaternion.AngleAxis(angle, localAxis.normalized);
        }

        private void OnValidate()
        {
            if (localAxis == Vector3.zero) localAxis = Vector3.up;
            if (maxAngle < minAngle) maxAngle = minAngle;
            angle = Mathf.Clamp(angle, minAngle, maxAngle);
            ApplyAngle();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(transform.position - WorldAxis * 0.2f, transform.position + WorldAxis * 0.2f);
        }
    }
}
