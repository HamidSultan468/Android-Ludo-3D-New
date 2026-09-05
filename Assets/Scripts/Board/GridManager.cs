using System.Collections.Generic;
using UnityEngine;

namespace LudoGame.Board
{
    /// <summary>
    /// Central manager for all board grid positions in the Ludo game.
    /// Holds the shared 52-cell main path, each color's home stretch,
    /// each color's yard (starting) spots, and safe-cell info.
    ///
    /// Setup (in the Unity Editor):
    /// 1. Create an empty GameObject on the board for every cell
    ///    (e.g. name them Cell_00, Cell_01, ... Cell_51) and position
    ///    them exactly on top of the board artwork squares.
    /// 2. Drag those Transforms into the "Main Path" list below, in the
    ///    exact order a token walks (clockwise, starting at Red's entry).
    /// 3. Do the same for each color's 6-cell home stretch and 4-spot yard.
    /// 4. Put this script on a single "GridManager" (or "Board") GameObject
    ///    in the scene. Other scripts (e.g. a future PlayerToken.cs) will
    ///    read positions from GridManager.Instance.
    /// </summary>
    public class GridManager : MonoBehaviour
    {
        public enum PlayerColor { Red, Green, Yellow, Blue }

        private static GridManager instance;

        /// <summary>
        /// The active GridManager in the scene. Self-healing: a destroyed Unity
        /// Object compares equal to null (Unity's overloaded ==), so if the
        /// previously cached instance was destroyed (e.g. after a scene reload)
        /// this automatically looks up the current one instead of returning a
        /// stale reference - which would otherwise throw MissingReferenceException
        /// the next time any member is accessed on it.
        /// </summary>
        public static GridManager Instance
        {
            get
            {
                if (instance == null)
                    instance = FindAnyObjectByType<GridManager>();
                return instance;
            }
        }

        [Header("Main Path (52 shared cells, in travel order)")]
        [Tooltip("Drag the 52 waypoint Transforms here, in the order tokens travel (clockwise), starting from Red's entry cell.")]
        [SerializeField] private List<Transform> mainPath = new List<Transform>();

        [Header("Home Stretch Paths (6 cells each, per color)")]
        [SerializeField] private List<Transform> redHomePath = new List<Transform>();
        [SerializeField] private List<Transform> greenHomePath = new List<Transform>();
        [SerializeField] private List<Transform> yellowHomePath = new List<Transform>();
        [SerializeField] private List<Transform> blueHomePath = new List<Transform>();

        [Header("Yard Positions (4 token spots per color)")]
        [SerializeField] private List<Transform> redYard = new List<Transform>();
        [SerializeField] private List<Transform> greenYard = new List<Transform>();
        [SerializeField] private List<Transform> yellowYard = new List<Transform>();
        [SerializeField] private List<Transform> blueYard = new List<Transform>();

        [Header("Entry Index Into Main Path (per color)")]
        [Tooltip("Index inside Main Path where each color's tokens appear when they leave the yard.")]
        [SerializeField] private int redStartIndex = 0;
        [SerializeField] private int greenStartIndex = 13;
        [SerializeField] private int yellowStartIndex = 26;
        [SerializeField] private int blueStartIndex = 39;

        [Header("Safe Cells (star squares, tokens cannot be captured here)")]
        [SerializeField] private List<int> safeCellIndexes = new List<int> { 0, 8, 13, 21, 26, 34, 39, 47 };

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
        }

        private void OnDestroy()
        {
            // Clear the static reference so Instance doesn't keep pointing at a
            // destroyed object (see the self-healing getter above).
            if (instance == this)
                instance = null;
        }

        // ---------------- Public read-only info ----------------

        /// <summary>Total number of cells on the shared main path.</summary>
        public int MainPathLength => mainPath.Count;

        /// <summary>World position of a cell on the shared main path (wraps around automatically).</summary>
        public Vector3 GetMainPathPosition(int index)
        {
            if (mainPath.Count == 0) return transform.position;
            int wrapped = ((index % mainPath.Count) + mainPath.Count) % mainPath.Count;
            return mainPath[wrapped].position;
        }

        /// <summary>World position of a cell inside a color's home stretch (0 = first cell after leaving main path).</summary>
        public Vector3 GetHomePathPosition(PlayerColor color, int stepIndex)
        {
            List<Transform> path = GetHomePathList(color);
            if (path.Count == 0) return transform.position;
            stepIndex = Mathf.Clamp(stepIndex, 0, path.Count - 1);
            return path[stepIndex].position;
        }

        /// <summary>World position of one of the 4 waiting spots in a color's yard.</summary>
        public Vector3 GetYardPosition(PlayerColor color, int tokenSlot)
        {
            List<Transform> yard = GetYardList(color);
            if (yard.Count == 0) return transform.position;
            tokenSlot = Mathf.Clamp(tokenSlot, 0, yard.Count - 1);
            return yard[tokenSlot].position;
        }

        /// <summary>Main path index where a color's tokens enter after leaving the yard.</summary>
        public int GetStartIndex(PlayerColor color)
        {
            switch (color)
            {
                case PlayerColor.Red: return redStartIndex;
                case PlayerColor.Green: return greenStartIndex;
                case PlayerColor.Yellow: return yellowStartIndex;
                case PlayerColor.Blue: return blueStartIndex;
                default: return 0;
            }
        }

        /// <summary>True if the given main-path index is a safe (star) cell.</summary>
        public bool IsSafeCell(int mainPathIndex)
        {
            return safeCellIndexes.Contains(mainPathIndex);
        }

        // ---------------- Internal helpers ----------------

        private List<Transform> GetHomePathList(PlayerColor color)
        {
            switch (color)
            {
                case PlayerColor.Red: return redHomePath;
                case PlayerColor.Green: return greenHomePath;
                case PlayerColor.Yellow: return yellowHomePath;
                case PlayerColor.Blue: return blueHomePath;
                default: return redHomePath;
            }
        }

        private List<Transform> GetYardList(PlayerColor color)
        {
            switch (color)
            {
                case PlayerColor.Red: return redYard;
                case PlayerColor.Green: return greenYard;
                case PlayerColor.Yellow: return yellowYard;
                case PlayerColor.Blue: return blueYard;
                default: return redYard;
            }
        }

#if UNITY_EDITOR
        // Draws small markers in the Scene view so you can see the assigned
        // grid positions while setting the board up.
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            foreach (var cell in mainPath)
            {
                if (cell != null) Gizmos.DrawWireSphere(cell.position, 0.15f);
            }

            DrawHomeGizmo(redHomePath, Color.red);
            DrawHomeGizmo(greenHomePath, Color.green);
            DrawHomeGizmo(yellowHomePath, Color.yellow);
            DrawHomeGizmo(blueHomePath, Color.blue);
        }

        private void DrawHomeGizmo(List<Transform> path, Color color)
        {
            Gizmos.color = color;
            foreach (var cell in path)
            {
                if (cell != null) Gizmos.DrawWireCube(cell.position, Vector3.one * 0.2f);
            }
        }
#endif
    }
}
