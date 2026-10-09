using UnityEngine;

namespace HardnessCell.Mold
{
    /// <summary>The mold under test. Placeholder geometry; owns its measurement points.</summary>
    public sealed class SandMold : MonoBehaviour
    {
        [SerializeField] private string moldId = "MOLD-0001";
        [SerializeField] private Renderer body;
        [SerializeField] private MeasurementPointSet measurementPoints;

        public string MoldId => moldId;
        public MeasurementPointSet MeasurementPoints => measurementPoints;
        public Bounds WorldBounds => body != null ? body.bounds : new Bounds(transform.position, Vector3.zero);
    }
}
