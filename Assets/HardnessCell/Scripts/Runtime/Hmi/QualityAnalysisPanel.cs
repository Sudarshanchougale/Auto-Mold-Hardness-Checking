using System.Globalization;
using HardnessCell.Analysis;
using UnityEngine;
using UnityEngine.UI;

namespace HardnessCell.Hmi
{
    /// <summary>
    /// HMI view of the quality analysis. Displays ResultAnalyzer.LatestReport (statistics, overall PASS/FAIL,
    /// failed points); performs no calculations. Shows placeholders until a completed test has been analysed.
    /// </summary>
    public sealed class QualityAnalysisPanel : MonoBehaviour
    {
        [SerializeField] private ResultAnalyzer analyzer;

        [Header("Statistics")]
        [SerializeField] private Text targetText;
        [SerializeField] private Text countText;
        [SerializeField] private Text averageText;
        [SerializeField] private Text minimumText;
        [SerializeField] private Text maximumText;
        [SerializeField] private Text rangeText;
        [SerializeField] private Text standardDeviationText;

        [Header("Overall result")]
        [SerializeField] private Image resultBadge;
        [SerializeField] private Text resultText;
        [SerializeField] private Text resultCaption;

        [Header("Failed points")]
        [SerializeField] private FailedPointRow[] failedRows = new FailedPointRow[0];
        [SerializeField] private Text failedPointsMessage;

        private void OnEnable()
        {
            if (analyzer == null)
            {
                Debug.LogError($"{name}: no ResultAnalyzer assigned.", this);
                enabled = false;
                return;
            }
            analyzer.AnalysisCompleted += OnAnalysisCompleted;
            analyzer.AnalysisCleared += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (analyzer == null) return;
            analyzer.AnalysisCompleted -= OnAnalysisCompleted;
            analyzer.AnalysisCleared -= Refresh;
        }

        private void OnAnalysisCompleted(AnalysisReport report) => Refresh();

        public void Refresh()
        {
            var limits = analyzer.Limits;
            targetText.text = $"{Format(limits.Minimum)} – {Format(limits.Maximum)}";

            var report = analyzer.LatestReport;
            if (report == null)
            {
                countText.text = averageText.text = minimumText.text = maximumText.text = rangeText.text = standardDeviationText.text = "--";
                SetResult("--", HmiPalette.ResultPending, "AVAILABLE AFTER COMPLETE TEST");
                ShowFailures(null, "--");
                return;
            }

            bool hasData = report.Count > 0;
            countText.text = $"{report.Count} / {report.RequiredCount}";
            averageText.text = hasData ? Format(report.Average, "0.0") : "--";
            minimumText.text = hasData ? Format(report.Minimum) : "--";
            maximumText.text = hasData ? Format(report.Maximum) : "--";
            rangeText.text = hasData ? Format(report.Range) : "--";
            standardDeviationText.text = hasData ? Format(report.StandardDeviation, "0.00") : "--";

            switch (report.Verdict)
            {
                case QualityVerdict.Pass:
                    SetResult("PASS", HmiPalette.ResultPass, "MOLD MEETS HARDNESS ACCEPTANCE");
                    ShowFailures(null, "NONE – ALL POINTS WITHIN TARGET");
                    break;
                case QualityVerdict.Fail:
                    SetResult("FAIL", HmiPalette.ResultFail, $"{report.Failures.Count} OF {report.RequiredCount} POINTS OUT OF RANGE");
                    ShowFailures(report, null);
                    break;
                default:
                    SetResult("INCOMPLETE", HmiPalette.ResultIncomplete, $"{report.Count} OF {report.RequiredCount} POINTS MEASURED");
                    ShowFailures(null, "--");
                    break;
            }
        }

        private void SetResult(string label, Color color, string caption)
        {
            resultText.text = label;
            resultBadge.color = color;
            resultCaption.text = caption;
        }

        private void ShowFailures(AnalysisReport report, string message)
        {
            int shown = report != null ? Mathf.Min(report.Failures.Count, failedRows.Length) : 0;
            for (int i = 0; i < failedRows.Length; i++)
            {
                if (failedRows[i] == null) continue;
                if (i < shown) failedRows[i].Show(report.Failures[i]);
                else failedRows[i].Hide();
            }
            failedPointsMessage.gameObject.SetActive(message != null);
            if (message != null) failedPointsMessage.text = message;
        }

        private static string Format(float value, string format = "0.#") => value.ToString(format, CultureInfo.InvariantCulture);
    }
}
