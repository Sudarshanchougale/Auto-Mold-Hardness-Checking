using HardnessCell.Analysis;
using HardnessCell.Cell;
using HardnessCell.Hmi;
using HardnessCell.Workflow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static HardnessCell.Editor.HmiInstaller;

namespace HardnessCell.Editor
{
    /// <summary>
    /// Adds Stage 4 quality analysis to an existing scene: a ResultAnalyzer under the TestingCell and a
    /// left-hand QUALITY ANALYSIS panel on the existing operator HMI canvas. Nothing else is changed.
    /// </summary>
    public static class QualityAnalysisInstaller
    {
        private const float PanelWidth = 400f;
        private const float ContentWidth = PanelWidth - 2f * Pad;
        private const float RowHeight = 28f;

        [MenuItem("Tools/Hardness Cell/Add Quality Analysis To Open Scene")]
        private static void InstallInActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            TestingCell cell = null;
            HmiController hmi = null;
            QualityAnalysisPanel existing = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (cell == null) cell = root.GetComponentInChildren<TestingCell>(true);
                if (hmi == null) hmi = root.GetComponentInChildren<HmiController>(true);
                if (existing == null) existing = root.GetComponentInChildren<QualityAnalysisPanel>(true);
            }

            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                Debug.Log("Quality analysis is already installed on " + existing.name, existing);
                return;
            }
            var workflow = cell != null ? cell.GetComponentInChildren<MeasurementWorkflow>(true) : null;
            if (workflow == null || hmi == null)
            {
                EditorUtility.DisplayDialog("Add Quality Analysis",
                    "The active scene needs the measurement workflow and the operator HMI. Open HardnessTestingCell.unity and add them first.", "OK");
                return;
            }

            var panel = Install(cell, workflow, hmi);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = panel.gameObject;
            Debug.Log("Quality analysis added and scene saved.", panel);
        }

        public static QualityAnalysisPanel Install(TestingCell cell, MeasurementWorkflow workflow, HmiController hmi)
        {
            var analyzer = cell.GetComponentInChildren<ResultAnalyzer>(true);
            if (analyzer == null)
            {
                var analyzerGo = new GameObject("QualityAnalysis");
                analyzerGo.transform.SetParent(cell.transform, false);
                Undo.RegisterCreatedObjectUndo(analyzerGo, "Add Quality Analysis");
                analyzer = analyzerGo.AddComponent<ResultAnalyzer>();
                TestingCellSceneBuilder.Configure(analyzer, ("workflow", workflow), ("cell", cell));
            }

            var panelImage = Box("AnalysisPanel", hmi.transform, HmiPalette.PanelBackground);
            Undo.RegisterCreatedObjectUndo(panelImage.gameObject, "Add Quality Analysis Panel");
            var rect = panelImage.rectTransform;
            Anchor(rect, Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(PanelWidth, 0f));
            rect.offsetMax = new Vector2(rect.offsetMax.x, -HeaderHeight);
            var p = rect.transform;

            SectionLabel(p, "QUALITY ANALYSIS", 18f, Pad, ContentWidth);
            var target = ValueRow(p, "Target", "LIMITS (MIN – MAX)", 46f, Pad, ContentWidth);
            target.color = HmiPalette.Accent;
            var count = ValueRow(p, "Count", "MEASUREMENTS", 84f, Pad, ContentWidth);
            var average = ValueRow(p, "Average", "AVERAGE", 116f, Pad, ContentWidth);
            var minimum = ValueRow(p, "Minimum", "MINIMUM", 148f, Pad, ContentWidth);
            var maximum = ValueRow(p, "Maximum", "MAXIMUM", 180f, Pad, ContentWidth);
            var range = ValueRow(p, "Range", "RANGE", 212f, Pad, ContentWidth);
            var stdDev = ValueRow(p, "StdDev", "STD. DEV.", 244f, Pad, ContentWidth);

            SectionLabel(p, "OVERALL RESULT", 294f, Pad, ContentWidth);
            var badge = Box("ResultBadge", p, HmiPalette.ResultPending);
            Place(badge.rectTransform, Pad, 322f, ContentWidth, 84f);
            var resultText = Label("ResultText", badge.transform, "--", 46, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            Stretch(resultText.rectTransform);
            var caption = Label("ResultCaption", p, "AVAILABLE AFTER COMPLETE TEST", 15, FontStyle.Bold, TextAnchor.MiddleCenter, HmiPalette.TextMuted);
            Place(caption.rectTransform, Pad, 412f, ContentWidth, 22f);

            SectionLabel(p, "FAILED POINTS", 452f, Pad, ContentWidth);
            const float listTop = 480f;
            var message = Label("FailedPointsMessage", p, "--", 16, FontStyle.Bold, TextAnchor.MiddleLeft, HmiPalette.TextMuted);
            Place(message.rectTransform, Pad, listTop, ContentWidth, RowHeight);

            int rowCount = cell.Mold.MeasurementPoints.Count;
            var rows = new FailedPointRow[rowCount];
            for (int i = 0; i < rowCount; i++) rows[i] = BuildFailedRow(p, i, listTop + i * RowHeight);

            var panel = panelImage.gameObject.AddComponent<QualityAnalysisPanel>();
            TestingCellSceneBuilder.Configure(panel,
                ("analyzer", analyzer), ("targetText", target), ("countText", count), ("averageText", average),
                ("minimumText", minimum), ("maximumText", maximum), ("rangeText", range), ("standardDeviationText", stdDev),
                ("resultBadge", badge), ("resultText", resultText), ("resultCaption", caption),
                ("failedRows", rows), ("failedPointsMessage", message));
            return panel;
        }

        private static FailedPointRow BuildFailedRow(Transform parent, int index, float top)
        {
            var row = new GameObject("FailedRow_" + (index + 1), typeof(RectTransform));
            row.transform.SetParent(parent, false);
            Place((RectTransform)row.transform, Pad, top, ContentWidth, RowHeight);

            var point = Label("Point", row.transform, "--", 18, FontStyle.Bold, TextAnchor.MiddleLeft, HmiPalette.TextPrimary);
            Place(point.rectTransform, 0f, 0f, 64f, RowHeight);
            var value = Label("Value", row.transform, "--", 18, FontStyle.Bold, TextAnchor.MiddleRight, HmiPalette.TextPrimary);
            Place(value.rectTransform, 64f, 0f, 48f, RowHeight);
            var reason = Label("Reason", row.transform, "--", 16, FontStyle.Bold, TextAnchor.MiddleRight, HmiPalette.FailureText);
            Place(reason.rectTransform, 124f, 0f, ContentWidth - 124f, RowHeight);

            var component = row.AddComponent<FailedPointRow>();
            TestingCellSceneBuilder.Configure(component, ("pointText", point), ("valueText", value), ("reasonText", reason));
            row.SetActive(false);
            return component;
        }
    }
}
