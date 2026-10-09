using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace HardnessCell.Hmi
{
    /// <summary>Result tile for one measurement point: pending ("--"), active (highlighted) or measured (value shown).</summary>
    public sealed class PointResultTile : MonoBehaviour
    {
        [SerializeField] private string pointId = "P01";
        [SerializeField] private Image frame;
        [SerializeField] private Image fill;
        [SerializeField] private Text idText;
        [SerializeField] private Text valueText;

        public string PointId => pointId;

        public void Show(float? value, bool active)
        {
            idText.text = pointId;
            valueText.text = value.HasValue ? value.Value.ToString("0.#", CultureInfo.InvariantCulture) : "--";
            valueText.color = value.HasValue || active ? HmiPalette.TextPrimary : HmiPalette.TextDim;
            idText.color = active ? HmiPalette.Accent : HmiPalette.TextMuted;
            frame.color = active ? HmiPalette.Accent : value.HasValue ? HmiPalette.TileMeasuredFrame : HmiPalette.TileFrame;
            fill.color = active ? HmiPalette.TileActiveFill : HmiPalette.TileFill;
        }
    }
}
