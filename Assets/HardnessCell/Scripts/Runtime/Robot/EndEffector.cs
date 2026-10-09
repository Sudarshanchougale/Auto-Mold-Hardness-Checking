using HardnessCell.Probe;
using UnityEngine;

namespace HardnessCell.Robot
{
    /// <summary>Robot flange. Tools are parented to the tool mount; the tool center point (TCP) comes from the attached tool.</summary>
    public sealed class EndEffector : MonoBehaviour
    {
        [SerializeField] private Transform toolMount;
        [SerializeField] private HardnessProbe attachedProbe;

        public Transform ToolMount => toolMount != null ? toolMount : transform;
        public HardnessProbe AttachedProbe => attachedProbe;
        public Transform ToolCenterPoint => attachedProbe != null ? attachedProbe.Tip : ToolMount;

        public void Attach(HardnessProbe probe)
        {
            attachedProbe = probe;
            if (probe == null) return;
            probe.transform.SetParent(ToolMount, false);
            probe.transform.localPosition = Vector3.zero;
            probe.transform.localRotation = Quaternion.identity;
        }
    }
}
