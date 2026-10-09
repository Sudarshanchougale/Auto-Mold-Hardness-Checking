using HardnessCell.Cell;
using HardnessCell.Hmi;
using HardnessCell.Measurement;
using HardnessCell.Safety;
using HardnessCell.Workflow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static HardnessCell.Editor.HmiInstaller;

namespace HardnessCell.Editor
{
    /// <summary>
    /// Adds Stage 6 safety to an existing scene: a Safety object (SafetyController, WorkflowSafetyInterlock, FaultSimulator)
    /// under the TestingCell and a safety bar along the bottom of the HMI between the left and right columns.
    /// Also points HmiController's status override at the safety bar. Nothing else is changed.
    /// </summary>
    public static class SafetyInstaller
    {
        private const float LeftColumnWidth = 400f;
        private const float RightColumnWidth = 460f;
        private const float BarHeight = 150f;
        // Content is a fixed-width group centred in the bar; it fits down to a 16:10 screen.
        private const float ContentWidth = 866f;
        private const float ButtonWidth = 116f;
        private const float ButtonGap = 10f;

        [MenuItem("Tools/Hardness Cell/Add Safety System To Open Scene")]
        private static void InstallInActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            TestingCell cell = null;
            HmiController hmi = null;
            SafetyPanel existing = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (cell == null) cell = root.GetComponentInChildren<TestingCell>(true);
                if (hmi == null) hmi = root.GetComponentInChildren<HmiController>(true);
                if (existing == null) existing = root.GetComponentInChildren<SafetyPanel>(true);
            }

            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                Debug.Log("Safety system is already installed on " + existing.name, existing);
                return;
            }
            var workflow = cell != null ? cell.GetComponentInChildren<MeasurementWorkflow>(true) : null;
            if (workflow == null || hmi == null)
            {
                EditorUtility.DisplayDialog("Add Safety System",
                    "The active scene needs the measurement workflow and the operator HMI. Open HardnessTestingCell.unity and add them first.", "OK");
                return;
            }

            var panel = Install(cell, workflow, hmi);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = panel.gameObject;
            Debug.Log("Safety system added and scene saved.", panel);
        }

        public static SafetyPanel Install(TestingCell cell, MeasurementWorkflow workflow, HmiController hmi)
        {
            var safety = cell.GetComponentInChildren<SafetyController>(true);
            FaultSimulator faults;
            if (safety == null)
            {
                var safetyGo = new GameObject("Safety");
                safetyGo.transform.SetParent(cell.transform, false);
                Undo.RegisterCreatedObjectUndo(safetyGo, "Add Safety System");
                safety = safetyGo.AddComponent<SafetyController>();
                var interlock = safetyGo.AddComponent<WorkflowSafetyInterlock>();
                TestingCellSceneBuilder.Configure(interlock, ("safety", safety), ("workflow", workflow));
                faults = safetyGo.AddComponent<FaultSimulator>();
                var sensor = workflow.GetComponent<SimulatedHardnessSensor>();
                if (sensor != null) TestingCellSceneBuilder.Configure(faults, ("safety", safety), ("sensor", sensor));
                else TestingCellSceneBuilder.Configure(faults, ("safety", safety));
            }
            else
            {
                faults = safety.GetComponent<FaultSimulator>();
            }

            // Bar stretches between the left (analysis/history) and right (status/results) columns.
            var bar = Box("SafetyPanel", hmi.transform, HmiPalette.PanelBackground);
            Undo.RegisterCreatedObjectUndo(bar.gameObject, "Add Safety Panel");
            Anchor(bar.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, BarHeight));
            bar.rectTransform.offsetMin = new Vector2(LeftColumnWidth, 0f);
            bar.rectTransform.offsetMax = new Vector2(-RightColumnWidth, BarHeight);
            var topLine = Box("AccentLine", bar.transform, HmiPalette.EmergencyFrame);
            Anchor(topLine.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 3f));

            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(bar.transform, false);
            Anchor(content, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(ContentWidth, 0f));
            var c = content.transform;

            // Status column.
            SectionLabel(c, "SAFETY STATUS", 12f, 12f, 214f);
            var badge = Box("SafetyBadge", c, HmiPalette.SafetyOk);
            Place(badge.rectTransform, 12f, 36f, 214f, 50f);
            var statusText = Label("SafetyText", badge.transform, "SAFE", 20, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            Stretch(statusText.rectTransform);
            var message = Label("SafetyMessage", c, "--", 13, FontStyle.Bold, TextAnchor.UpperLeft, HmiPalette.TextMuted);
            message.horizontalOverflow = HorizontalWrapMode.Wrap;
            Place(message.rectTransform, 12f, 92f, 214f, 50f);

            // Emergency stop: red button in a yellow frame.
            var frame = Box("EmergencyStopFrame", c, HmiPalette.EmergencyFrame);
            Place(frame.rectTransform, 238f, 12f, 110f, 126f);
            var eStop = StyledButton(frame.transform, "EmergencyStopButton", "EMERGENCY\nSTOP", HmiPalette.SafetyEmergency, 16);
            Stretch((RectTransform)eStop.transform, 6f);

            // Two rows of buttons in four columns.
            const float x0 = 360f;
            var safetyReset = GridButton(c, "SafetyResetButton", "SAFETY\nRESET", HmiPalette.ClearFaultButton, x0, 0);
            var gate = GridButton(c, "GateButton", "GATE\nCLOSED", HmiPalette.GateClosed, x0, 1);
            var pause = GridButton(c, "PauseButton", "PAUSE", HmiPalette.ResetButton, x0 + (ButtonWidth + ButtonGap), 0);
            var resume = GridButton(c, "ResumeButton", "RESUME", HmiPalette.StartButton, x0 + (ButtonWidth + ButtonGap), 1);
            var robotFault = GridButton(c, "SimulateRobotFaultButton", "SIMULATE\nROBOT FAULT", HmiPalette.SimulateFaultButton, x0 + 2f * (ButtonWidth + ButtonGap), 0);
            var clearRobot = GridButton(c, "ClearRobotFaultButton", "CLEAR\nROBOT FAULT", HmiPalette.ClearFaultButton, x0 + 2f * (ButtonWidth + ButtonGap), 1);
            var sensorFault = GridButton(c, "SimulateSensorFaultButton", "SIMULATE\nSENSOR FAULT", HmiPalette.SimulateFaultButton, x0 + 3f * (ButtonWidth + ButtonGap), 0);
            var clearSensor = GridButton(c, "ClearSensorFaultButton", "CLEAR\nSENSOR FAULT", HmiPalette.ClearFaultButton, x0 + 3f * (ButtonWidth + ButtonGap), 1);

            var panel = bar.gameObject.AddComponent<SafetyPanel>();
            TestingCellSceneBuilder.Configure(panel,
                ("safety", safety), ("workflow", workflow),
                ("statusBadge", badge), ("statusText", statusText), ("messageText", message),
                ("emergencyStopButton", eStop), ("safetyResetButton", safetyReset),
                ("gateButton", gate), ("gateButtonText", gate.GetComponentInChildren<Text>()),
                ("pauseButton", pause), ("resumeButton", resume),
                ("simulateRobotFaultButton", robotFault), ("clearRobotFaultButton", clearRobot),
                ("simulateSensorFaultButton", sensorFault), ("clearSensorFaultButton", clearSensor));
            if (faults != null) TestingCellSceneBuilder.Configure(panel, ("faults", faults));

            Undo.RecordObject(hmi, "Link HMI status to safety");
            TestingCellSceneBuilder.Configure(hmi, ("statusOverrideSource", panel));
            return panel;
        }

        private static Button GridButton(Transform parent, string name, string caption, Color color, float x, int row)
        {
            var button = StyledButton(parent, name, caption, color, 14);
            Place((RectTransform)button.transform, x, row == 0 ? 12f : 78f, ButtonWidth, 60f);
            return button;
        }
    }
}
