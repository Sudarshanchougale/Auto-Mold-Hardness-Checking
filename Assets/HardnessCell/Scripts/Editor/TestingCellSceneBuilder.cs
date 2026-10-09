using HardnessCell.Cell;
using HardnessCell.Mold;
using HardnessCell.Probe;
using HardnessCell.Robot;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace HardnessCell.Editor
{
    /// <summary>
    /// Generates the hardness testing cell scene from placeholder primitives.
    /// Rebuilding overwrites the scene; materials are only created if missing, so tweaks to them survive.
    /// Batch mode: Unity.exe -batchmode -quit -projectPath "..." -executeMethod HardnessCell.Editor.TestingCellSceneBuilder.Build
    /// </summary>
    public static class TestingCellSceneBuilder
    {
        private const string RootFolder = "Assets/HardnessCell";
        private const string ScenePath = RootFolder + "/Scenes/HardnessTestingCell.unity";
        private const string MaterialFolder = RootFolder + "/Materials";

        // Layout in metres. The robot sits on -Z and faces +Z toward the mold, which is centred on the origin.
        private static readonly Vector3 RobotBasePosition = new Vector3(0f, 0f, -1.3f);
        private const float FixtureTopHeight = 0.75f;
        private static readonly Vector3 MoldSize = new Vector3(1.0f, 0.35f, 0.7f);
        // P01..P09 are numbered row by row, starting with the row nearest the robot, left to right as seen from the robot.
        private static readonly float[] PointRowsZ = { -0.2f, 0f, 0.2f };
        private static readonly float[] PointColumnsX = { -0.35f, 0f, 0.35f };
        // J1..J6 in degrees: probe vertical, hovering just above the near row of points.
        private static readonly float[] HomePose = { 0f, 0f, 10f, 0f, -10f, 0f };

        private static Material robotPaint, robotJoint, steel, probeBody, probeTip, sand, fixture, concrete, safetyYellow, fence, pointMarker;

        [MenuItem("Tools/Hardness Cell/Build Testing Cell Scene")]
        private static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null &&
                !EditorUtility.DisplayDialog("Rebuild testing cell", ScenePath + " already exists and will be overwritten.", "Rebuild", "Cancel"))
                return;
            Build();
        }

        public static void Build()
        {
            EnsureFolder(RootFolder, "Scenes");
            EnsureFolder(RootFolder, "Materials");
            CreateMaterials();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            Camera camera = null;
            Light sun = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (camera == null) camera = root.GetComponent<Camera>();
                if (sun == null) sun = root.GetComponent<Light>();
            }

            var environment = new GameObject("Environment").transform;
            SetupLighting(sun, environment);
            BuildFloor(environment);
            BuildSafetyFence(environment);

            var cellRoot = new GameObject("HardnessTestingCell");
            var cell = cellRoot.AddComponent<TestingCell>();
            var mold = BuildTestStation(cellRoot.transform);
            var rig = BuildRobotArm(cellRoot.transform, out var probe);
            Configure(cell, ("mold", mold), ("robotArm", rig), ("probe", probe));
            var workflow = MeasurementWorkflowInstaller.Install(cell);
            var hmi = HmiInstaller.Install(cell, workflow);
            var analysisPanel = QualityAnalysisInstaller.Install(cell, workflow, hmi);
            var analyzer = cell.GetComponentInChildren<Analysis.ResultAnalyzer>(true);
            TestRecordsInstaller.Install(cell, workflow, analyzer, hmi);
            SafetyInstaller.Install(cell, workflow, hmi);

            SetupCamera(camera);
            FinalPolishInstaller.Apply(cell, workflow, hmi, analyzer, camera, analysisPanel);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("Hardness testing cell scene built at " + ScenePath);
        }

        // ---------- Environment ----------

        private static void SetupLighting(Light sun, Transform parent)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.60f, 0.62f, 0.66f);
            RenderSettings.ambientEquatorColor = new Color(0.42f, 0.42f, 0.42f);
            RenderSettings.ambientGroundColor = new Color(0.22f, 0.21f, 0.20f);

            if (sun == null) sun = new GameObject("Directional Light").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.name = "Key Light";
            sun.transform.SetParent(parent, false);
            sun.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            sun.color = new Color(1f, 0.97f, 0.92f);
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.Soft;
        }

        private static void BuildFloor(Transform parent)
        {
            var floor = Group("Floor", parent, Vector3.zero).transform;
            Part(PrimitiveType.Cube, "Slab", floor, new Vector3(0f, -0.05f, 0f), Vector3.zero, new Vector3(12f, 0.1f, 12f), concrete, keepCollider: true);

            // Yellow boundary around the robot and fixture working area.
            var markings = Group("WorkAreaMarkings", floor, new Vector3(0f, 0.003f, -0.6f)).transform;
            const float width = 3.6f, depth = 3.4f, line = 0.08f, thickness = 0.006f;
            Part(PrimitiveType.Cube, "Line_Near", markings, new Vector3(0f, 0f, -depth / 2f), Vector3.zero, new Vector3(width + line, thickness, line), safetyYellow);
            Part(PrimitiveType.Cube, "Line_Far", markings, new Vector3(0f, 0f, depth / 2f), Vector3.zero, new Vector3(width + line, thickness, line), safetyYellow);
            Part(PrimitiveType.Cube, "Line_Left", markings, new Vector3(-width / 2f, 0f, 0f), Vector3.zero, new Vector3(line, thickness, depth), safetyYellow);
            Part(PrimitiveType.Cube, "Line_Right", markings, new Vector3(width / 2f, 0f, 0f), Vector3.zero, new Vector3(line, thickness, depth), safetyYellow);
        }

        private static void BuildSafetyFence(Transform parent)
        {
            // Back and left sides only, so the demo camera (front right) has a clear view.
            var fenceRoot = Group("SafetyFence", parent, Vector3.zero).transform;
            BuildFenceRun(fenceRoot, "Back", new Vector3(-3f, 0f, 2.2f), new Vector3(3f, 0f, 2.2f), 4);
            BuildFenceRun(fenceRoot, "Left", new Vector3(-3f, 0f, -3f), new Vector3(-3f, 0f, 2.2f), 4);
        }

        private static void BuildFenceRun(Transform parent, string name, Vector3 from, Vector3 to, int segments)
        {
            var run = Group(name, parent, Vector3.zero).transform;
            for (int i = 0; i <= segments; i++)
            {
                var post = Vector3.Lerp(from, to, i / (float)segments) + Vector3.up;
                Part(PrimitiveType.Cube, "Post_" + i, run, post, Vector3.zero, new Vector3(0.06f, 2f, 0.06f), safetyYellow);
            }

            var direction = to - from;
            var railRotation = Quaternion.LookRotation(direction.normalized).eulerAngles;
            foreach (var height in new[] { 0.15f, 1.05f, 1.95f })
            {
                var centre = (from + to) / 2f + Vector3.up * height;
                Part(PrimitiveType.Cube, "Rail_" + height.ToString("0.00"), run, centre, railRotation, new Vector3(0.03f, 0.04f, direction.magnitude), fence);
            }
        }

        // ---------- Test station: fixture, mold, measurement points ----------

        private static SandMold BuildTestStation(Transform parent)
        {
            var station = Group("TestStation", parent, Vector3.zero).transform;

            var table = Group("MoldFixture", station, Vector3.zero).transform;
            Part(PrimitiveType.Cube, "Top", table, new Vector3(0f, FixtureTopHeight - 0.025f, 0f), Vector3.zero, new Vector3(1.4f, 0.05f, 1.0f), fixture, keepCollider: true);
            const float legHeight = FixtureTopHeight - 0.05f;
            for (int i = 0; i < 4; i++)
            {
                var x = i % 2 == 0 ? -0.62f : 0.62f;
                var z = i < 2 ? -0.42f : 0.42f;
                Part(PrimitiveType.Cube, "Leg_" + i, table, new Vector3(x, legHeight / 2f, z), Vector3.zero, new Vector3(0.06f, legHeight, 0.06f), fixture);
            }

            // Mold origin is the centre of its bottom face, resting on the fixture.
            var moldRoot = Group("SandMold", station, new Vector3(0f, FixtureTopHeight, 0f));
            var body = Part(PrimitiveType.Cube, "Body", moldRoot.transform, new Vector3(0f, MoldSize.y / 2f, 0f), Vector3.zero, MoldSize, sand, keepCollider: true);
            var mold = moldRoot.AddComponent<SandMold>();

            var pointsRoot = Group("MeasurementPoints", moldRoot.transform, new Vector3(0f, MoldSize.y, 0f));
            var pointSet = pointsRoot.AddComponent<MeasurementPointSet>();
            var points = new MeasurementPoint[PointRowsZ.Length * PointColumnsX.Length];
            int index = 0;
            foreach (var z in PointRowsZ)
            {
                foreach (var x in PointColumnsX)
                {
                    var id = "P" + (index + 1).ToString("00");
                    var pointGo = Group(id, pointsRoot.transform, new Vector3(x, 0f, z));
                    points[index] = pointGo.AddComponent<MeasurementPoint>();
                    Configure(points[index], ("pointId", id));
                    Part(PrimitiveType.Cylinder, "Marker", pointGo.transform, new Vector3(0f, 0.001f, 0f), Vector3.zero, new Vector3(0.05f, 0.001f, 0.05f), pointMarker);
                    index++;
                }
            }

            Configure(pointSet, ("points", points));
            Configure(mold, ("moldId", "MOLD-0001"), ("body", body.GetComponent<Renderer>()), ("measurementPoints", pointSet));
            return mold;
        }

        // ---------- Robot arm ----------

        private static RobotArmRig BuildRobotArm(Transform parent, out HardnessProbe probe)
        {
            var arm = Group("RobotArm", parent, RobotBasePosition);
            var rig = arm.AddComponent<RobotArmRig>();
            Part(PrimitiveType.Cylinder, "BasePlate", arm.transform, new Vector3(0f, 0.01f, 0f), Vector3.zero, new Vector3(0.7f, 0.01f, 0.7f), robotJoint);
            Part(PrimitiveType.Cylinder, "Pedestal", arm.transform, new Vector3(0f, 0.26f, 0f), Vector3.zero, new Vector3(0.4f, 0.24f, 0.4f), robotJoint);

            var alongX = new Vector3(0f, 0f, 90f);
            var alongZ = new Vector3(90f, 0f, 0f);

            var j1 = Joint("J1", "J1_BaseYaw", arm.transform, new Vector3(0f, 0.5f, 0f), Vector3.up, -170f, 170f);
            Part(PrimitiveType.Cylinder, "Turret", j1.transform, new Vector3(0f, 0.1f, 0f), Vector3.zero, new Vector3(0.45f, 0.1f, 0.45f), robotPaint);

            var j2 = Joint("J2", "J2_Shoulder", j1.transform, new Vector3(0f, 0.25f, 0f), Vector3.right, -90f, 90f);
            Part(PrimitiveType.Cylinder, "ShoulderHub", j2.transform, Vector3.zero, alongX, new Vector3(0.26f, 0.14f, 0.26f), robotJoint);
            Part(PrimitiveType.Cube, "UpperArm", j2.transform, new Vector3(0f, 0.5f, 0f), Vector3.zero, new Vector3(0.16f, 1.0f, 0.18f), robotPaint);

            var j3 = Joint("J3", "J3_Elbow", j2.transform, new Vector3(0f, 1.0f, 0f), Vector3.right, -120f, 120f);
            Part(PrimitiveType.Cylinder, "ElbowHub", j3.transform, Vector3.zero, alongX, new Vector3(0.22f, 0.12f, 0.22f), robotJoint);
            Part(PrimitiveType.Cube, "Forearm", j3.transform, new Vector3(0f, 0f, 0.4f), Vector3.zero, new Vector3(0.14f, 0.14f, 0.8f), robotPaint);

            var j4 = Joint("J4", "J4_WristRoll", j3.transform, new Vector3(0f, 0f, 0.8f), Vector3.forward, -180f, 180f);
            Part(PrimitiveType.Cylinder, "WristHousing", j4.transform, new Vector3(0f, 0f, 0.1f), alongZ, new Vector3(0.13f, 0.1f, 0.13f), robotPaint);

            var j5 = Joint("J5", "J5_WristPitch", j4.transform, new Vector3(0f, 0f, 0.2f), Vector3.right, -120f, 120f);
            Part(PrimitiveType.Cylinder, "WristHub", j5.transform, Vector3.zero, alongX, new Vector3(0.12f, 0.08f, 0.12f), robotJoint);
            Part(PrimitiveType.Cube, "WristLink", j5.transform, new Vector3(0f, -0.075f, 0f), Vector3.zero, new Vector3(0.08f, 0.15f, 0.08f), robotPaint);

            // J6 rolls about the tool axis, which hangs along -Y from the wrist.
            var j6 = Joint("J6", "J6_FlangeRoll", j5.transform, new Vector3(0f, -0.15f, 0f), Vector3.up, -180f, 180f);

            var endEffectorGo = Group("EndEffector", j6.transform, Vector3.zero);
            var endEffector = endEffectorGo.AddComponent<EndEffector>();
            Part(PrimitiveType.Cylinder, "Flange", endEffectorGo.transform, new Vector3(0f, -0.01f, 0f), Vector3.zero, new Vector3(0.1f, 0.01f, 0.1f), steel);
            var toolMount = Group("ToolMount", endEffectorGo.transform, new Vector3(0f, -0.02f, 0f)).transform;

            probe = BuildProbe(toolMount);
            Configure(endEffector, ("toolMount", toolMount), ("attachedProbe", probe));
            Configure(rig, ("joints", new[] { j1, j2, j3, j4, j5, j6 }), ("endEffector", endEffector));
            rig.SetJointAngles(HomePose);
            return rig;
        }

        private static RobotJoint Joint(string jointName, string objectName, Transform parent, Vector3 localPosition, Vector3 axis, float min, float max)
        {
            var joint = Group(objectName, parent, localPosition).AddComponent<RobotJoint>();
            Configure(joint, ("jointName", jointName), ("localAxis", axis), ("minAngle", min), ("maxAngle", max),
                ("restLocalRotation", Quaternion.identity));
            return joint;
        }

        private static HardnessProbe BuildProbe(Transform mount)
        {
            // Probe hangs along -Y from the tool mount; the tip is 0.25 m below the mount.
            var probeGo = Group("HardnessProbe", mount, Vector3.zero);
            var probe = probeGo.AddComponent<HardnessProbe>();
            Part(PrimitiveType.Cylinder, "Body", probeGo.transform, new Vector3(0f, -0.09f, 0f), Vector3.zero, new Vector3(0.06f, 0.09f, 0.06f), probeBody);
            Part(PrimitiveType.Cylinder, "Collar", probeGo.transform, new Vector3(0f, -0.195f, 0f), Vector3.zero, new Vector3(0.04f, 0.015f, 0.04f), steel);
            Part(PrimitiveType.Cylinder, "Stem", probeGo.transform, new Vector3(0f, -0.2275f, 0f), Vector3.zero, new Vector3(0.012f, 0.0175f, 0.012f), steel);
            Part(PrimitiveType.Sphere, "Indenter", probeGo.transform, new Vector3(0f, -0.243f, 0f), Vector3.zero, Vector3.one * 0.014f, probeTip);
            var tip = Group("Tip", probeGo.transform, new Vector3(0f, -0.25f, 0f)).transform;
            Configure(probe, ("probeId", "HP-01"), ("tip", tip), ("approachDistance", 0.05f));
            return probe;
        }

        // ---------- Camera ----------

        // Framed so the whole cell, including the arm's reach over P01-P09, sits inside the area the HMI leaves free
        // (left 400, right 460, top 64, bottom 150 reference units) at both 16:9 and 16:10. Viewing direction is the
        // original Stage 1 one, moved closer and panned.
        internal static readonly Vector3 CameraPosition = new Vector3(2.74f, 1.82f, -3.17f);
        internal static readonly Vector3 CameraLookAt = new Vector3(0.17f, 0.63f, -0.55f);

        internal static void SetupCamera(Camera camera)
        {
            if (camera == null)
            {
                camera = new GameObject("Main Camera").AddComponent<Camera>();
                camera.tag = "MainCamera";
            }
            camera.transform.position = CameraPosition;
            camera.transform.LookAt(CameraLookAt);
            camera.fieldOfView = 45f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 100f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.16f, 0.17f, 0.19f);
        }

        // ---------- Helpers ----------

        private static void CreateMaterials()
        {
            robotPaint = GetOrCreateMaterial("RobotPaint", new Color(0.95f, 0.45f, 0.05f), 0f, 0.55f);
            robotJoint = GetOrCreateMaterial("RobotJoint", new Color(0.18f, 0.18f, 0.2f), 0.3f, 0.5f);
            steel = GetOrCreateMaterial("Steel", new Color(0.7f, 0.72f, 0.75f), 0.9f, 0.6f);
            probeBody = GetOrCreateMaterial("ProbeBody", new Color(0.12f, 0.25f, 0.45f), 0.2f, 0.6f);
            probeTip = GetOrCreateMaterial("ProbeTip", new Color(0.85f, 0.1f, 0.1f), 0.5f, 0.7f);
            sand = GetOrCreateMaterial("SandMold", new Color(0.76f, 0.64f, 0.45f), 0f, 0.1f);
            fixture = GetOrCreateMaterial("Fixture", new Color(0.32f, 0.36f, 0.4f), 0.6f, 0.4f);
            concrete = GetOrCreateMaterial("ConcreteFloor", new Color(0.36f, 0.36f, 0.35f), 0f, 0.2f);
            safetyYellow = GetOrCreateMaterial("SafetyYellow", new Color(0.95f, 0.78f, 0.05f), 0f, 0.4f);
            fence = GetOrCreateMaterial("FenceRail", new Color(0.15f, 0.15f, 0.15f), 0.4f, 0.4f);
            pointMarker = GetOrCreateMaterial("PointMarker", new Color(0.1f, 0.9f, 1f), 0f, 0.5f, emission: new Color(0.05f, 0.45f, 0.5f));
        }

        private static Material GetOrCreateMaterial(string name, Color color, float metallic, float smoothness, Color? emission = null)
        {
            var path = MaterialFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new System.InvalidOperationException("URP Lit shader not found; is URP the active render pipeline?");
            material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            if (emission.HasValue)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission.Value);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static GameObject Group(string name, Transform parent, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            return go;
        }

        /// <summary>Placeholder visual. Cylinder primitives are 1 m wide and 2 m tall, so scale.y is half the height.</summary>
        private static GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 localEuler,
            Vector3 scale, Material material, bool keepCollider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.Euler(localEuler);
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        /// <summary>Assigns private serialized fields so runtime components need no public setters.</summary>
        internal static void Configure(Object target, params (string field, object value)[] values)
        {
            var serialized = new SerializedObject(target);
            foreach (var (field, value) in values)
            {
                var property = serialized.FindProperty(field);
                if (property == null) throw new System.ArgumentException(target.GetType().Name + " has no serialized field '" + field + "'");
                switch (value)
                {
                    case Object[] array:
                        property.arraySize = array.Length;
                        for (int i = 0; i < array.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = array[i];
                        break;
                    case Object reference: property.objectReferenceValue = reference; break;
                    case string text: property.stringValue = text; break;
                    case float number: property.floatValue = number; break;
                    case Vector3 vector: property.vector3Value = vector; break;
                    case Quaternion rotation: property.quaternionValue = rotation; break;
                    default: throw new System.ArgumentException("Unsupported value type for field '" + field + "'");
                }
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
        }
    }
}
