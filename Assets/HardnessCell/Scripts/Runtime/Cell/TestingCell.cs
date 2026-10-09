using HardnessCell.Mold;
using HardnessCell.Probe;
using HardnessCell.Robot;
using UnityEngine;

namespace HardnessCell.Cell
{
    /// <summary>
    /// Composition root of the testing cell. Later systems (workflow state machine, HMI, readings, storage)
    /// get their scene references from here instead of searching the scene.
    /// </summary>
    public sealed class TestingCell : MonoBehaviour
    {
        [SerializeField] private SandMold mold;
        [SerializeField] private RobotArmRig robotArm;
        [SerializeField] private HardnessProbe probe;

        public SandMold Mold => mold;
        public RobotArmRig RobotArm => robotArm;
        public HardnessProbe Probe => probe;
    }
}
