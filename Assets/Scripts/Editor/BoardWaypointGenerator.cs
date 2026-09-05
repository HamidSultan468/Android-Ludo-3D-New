using System.Collections.Generic;
using LudoGame.Board;
using UnityEditor;
using UnityEngine;

namespace LudoGame.EditorTools
{
    /// <summary>
    /// Editor window that procedurally builds all of GridManager's waypoints
    /// (52 main-path cells + 24 home-stretch cells + 16 yard cells) on a
    /// 15x15 logical grid, and assigns them straight into GridManager's
    /// Inspector lists. No more placing 90+ empty GameObjects by hand.
    ///
    /// The layout is generated with math (one arm's pattern rotated 90 degrees,
    /// three times) instead of hand-typed coordinates, so it is guaranteed to
    /// form one consistent, closed loop with each color spaced exactly a
    /// quarter of the board apart.
    ///
    /// How to use:
    /// 1. Run "1. Scene Bootstrapper" first, so a GridManager exists.
    /// 2. Window > Ludo Tools > 2. Board Waypoint Generator.
    /// 3. Drag your GridManager into "Grid Manager".
    /// 4. Drag (or create) an empty GameObject positioned at the CENTER of
    ///    your board art into "Board Origin". Rotate/move it in the Scene
    ///    view until the generated dots line up with your actual board -
    ///    GridManager draws small gizmo markers on its assigned waypoints
    ///    once you select it, so you can see them while adjusting.
    /// 5. Set "Cell Size" to roughly the real-world size of one board square.
    /// 6. Click "Generate Waypoints". Re-clicking regenerates and re-links
    ///    everything (old waypoints from a previous run are replaced).
    /// </summary>
    public class BoardWaypointGenerator : EditorWindow
    {
        private GridManager gridManager;
        private Transform boardOrigin;
        private float cellSize = 1f;
        private const int GridSize = 15; // a classic Ludo board is a 15x15 grid of cells

        // One arm's main-path template (13 cells), for the color whose yard sits
        // in the "top-left" quadrant of the grid. The other 3 arms are this exact
        // template rotated 90, 180 and 270 degrees around the board's center.
        private static readonly Vector2Int[] ArmPathTemplate =
        {
            new Vector2Int(1, 6), new Vector2Int(2, 6), new Vector2Int(3, 6), new Vector2Int(4, 6), new Vector2Int(5, 6),
            new Vector2Int(6, 5), new Vector2Int(6, 4), new Vector2Int(6, 3), new Vector2Int(6, 2), new Vector2Int(6, 1), new Vector2Int(6, 0),
            new Vector2Int(7, 0), new Vector2Int(8, 0),
        };

        // One arm's 6-cell home stretch template, leading from the edge to the center.
        private static readonly Vector2Int[] HomeStretchTemplate =
        {
            new Vector2Int(1, 7), new Vector2Int(2, 7), new Vector2Int(3, 7), new Vector2Int(4, 7), new Vector2Int(5, 7), new Vector2Int(6, 7),
        };

        // One color's 4 yard (waiting spot) positions inside its 6x6 corner quadrant.
        private static readonly Vector2Int[] YardTemplate =
        {
            new Vector2Int(1, 1), new Vector2Int(4, 1), new Vector2Int(1, 4), new Vector2Int(4, 4),
        };

        private static readonly string[] ColorNames = { "Red", "Green", "Yellow", "Blue" };

        [MenuItem("Window/Ludo Tools/2. Board Waypoint Generator")]
        private static void Open()
        {
            GetWindow<BoardWaypointGenerator>("Board Waypoints");
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Generates all 92 board waypoints (52 main path + 24 home stretch + 16 yard) " +
                "and links them into GridManager automatically.", MessageType.Info);

