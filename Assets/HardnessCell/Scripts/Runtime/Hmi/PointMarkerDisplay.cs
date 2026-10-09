using HardnessCell.Measurement;
using HardnessCell.Mold;
using HardnessCell.Workflow;
using UnityEngine;

namespace HardnessCell.Hmi
{
    /// <summary>
    /// 3D counterpart of the HMI result tiles: colours each measurement point's marker disc as pending (material
    /// default), active (amber) or measured (light grey) from the workflow state. Neutral colours only; PASS/FAIL is
    /// shown solely as the overall result. Uses a MaterialPropertyBlock so the shared marker material is untouched.
    /// </summary>
    public sealed class PointMarkerDisplay : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly Color ActiveColor = new Color(0.95f, 0.66f, 0f);
        private static readonly Color ActiveEmission = new Color(0.65f, 0.42f, 0f);
        private static readonly Color MeasuredColor = new Color(0.91f, 0.93f, 0.95f);
        private static readonly Color MeasuredEmission = new Color(0.30f, 0.31f, 0.33f);

        [SerializeField] private MeasurementWorkflow workflow;
        [Tooltip("Points and their marker renderers, in the same order.")]
        [SerializeField] private MeasurementPoint[] points = new MeasurementPoint[0];
        [SerializeField] private Renderer[] markers = new Renderer[0];

        private MaterialPropertyBlock block;

        private void OnEnable()
        {
            if (workflow == null)
            {
                Debug.LogError($"{name}: no MeasurementWorkflow assigned.", this);
                enabled = false;
                return;
            }
            block ??= new MaterialPropertyBlock();
            workflow.StepChanged += OnStepChanged;
            workflow.ReadingRecorded += OnReadingRecorded;
            Refresh();
        }

        private void OnDisable()
        {
            if (workflow == null) return;
            workflow.StepChanged -= OnStepChanged;
            workflow.ReadingRecorded -= OnReadingRecorded;
            for (int i = 0; i < markers.Length; i++)
                if (markers[i] != null) markers[i].SetPropertyBlock(null);
        }

        private void OnStepChanged(WorkflowStep step, string pointId) => Refresh();
        private void OnReadingRecorded(HardnessReading reading) => Refresh();

        public void Refresh()
        {
            string activeId = workflow.IsRunning ? workflow.CurrentPointId : null;
            int count = Mathf.Min(points.Length, markers.Length);
            for (int i = 0; i < count; i++)
            {
                if (points[i] == null || markers[i] == null) continue;
                string id = points[i].PointId;
                if (id == activeId) Apply(markers[i], ActiveColor, ActiveEmission);
                else if (IsMeasured(id)) Apply(markers[i], MeasuredColor, MeasuredEmission);
                else markers[i].SetPropertyBlock(null);
            }
        }

        private bool IsMeasured(string pointId)
        {
            foreach (var reading in workflow.Results.Readings)
                if (reading.PointId == pointId) return true;
            return false;
        }

        private void Apply(Renderer marker, Color baseColor, Color emission)
        {
            block.Clear();
            block.SetColor(BaseColorId, baseColor);
            block.SetColor(EmissionColorId, emission);
            marker.SetPropertyBlock(block);
        }
    }
}
