using System.Linq;
using LudoGame.Board;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LudoGame.EditorTools
{
    /// <summary>
    /// Re-links GridManager's waypoint lists to whatever Transforms already
    /// exist under LudoBoard/BoardWaypoints in the scene - it does not
    /// create or move anything (see "2. Board Waypoint Generator" for that).
    /// This is a repair/relink tool for when the hierarchy already matches
    /// the expected layout but GridManager's references got lost, were
    /// cleared, or need refreshing after manual hierarchy edits.
    ///
    /// Expects this exact hierarchy:
    ///   LudoBoard
    ///     BoardWaypoints
    ///       MainPath                    (52 children, in travel order)
    ///       HomeStretches
    ///         Red / Green / Yellow / Blue   (children = that color's home stretch, in order)
    ///       Yards
    ///         Red / Green / Yellow / Blue   (children = that color's 4 yard spots)
    ///
    /// How to use: Window > Ludo Tools > Auto Assign Grid Paths.
    /// </summary>
    public static class GridAutoAssigner
    {
        private static readonly string[] ColorNames = { "Red", "Green", "Yellow", "Blue" };

        [MenuItem("Window/Ludo Tools/Auto Assign Grid Paths")]
        private static void AutoAssign()
        {
            GridManager gridManager = Object.FindAnyObjectByType<GridManager>();
            if (gridManager == null)
            {
                Debug.LogError("[GridAutoAssigner] No GridManager found in the active scene.");
                return;
            }

            GameObject ludoBoard = GameObject.Find("LudoBoard");
            if (ludoBoard == null)
            {
                Debug.LogError("[GridAutoAssigner] No 'LudoBoard' object found in the active scene.");
                return;
            }

            Transform boardWaypoints = ludoBoard.transform.Find("BoardWaypoints");
            if (boardWaypoints == null)
            {
                Debug.LogError("[GridAutoAssigner] 'LudoBoard' has no 'BoardWaypoints' child. " +
                    "Actual children of 'LudoBoard': " + DescribeChildren(ludoBoard.transform));
                return;
            }

            Debug.Log("[GridAutoAssigner] Found 'BoardWaypoints' with children: " + DescribeChildren(boardWaypoints));

            Undo.RecordObject(gridManager, "Auto Assign Grid Paths");
            var so = new SerializedObject(gridManager);

            bool allFound = true;
            allFound &= AssignMainPath(so, boardWaypoints);
            allFound &= AssignColorGroup(so, boardWaypoints, "HomeStretches", "HomePath");
            allFound &= AssignColorGroup(so, boardWaypoints, "Yards", "Yard");

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(gridManager);

            // Actually write the fix to disk - without this, a headless (-executeMethod) run
            // would lose all these changes the instant Unity quits, and even in the Editor
            // it's easy to forget to hit Ctrl+S afterward.
            EditorSceneManager.MarkSceneDirty(gridManager.gameObject.scene);
            bool saved = EditorSceneManager.SaveScene(gridManager.gameObject.scene);
            Debug.Log("[GridAutoAssigner] Scene '" + gridManager.gameObject.scene.name + "' saved: " + saved);

            if (allFound)
                Debug.Log("[GridAutoAssigner] All grid paths assigned successfully.");
            else
                Debug.LogWarning("[GridAutoAssigner] Finished, but some paths were missing - see the warnings above. " +
                    "GridManager was still updated for whatever was found.");
        }

        private static bool AssignMainPath(SerializedObject so, Transform boardWaypoints)
        {
            Transform mainPath = boardWaypoints.Find("MainPath");
            if (mainPath == null)
            {
                Debug.LogWarning("[GridAutoAssigner] Missing 'BoardWaypoints/MainPath' - mainPath left untouched.");
                return false;
            }

            SetTransformList(so, "mainPath", mainPath);

            if (mainPath.childCount != 52)
                Debug.LogWarning("[GridAutoAssigner] 'MainPath' has " + mainPath.childCount + " children, expected 52.");

            Debug.Log("[GridAutoAssigner] Assigned mainPath (" + mainPath.childCount + " cells).");
            return true;
        }

        /// <summary>Assigns the 4 per-color fields under a group (e.g. "HomeStretches" -> redHomePath, greenHomePath, ...).</summary>
        private static bool AssignColorGroup(SerializedObject so, Transform boardWaypoints, string groupName, string fieldSuffix)
        {
            Transform group = boardWaypoints.Find(groupName);
            if (group == null)
            {
                Debug.LogWarning("[GridAutoAssigner] Missing 'BoardWaypoints/" + groupName + "' - skipped. " +
                    "Actual children of 'BoardWaypoints': " + DescribeChildren(boardWaypoints));
                return false;
            }

            Debug.Log("[GridAutoAssigner] Found '" + groupName + "' with children: " + DescribeChildren(group));

            bool allFound = true;
            foreach (string color in ColorNames)
            {
                string fieldName = color.ToLowerInvariant() + fieldSuffix;
                Transform colorFolder = group.Find(color);

                if (colorFolder == null)
                {
                    Debug.LogWarning("[GridAutoAssigner] Missing 'BoardWaypoints/" + groupName + "/" + color +
                        "' - " + fieldName + " left untouched. Actual children of '" + groupName + "': " + DescribeChildren(group));
                    allFound = false;
                    continue;
                }

                SetTransformList(so, fieldName, colorFolder);
                Debug.Log("[GridAutoAssigner] Assigned " + fieldName + " (" + colorFolder.childCount + " cells).");
            }

            return allFound;
        }

        /// <summary>Lists a transform's immediate children by name, for diagnostic log messages (e.g. "[Red, Green]" or "(none)").</summary>
        private static string DescribeChildren(Transform parent)
        {
            if (parent.childCount == 0) return "(none)";

            return "[" + string.Join(", ", Enumerable.Range(0, parent.childCount).Select(i => parent.GetChild(i).name)) + "]";
        }

        private static void SetTransformList(SerializedObject so, string fieldName, Transform parent)
        {
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogError("[GridAutoAssigner] GridManager has no field named '" + fieldName + "'.");
                return;
            }

            prop.arraySize = parent.childCount;
            for (int i = 0; i < parent.childCount; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = parent.GetChild(i);
        }
    }
}
