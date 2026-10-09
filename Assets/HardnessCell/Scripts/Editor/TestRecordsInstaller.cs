using HardnessCell.Analysis;
using HardnessCell.Cell;
using HardnessCell.Hmi;
using HardnessCell.Records;
using HardnessCell.Workflow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static HardnessCell.Editor.HmiInstaller;

namespace HardnessCell.Editor
{
    /// <summary>
    /// Adds Stage 5 test records to an existing scene: a TestRecords object (TestHistory + TestRecorder) under the
    /// TestingCell and a LATEST TEST panel in the free space at the bottom of the HMI's left-hand column.
    /// </summary>
    public static class TestRecordsInstaller
    {
        private const float PanelWidth = 400f;
        private const float PanelHeight = 270f;
        private const float ContentWidth = PanelWidth - 2f * Pad;

        [MenuItem("Tools/Hardness Cell/Add Test Records To Open Scene")]
        private static void InstallInActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            TestingCell cell = null;
            HmiController hmi = null;
            TestHistoryPanel existing = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (cell == null) cell = root.GetComponentInChildren<TestingCell>(true);
                if (hmi == null) hmi = root.GetComponentInChildren<HmiController>(true);
                if (existing == null) existing = root.GetComponentInChildren<TestHistoryPanel>(true);
            }

            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                Debug.Log("Test records are already installed on " + existing.name, existing);
                return;
            }
            var workflow = cell != null ? cell.GetComponentInChildren<MeasurementWorkflow>(true) : null;
            var analyzer = cell != null ? cell.GetComponentInChildren<ResultAnalyzer>(true) : null;
            if (workflow == null || analyzer == null || hmi == null)
            {
                EditorUtility.DisplayDialog("Add Test Records",
                    "The active scene needs the measurement workflow, the operator HMI and the quality analysis. Open HardnessTestingCell.unity and add them first.", "OK");
                return;
            }

            var panel = Install(cell, workflow, analyzer, hmi);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = panel.gameObject;
            Debug.Log("Test records added and scene saved.", panel);
        }

        public static TestHistoryPanel Install(TestingCell cell, MeasurementWorkflow workflow, ResultAnalyzer analyzer, HmiController hmi)
        {
            var history = cell.GetComponentInChildren<TestHistory>(true);
            if (history == null)
            {
                var recordsGo = new GameObject("TestRecords");
                recordsGo.transform.SetParent(cell.transform, false);
                Undo.RegisterCreatedObjectUndo(recordsGo, "Add Test Records");
                history = recordsGo.AddComponent<TestHistory>();
                var recorder = recordsGo.AddComponent<TestRecorder>();
                TestingCellSceneBuilder.Configure(recorder, ("workflow", workflow), ("analyzer", analyzer), ("history", history));
            }

            var panelImage = Box("HistoryPanel", hmi.transform, HmiPalette.PanelBackground);
            Undo.RegisterCreatedObjectUndo(panelImage.gameObject, "Add Test History Panel");
            Anchor(panelImage.rectTransform, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(PanelWidth, PanelHeight));
            var p = panelImage.transform;

            SectionLabel(p, "LATEST TEST", 12f, Pad, ContentWidth);
            var testId = ValueRow(p, "TestId", "TEST ID", 38f, Pad, ContentWidth);
            var moldId = ValueRow(p, "MoldId", "MOLD ID", 68f, Pad, ContentWidth);
            var result = ValueRow(p, "Result", "RESULT", 98f, Pad, ContentWidth);
            var time = ValueRow(p, "Time", "TIME", 128f, Pad, ContentWidth);
            var stats = ValueRow(p, "Stats", "AVG / MIN / MAX", 158f, Pad, ContentWidth);

            var exportButton = StyledButton(p, "ExportCsvButton", "EXPORT CSV", HmiPalette.ResetButton, 18);
            Anchor((RectTransform)exportButton.transform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(Pad, 16f), new Vector2(160f, 48f));
            var message = Label("ExportMessage", p, "--", 14, FontStyle.Bold, TextAnchor.MiddleLeft, HmiPalette.TextMuted);
            message.horizontalOverflow = HorizontalWrapMode.Wrap;
            Anchor(message.rectTransform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(Pad + 172f, 16f), new Vector2(ContentWidth - 172f, 48f));

            var panel = panelImage.gameObject.AddComponent<TestHistoryPanel>();
            TestingCellSceneBuilder.Configure(panel,
                ("history", history), ("testIdText", testId), ("moldIdText", moldId), ("resultText", result),
                ("timestampText", time), ("statisticsText", stats), ("exportButton", exportButton), ("exportMessage", message));
            return panel;
        }
    }
}
