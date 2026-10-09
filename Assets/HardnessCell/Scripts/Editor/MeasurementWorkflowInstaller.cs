using HardnessCell.Cell;
using HardnessCell.Measurement;
using HardnessCell.Robot;
using HardnessCell.Workflow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HardnessCell.Editor
{
    /// <summary>
    /// Adds the Stage 2 measurement workflow to an existing testing cell without rebuilding the scene.
    /// Creates one child of the TestingCell holding the workflow, simulated motion and simulated sensor.
    /// </summary>
    public static class MeasurementWorkflowInstaller
    {
        private const string ObjectName = "MeasurementWorkflow";

        [MenuItem("Tools/Hardness Cell/Add Measurement Workflow To Open Scene")]
        private static void InstallInActiveScene()
        {
            TestingCell cell = null;
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                cell = root.GetComponentInChildren<TestingCell>(true);
                if (cell != null) break;
            }
            if (cell == null)
            {
                EditorUtility.DisplayDialog("Add Measurement Workflow", "The active scene has no TestingCell. Open HardnessTestingCell.unity first.", "OK");
                return;
            }

            var existing = cell.GetComponentInChildren<MeasurementWorkflow>(true);
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                Debug.Log("Measurement workflow is already installed on " + existing.name, existing);
                return;
            }

            var workflow = Install(cell);
            Undo.RegisterCreatedObjectUndo(workflow.gameObject, "Add Measurement Workflow");
            EditorSceneManager.MarkSceneDirty(cell.gameObject.scene);
            EditorSceneManager.SaveScene(cell.gameObject.scene);
            Selection.activeGameObject = workflow.gameObject;
            Debug.Log("Measurement workflow added and scene saved.", workflow);
        }

        public static MeasurementWorkflow Install(TestingCell cell)
        {
            var go = new GameObject(ObjectName);
            go.transform.SetParent(cell.transform, false);
            var motion = go.AddComponent<KinematicRobotMotion>();
            go.AddComponent<SimulatedHardnessSensor>();
            var workflow = go.AddComponent<MeasurementWorkflow>();

            TestingCellSceneBuilder.Configure(motion, ("rig", cell.RobotArm));
            TestingCellSceneBuilder.Configure(workflow, ("cell", cell));
            return workflow;
        }
    }
}
