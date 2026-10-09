using UnityEngine;

namespace HardnessCell.Mold
{
    /// <summary>
    /// A predefined hardness measurement location on the mold surface.
    /// The transform position is the contact point; transform.up is the outward surface normal,
    /// so the probe approaches along -up and its tip pose should match this pose at contact.
    /// </summary>
    public sealed class MeasurementPoint : MonoBehaviour
    {
        [SerializeField] private string pointId = "P01";

        public string PointId => pointId;
        public Vector3 Position => transform.position;
        public Vector3 SurfaceNormal => transform.up;
        public Pose ContactPose => new Pose(transform.position, transform.rotation);

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.1f, 0.9f, 1f);
            Gizmos.DrawWireSphere(transform.position, 0.015f);
            Gizmos.DrawLine(transform.position, transform.position + SurfaceNormal * 0.08f);
#if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + SurfaceNormal * 0.1f, pointId);
#endif
        }
    }
}
