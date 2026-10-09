using HardnessCell.Cell;
using HardnessCell.Hmi;
using HardnessCell.Mold;
using HardnessCell.Workflow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HardnessCell.Editor
{
    /// <summary>
    /// Adds the operator HMI (screen-space overlay Canvas) and, if missing, an Input System EventSystem to the open scene.
    /// Layout is in 1920x1080 reference units: a header bar plus a right-hand panel, leaving the cell view visible.
    /// </summary>
    public static class HmiInstaller
    {
        private const string RootName = "OperatorHMI";
        internal const float HeaderHeight = 64f;
        private const float PanelWidth = 460f;
        internal const float Pad = 20f;
        private const float ContentWidth = PanelWidth - 2f * Pad;

        private static Font font;

        [MenuItem("Tools/Hardness Cell/Add Operator HMI To Open Scene")]
        private static void InstallInActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            TestingCell cell = null;
            HmiController existing = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (cell == null) cell = root.GetComponentInChildren<TestingCell>(true);
                if (existing == null) existing = root.GetComponentInChildren<HmiController>(true);
            }

            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                Debug.Log("Operator HMI is already installed on " + existing.name, existing);
                return;
            }
            var workflow = cell != null ? cell.GetComponentInChildren<MeasurementWorkflow>(true) : null;
            if (workflow == null)
            {
                EditorUtility.DisplayDialog("Add Operator HMI",
                    "The active scene needs a TestingCell with a MeasurementWorkflow. Open HardnessTestingCell.unity and add the measurement workflow first.", "OK");
                return;
            }

            var hmi = Install(cell, workflow);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = hmi.gameObject;
            Debug.Log("Operator HMI added and scene saved.", hmi);
        }

        public static HmiController Install(TestingCell cell, MeasurementWorkflow workflow)
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            EnsureEventSystem(cell.gameObject.scene);

            var root = new GameObject(RootName, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(root, "Add Operator HMI");
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            root.AddComponent<GraphicRaycaster>();
            var controller = root.AddComponent<HmiController>();

            // ----- Header -----
            var header = Box("Header", root.transform, HmiPalette.HeaderBackground);
            Anchor(header.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, HeaderHeight));
            var accent = Box("AccentLine", header.transform, HmiPalette.Accent);
            Anchor(accent.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 3f));
            var title = Label("Title", header.transform, "SAND MOLD HARDNESS TESTING", 28, FontStyle.Bold, TextAnchor.MiddleLeft, HmiPalette.TextPrimary);
            Anchor(title.rectTransform, Vector2.zero, new Vector2(0.6f, 1f), new Vector2(0f, 0.5f), new Vector2(24f, 0f), Vector2.zero);
            title.rectTransform.offsetMax = Vector2.zero;
            var moldLabel = Label("MoldIdLabel", header.transform, "MOLD ID", 16, FontStyle.Bold, TextAnchor.MiddleRight, HmiPalette.TextMuted);
            Anchor(moldLabel.rectTransform, new Vector2(1f, 0f), Vector2.one, new Vector2(1f, 0.5f), new Vector2(-256f, 0f), new Vector2(120f, 0f));
            var moldId = Label("MoldIdValue", header.transform, "--", 24, FontStyle.Bold, TextAnchor.MiddleRight, HmiPalette.TextPrimary);
            Anchor(moldId.rectTransform, new Vector2(1f, 0f), Vector2.one, new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(220f, 0f));

            // ----- Right-hand panel -----
            var panel = Box("Panel", root.transform, HmiPalette.PanelBackground);
            Anchor(panel.rectTransform, new Vector2(1f, 0f), Vector2.one, new Vector2(1f, 1f), Vector2.zero, new Vector2(PanelWidth, 0f));
            panel.rectTransform.offsetMax = new Vector2(0f, -HeaderHeight);
            var p = panel.transform;

            SectionLabel(p, "STATUS", 18f);
            var badge = Box("StatusBadge", p, HmiPalette.StatusReady);
            Place(badge.rectTransform, Pad, 46f, ContentWidth, 76f);
            var statusText = Label("StatusText", badge.transform, "READY", 38, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            Stretch(statusText.rectTransform);

            SectionLabel(p, "CURRENT MEASUREMENT", 142f);
            var pointValue = ValueRow(p, "Point", "POINT", 170f);
            var stepValue = ValueRow(p, "Step", "STEP", 204f);
            var readingBox = Box("HardnessBox", p, HmiPalette.InsetBackground);
            Place(readingBox.rectTransform, Pad, 242f, ContentWidth, 72f);
            var readingLabel = Label("Label", readingBox.transform, "HARDNESS", 18, FontStyle.Bold, TextAnchor.MiddleLeft, HmiPalette.TextMuted);
            Stretch(readingLabel.rectTransform, 14f);
            var readingValue = Label("Value", readingBox.transform, "--", 44, FontStyle.Bold, TextAnchor.MiddleRight, HmiPalette.TextPrimary);
            Stretch(readingValue.rectTransform, 14f);

            SectionLabel(p, "PROGRESS", 334f);
            var progressText = Label("ProgressText", p, "0 / 9", 18, FontStyle.Bold, TextAnchor.UpperRight, HmiPalette.TextPrimary);
            Place(progressText.rectTransform, Pad, 334f, ContentWidth, 22f);
            var track = Box("ProgressTrack", p, HmiPalette.ProgressTrack);
            Place(track.rectTransform, Pad, 364f, ContentWidth, 22f);
            var fill = Box("ProgressFill", track.transform, HmiPalette.ProgressFill);
            Anchor(fill.rectTransform, Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);

            SectionLabel(p, "RESULTS", 406f);
            var tiles = BuildTiles(p, cell, 434f);

            var startButton = CreateButton(p, "StartButton", "START TEST", HmiPalette.StartButton, 0f);
            var resetButton = CreateButton(p, "ResetButton", "RESET", HmiPalette.ResetButton, 1f);

            TestingCellSceneBuilder.Configure(controller,
                ("workflow", workflow), ("cell", cell), ("moldIdText", moldId),
                ("statusBadge", badge), ("statusText", statusText),
                ("currentPointText", pointValue), ("currentStepText", stepValue), ("currentReadingText", readingValue),
                ("progressText", progressText), ("progressFill", fill.rectTransform),
                ("pointTiles", tiles), ("startButton", startButton), ("resetButton", resetButton));
            return controller;
        }

        private static PointResultTile[] BuildTiles(Transform parent, TestingCell cell, float top)
        {
            var points = cell.Mold.MeasurementPoints.Points;
            const int columns = 3;
            const float gap = 10f, height = 88f;
            float width = (ContentWidth - gap * (columns - 1)) / columns;

            var grid = new GameObject("ResultsGrid", typeof(RectTransform)).GetComponent<RectTransform>();
            grid.SetParent(parent, false);
            Stretch(grid);

            var tiles = new PointResultTile[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                MeasurementPoint point = points[i];
                var frame = Box("Tile_" + point.PointId, grid, HmiPalette.TileFrame);
                Place(frame.rectTransform, Pad + (i % columns) * (width + gap), top + (i / columns) * (height + gap), width, height);
                var tileFill = Box("Fill", frame.transform, HmiPalette.TileFill);
                Stretch(tileFill.rectTransform, 2f);
                var id = Label("Id", frame.transform, point.PointId, 15, FontStyle.Bold, TextAnchor.UpperLeft, HmiPalette.TextMuted);
                Stretch(id.rectTransform, 10f);
                var value = Label("Value", frame.transform, "--", 34, FontStyle.Bold, TextAnchor.MiddleCenter, HmiPalette.TextDim);
                Stretch(value.rectTransform);
                value.rectTransform.offsetMin = new Vector2(0f, -8f);

                tiles[i] = frame.gameObject.AddComponent<PointResultTile>();
                TestingCellSceneBuilder.Configure(tiles[i], ("pointId", point.PointId), ("frame", frame), ("fill", tileFill),
                    ("idText", id), ("valueText", value));
            }
            return tiles;
        }

        private static Button CreateButton(Transform parent, string name, string caption, Color color, float column)
        {
            const float height = 68f, gap = 12f;
            float width = (ContentWidth - gap) / 2f;
            var button = StyledButton(parent, name, caption, color, 22);
            Anchor((RectTransform)button.transform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(Pad + column * (width + gap), 24f), new Vector2(width, height));
            return button;
        }

        /// <summary>Flat coloured button with the HMI's tint states; caller positions it.</summary>
        internal static Button StyledButton(Transform parent, string name, string caption, Color color, int fontSize)
        {
            var image = Box(name, parent, color);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.88f, 0.88f, 0.88f);
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f);
            colors.disabledColor = new Color(0.4f, 0.4f, 0.4f, 0.6f);
            button.colors = colors;
            var text = Label("Caption", image.transform, caption, fontSize, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            Stretch(text.rectTransform);
            return button;
        }

        internal static Text ValueRow(Transform parent, string name, string caption, float top, float x = Pad, float width = ContentWidth)
        {
            var label = Label(name + "Label", parent, caption, 18, FontStyle.Bold, TextAnchor.MiddleLeft, HmiPalette.TextMuted);
            Place(label.rectTransform, x, top, width, 30f);
            var value = Label(name + "Value", parent, "--", 22, FontStyle.Bold, TextAnchor.MiddleRight, HmiPalette.TextPrimary);
            Place(value.rectTransform, x, top, width, 30f);
            return value;
        }

        internal static void SectionLabel(Transform parent, string caption, float top, float x = Pad, float width = ContentWidth)
        {
            var label = Label("Section_" + caption.Replace(' ', '_'), parent, caption, 14, FontStyle.Bold, TextAnchor.UpperLeft, HmiPalette.TextMuted);
            Place(label.rectTransform, x, top, width, 18f);
            var line = Box("Divider", label.transform, HmiPalette.Divider);
            Anchor(line.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(0f, 1f));
        }

        private static void EnsureEventSystem(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.GetComponentInChildren<EventSystem>(true) != null) return;

            var go = new GameObject("EventSystem", typeof(EventSystem));
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            Undo.RegisterCreatedObjectUndo(go, "Add EventSystem");
        }

        // ---------- Primitive UI helpers ----------

        internal static Image Box(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        internal static Text Label(string name, Transform parent, string content, int size, FontStyle style, TextAnchor alignment, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.font = font;
            text.text = content;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.supportRichText = false;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        internal static void Anchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        /// <summary>Positions a rect by its top-left corner, measured from the parent's top-left (y grows downward).</summary>
        internal static void Place(RectTransform rect, float x, float y, float width, float height) =>
            Anchor(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -y), new Vector2(width, height));

        internal static void Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }
    }
}