            gridManager = (GridManager)EditorGUILayout.ObjectField("Grid Manager", gridManager, typeof(GridManager), true);
            boardOrigin = (Transform)EditorGUILayout.ObjectField("Board Origin", boardOrigin, typeof(Transform), true);
            cellSize = EditorGUILayout.FloatField("Cell Size (world units)", cellSize);

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(gridManager == null || boardOrigin == null))
            {
                if (GUILayout.Button("Generate Waypoints", GUILayout.Height(32)))
                {
                    Generate();
                }
            }
        }

        private void Generate()
        {
            Transform root = FindOrCreateChild(boardOrigin, "BoardWaypoints");
            DeleteChildren(root);

            Transform pathRoot = FindOrCreateChild(root, "MainPath");
            Transform homeRoot = FindOrCreateChild(root, "HomeStretches");
            Transform yardRoot = FindOrCreateChild(root, "Yards");

            var mainPath = new List<Transform>();
            var homePaths = new List<Transform>[4];
            var yards = new List<Transform>[4];
            for (int i = 0; i < 4; i++)
            {
                homePaths[i] = new List<Transform>();
                yards[i] = new List<Transform>();
            }

            int cellIndex = 0;
            for (int color = 0; color < 4; color++)
            {
                foreach (Vector2Int cell in ArmPathTemplate)
                {
                    Vector2Int rotated = RotateN(cell, color);
                    Transform t = CreateWaypoint(pathRoot, "Cell_" + cellIndex.ToString("00"), rotated);
                    mainPath.Add(t);
                    cellIndex++;
                }
            }

            for (int color = 0; color < 4; color++)
            {
                Transform colorHomeRoot = FindOrCreateChild(homeRoot, ColorNames[color]);
                for (int i = 0; i < HomeStretchTemplate.Length; i++)
                {
                    Vector2Int rotated = RotateN(HomeStretchTemplate[i], color);
                    homePaths[color].Add(CreateWaypoint(colorHomeRoot, "Home_" + i, rotated));
                }
            }

            for (int color = 0; color < 4; color++)
            {
                Transform colorYardRoot = FindOrCreateChild(yardRoot, ColorNames[color]);
                for (int i = 0; i < YardTemplate.Length; i++)
                {
                    Vector2Int rotated = RotateN(YardTemplate[i], color);
                    yards[color].Add(CreateWaypoint(colorYardRoot, "Yard_" + i, rotated));
                }
            }

            ApplyToGridManager(mainPath, homePaths, yards);
            EditorUtility.SetDirty(gridManager);

            EditorUtility.DisplayDialog("Board Waypoints",
                "Generated " + mainPath.Count + " main-path cells, " + (homePaths[0].Count * 4) +
                " home-stretch cells, and " + (yards[0].Count * 4) + " yard cells, and linked them into GridManager.\n\n" +
                "Move/rotate/scale the Board Origin object to line them up with your board art.", "OK");
        }

        private Transform CreateWaypoint(Transform parent, string name, Vector2Int gridCell)
        {
            GameObject go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Generate Board Waypoints");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = GridToLocalPosition(gridCell);
            return go.transform;
        }

        private Vector3 GridToLocalPosition(Vector2Int cell)
        {
            float half = (GridSize - 1) / 2f;
            float x = (cell.x - half) * cellSize;
            float z = (half - cell.y) * cellSize; // row 0 = top edge = +Z
            return new Vector3(x, 0f, z);
        }

        /// <summary>Rotates a grid cell 90 degrees (clockwise, seen from above) around the board center, "times" times.</summary>
        private static Vector2Int RotateN(Vector2Int cell, int times)
        {
            for (int i = 0; i < times; i++)
                cell = new Vector2Int(GridSize - 1 - cell.y, cell.x);
            return cell;
        }

        private void ApplyToGridManager(List<Transform> mainPath, List<Transform>[] homePaths, List<Transform>[] yards)
        {
            var so = new SerializedObject(gridManager);

            SetTransformList(so, "mainPath", mainPath);
            SetTransformList(so, "redHomePath", homePaths[0]);
            SetTransformList(so, "greenHomePath", homePaths[1]);
            SetTransformList(so, "yellowHomePath", homePaths[2]);
            SetTransformList(so, "blueHomePath", homePaths[3]);
            SetTransformList(so, "redYard", yards[0]);
            SetTransformList(so, "greenYard", yards[1]);
            SetTransformList(so, "yellowYard", yards[2]);
            SetTransformList(so, "blueYard", yards[3]);

            SetInt(so, "redStartIndex", 0);
            SetInt(so, "greenStartIndex", ArmPathTemplate.Length);
            SetInt(so, "yellowStartIndex", ArmPathTemplate.Length * 2);
            SetInt(so, "blueStartIndex", ArmPathTemplate.Length * 3);

            so.ApplyModifiedProperties();
        }

        private static void SetTransformList(SerializedObject so, string fieldName, List<Transform> values)
        {
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null) return;

            prop.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        private static void SetInt(SerializedObject so, string fieldName, int value)
        {
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop != null) prop.intValue = value;
        }

        private static Transform FindOrCreateChild(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null) return existing;

            GameObject go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Generate Board Waypoints");
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static void DeleteChildren(Transform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(root.GetChild(i).gameObject);
        }
    }
}
