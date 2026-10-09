using UnityEngine;

namespace HardnessCell.Probe
{
    /// <summary>
    /// Hardness-testing probe tool. The tip transform is the contact point; tip.up points back into the probe body,
    /// so aligning the tip pose with a MeasurementPoint's ContactPose puts the probe normal to the surface.
    /// </summary>
    public sealed class HardnessProbe : MonoBehaviour
    {
        [SerializeField] private string probeId = "HP-01";
        [SerializeField] private Transform tip;
        [Tooltip("Standoff distance (m) above the surface for the approach position before contact.")]
        [SerializeField] private float approachDistance = 0.05f;

        public string ProbeId => probeId;
        public Transform Tip => tip != null ? tip : transform;
        public Vector3 ProbingDirection => -Tip.up;
        public float ApproachDistance => approachDistance;

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(Tip.position, 0.01f);
            Gizmos.DrawLine(Tip.position, Tip.position + ProbingDirection * approachDistance);
        }
    }
}
