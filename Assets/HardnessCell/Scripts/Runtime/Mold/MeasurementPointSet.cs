using System.Collections.Generic;
using UnityEngine;

namespace HardnessCell.Mold
{
    /// <summary>Ordered set of measurement points; list order is the default measurement sequence.</summary>
    public sealed class MeasurementPointSet : MonoBehaviour
    {
        [SerializeField] private List<MeasurementPoint> points = new List<MeasurementPoint>();

        public IReadOnlyList<MeasurementPoint> Points => points;
        public int Count => points.Count;

        public bool TryGetPoint(string pointId, out MeasurementPoint point)
        {
            point = points.Find(p => p != null && p.PointId == pointId);
            return point != null;
        }

        [ContextMenu("Collect Child Points")]
        private void CollectChildPoints()
        {
            points = new List<MeasurementPoint>(GetComponentsInChildren<MeasurementPoint>());
            points.Sort((a, b) => string.CompareOrdinal(a.PointId, b.PointId));
        }
    }
}
