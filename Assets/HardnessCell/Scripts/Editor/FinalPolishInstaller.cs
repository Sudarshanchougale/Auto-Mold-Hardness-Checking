using HardnessCell.Analysis;
using HardnessCell.Cell;
using HardnessCell.Hmi;
using HardnessCell.Measurement;
using HardnessCell.Mold;
using HardnessCell.Safety;
using HardnessCell.Workflow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HardnessCell.Editor
{
    /// <summary>
    /// Stage 7 final integration for an existing scene (idempotent; safe to run repeatedly):
    /// links the HMI main status to the analysis result, applies the final camera framing, labels P01-P09 in the
    /// Game view with workflow-driven marker states, renames the limits row, and verifies the demo defaults
    /// (nominal hardness 80, limits 80-95, gate closed, no simulated fault), correcting and logging any deviation.
    /// </summary>
    public static class FinalPolishInstaller
    {
        private const string PointDisplayName = "PointDisplay";
        private const string LimitsCaption = "LIMITS (MIN – MAX)";
        private const float LabelOffset = 0.07f;   // label stands this far behind its point, away from the camera
        private const float LabelScale = 0.001f;   // world-space canvas: 1 unit = 1 mm

        [MenuItem("Tools/Hardness Cell/Apply Final Polish To Open Scene")]
        private static void ApplyToActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            TestingCell cell = null;
            HmiController hmi = null;
            QualityAnalysisPanel analysisPanel = null;
            Camera camera = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (cell == null) cell = root.GetComponentInChildren<TestingCell>(true);
                if (hmi == null) hmi = root.GetComponentInChildren<HmiController>(true);
                if (analysisPanel == null) analysisPanel = root.GetComponentInChildren<QualityAnalysisPanel>(true);
                if (camera == null && root.CompareTag("MainCamera")) camera = root.GetComponent<Camera>();
            }
            var workflow = cell != null ? cell.GetComponentInChildren<MeasurementWorkflow>(true) : null;
            var analyzer = cell != null ? cell.GetComponentInChildren<ResultAnalyzer>(true) : null;
            if (workflow == null || hmi == null || analyzer == null)
            {
                EditorUtility.DisplayDialog("Apply Final Polish",
                    "The active scene needs the measurement workflow, operator HMI and quality analysis (Stages 2-4). Open HardnessTestingCell.unity first.", "OK");
                return;
            }

            Apply(cell, workflow, hmi, analyzer, camera, analysisPanel);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Final polish applied and scene saved.", hmi);
        }

        public static void Apply(TestingCell cell, MeasurementWorkflow workflow, HmiController hmi, ResultAnalyzer analyzer,
            Camera camera, QualityAnalysisPanel analysisPanel)
        {
            Undo.RecordObject(hmi, "Final polish");
            TestingCellSceneBuilder.Configure(hmi, ("resultAnalyzer", analyzer));

            if (camera != null)
            {
                Undo.RecordObject(camera.transform, "Final polish");
                Undo.RecordObject(camera, "Final polish");
                TestingCellSceneBuilder.SetupCamera(camera);
            }

            if (analysisPanel != null)
            {
                var row = analysisPanel.transform.Find("TargetLabel");
                var label = row != null ? row.GetComponent<Text>() : null;
                if (label != null)
                {
                    Undo.RecordObject(label, "Final polish");
                    label.text = LimitsCaption;
                }
            }

            BuildPointDisplay(cell, workflow, camera);
            VerifyDefaults(cell, workflow, analyzer);
        }

        private static void BuildPointDisplay(TestingCell cell, MeasurementWorkflow workflow, Camera camera)
        {
            // Rebuilt every time so labels face the current camera.
            foreach (var root in cell.gameObject.scene.GetRootGameObjects())
            {
                var existing = root.GetComponentInChildren<PointMarkerDisplay>(true);
                if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);
            }

            var points = cell.Mold.MeasurementPoints.Points;
            var display = new GameObject(PointDisplayName);
            SceneManager.MoveGameObjectToScene(display, cell.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(display, "Final polish");

            Vector3 cameraPosition = camera != null ? camera.transform.position : TestingCellSceneBuilder.CameraPosition;
            var pointArray = new MeasurementPoint[points.Count];
            var markers = new Renderer[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                pointArray[i] = points[i];
                markers[i] = points[i].GetComponentInChildren<Renderer>();
                BuildLabel(display.transform, points[i], cameraPosition);
            }

            var component = display.AddComponent<PointMarkerDisplay>();
            TestingCellSceneBuilder.Configure(component, ("workflow", workflow), ("points", pointArray), ("markers", markers));
        }

        private static void BuildLabel(Transform parent, MeasurementPoint point, Vector3 cameraPosition)
        {
            // Placed behind the point (seen from the camera) so it never hides the probe at contact.
            Vector3 away = point.Position - cameraPosition;
            away.y = 0f;
            Vector3 position = point.Position + away.normalized * LabelOffset + Vector3.up * 0.004f;

            var go = new GameObject("Label_" + point.PointId, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(130f, 56f);
            rect.position = position;
            rect.rotation = Quaternion.LookRotation(position - cameraPosition, Vector3.up);
            rect.localScale = Vector3.one * LabelScale;
            go.AddComponent<Canvas>().renderMode = RenderMode.WorldSpace;

            var text = HmiInstaller.Label("Text", go.transform, point.PointId, 44, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            HmiInstaller.Stretch(text.rectTransform);
            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(3f, -3f);
        }

        private static void VerifyDefaults(TestingCell cell, MeasurementWorkflow workflow, ResultAnalyzer analyzer)
        {
            var sensor = workflow.GetComponent<SimulatedHardnessSensor>();
            if (sensor != null)
            {
                EnsureFloat(sensor, "nominalHardness", 80f, "Simulated sensor nominal hardness (default FAIL dataset)");
                EnsureBool(sensor, "simulateFault", false, "Simulated sensor fault injection");
            }
            EnsureFloat(analyzer, "limits.minimum", 80f, "Acceptance minimum");
            EnsureFloat(analyzer, "limits.maximum", 95f, "Acceptance maximum");
            var safety = cell.GetComponentInChildren<SafetyController>(true);
            if (safety != null) EnsureBool(safety, "gateOpenAtStart", false, "Safety gate open at start");
        }

        private static void EnsureFloat(Object target, string field, float expected, string description)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            if (property == null) return;
            if (Mathf.Approximately(property.floatValue, expected))
            {
                Debug.Log($"FINAL CONFIG: {description} = {expected} (verified)", target);
                return;
            }
            Debug.LogWarning($"FINAL CONFIG: {description} was {property.floatValue}, restored to {expected}", target);
            property.floatValue = expected;
            serialized.ApplyModifiedProperties();
        }

        private static void EnsureBool(Object target, string field, bool expected, string description)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            if (property == null) return;
            if (property.boolValue == expected)
            {
                Debug.Log($"FINAL CONFIG: {description} = {expected} (verified)", target);
                return;
            }
            Debug.LogWarning($"FINAL CONFIG: {description} was {property.boolValue}, restored to {expected}", target);
            property.boolValue = expected;
            serialized.ApplyModifiedProperties();
        }
    }
}
