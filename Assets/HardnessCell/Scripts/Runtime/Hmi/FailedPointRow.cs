using System.Globalization;
using HardnessCell.Analysis;
using UnityEngine;
using UnityEngine.UI;

namespace HardnessCell.Hmi
{
    /// <summary>One line of the failed-point list: point ID, measured value, reason.</summary>
    public sealed class FailedPointRow : MonoBehaviour
    {
        [SerializeField] private Text pointText;
        [SerializeField] private Text valueText;
        [SerializeField] private Text reasonText;

        public void Show(PointFailure failure)
        {
            pointText.text = failure.PointId;
            valueText.text = failure.Value.ToString("0.#", CultureInfo.InvariantCulture);
            reasonText.text = PointFailure.Describe(failure.Reason);
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
