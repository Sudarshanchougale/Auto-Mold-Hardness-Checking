using System;
using System.Globalization;
using System.IO;
using HardnessCell.Records;
using UnityEngine;
using UnityEngine.UI;

namespace HardnessCell.Hmi
{
    /// <summary>
    /// HMI view of the saved test history: shows the latest completed test and offers EXPORT CSV.
    /// Displays TestHistory data only; record creation and storage happen in TestRecorder / TestHistory.
    /// </summary>
    public sealed class TestHistoryPanel : MonoBehaviour
    {
        [SerializeField] private TestHistory history;

        [Header("Latest test")]
        [SerializeField] private Text testIdText;
        [SerializeField] private Text moldIdText;
        [SerializeField] private Text resultText;
        [SerializeField] private Text timestampText;
        [SerializeField] private Text statisticsText;

        [Header("Export")]
        [SerializeField] private Button exportButton;
        [SerializeField] private Text exportMessage;

        private void OnEnable()
        {
            if (history == null)
            {
                Debug.LogError($"{name}: no TestHistory assigned.", this);
                enabled = false;
                return;
            }
            history.HistoryChanged += Refresh;
            exportButton.onClick.AddListener(OnExportPressed);
            Refresh();
        }

        private void OnDisable()
        {
            if (history == null) return;
            history.HistoryChanged -= Refresh;
            exportButton.onClick.RemoveListener(OnExportPressed);
        }

        public void Refresh()
        {
            var latest = history.Latest;
            if (latest == null)
            {
                testIdText.text = moldIdText.text = resultText.text = timestampText.text = statisticsText.text = "--";
                resultText.color = HmiPalette.TextPrimary;
                ShowMessage(history.IsLoaded ? $"{history.Records.Count} TESTS STORED" : "HISTORY UNAVAILABLE", !history.IsLoaded);
                return;
            }

            testIdText.text = latest.TestId;
            moldIdText.text = latest.MoldId;
            resultText.text = latest.Result;
            resultText.color = latest.Passed ? HmiPalette.ResultPass : HmiPalette.FailureText;
            timestampText.text = TestRecord.ToDisplayTimestamp(latest.Timestamp);
            statisticsText.text = $"{Format(latest.Average, "0.0")} / {Format(latest.Minimum)} / {Format(latest.Maximum)}";
            ShowMessage($"{history.Records.Count} TESTS STORED", false);
        }

        private void OnExportPressed()
        {
            if (history.Records.Count == 0)
            {
                ShowMessage("NO COMPLETED TESTS TO EXPORT", false);
                return;
            }
            try
            {
                string path = CsvExporter.ExportToFile(history.Records, RecordPaths.ExportDirectory);
                Debug.Log($"CSV: exported {history.Records.Count} test(s) to {path}", this);
                ShowMessage($"EXPORTED {history.Records.Count} TESTS\n{Path.GetFileName(path)}", false);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
                Debug.LogError($"CSV: export to {RecordPaths.ExportDirectory} failed.", this);
                ShowMessage("EXPORT FAILED - SEE CONSOLE", true);
            }
        }

        private void ShowMessage(string message, bool isError)
        {
            exportMessage.text = message;
            exportMessage.color = isError ? HmiPalette.FailureText : HmiPalette.TextMuted;
        }

        private static string Format(float value, string format = "0.#") => value.ToString(format, CultureInfo.InvariantCulture);
    }
}
