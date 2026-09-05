using System;
using System.Collections;
using LudoGame.Board;
using UnityEngine;

namespace LudoGame.Player
{
    /// <summary>
    /// One movable token (goti) belonging to a player. Reads all its positions
    /// from GridManager, so it never hardcodes any world coordinate itself.
    ///
    /// Setup (in the Unity Editor):
    /// 1. Put this script on your token prefab (the little 3D piece that stands
    ///    on the board), alongside its model/mesh.
    /// 2. In the Inspector, set "Color" to the token's color and "Yard Slot"
    ///    to a number 0-3 (each color has 4 tokens, one per yard spot).
    /// 3. Make sure a GridManager already exists in the scene - this script
    ///    talks to it through GridManager.Instance.
    /// 4. A GameManager / DiceManager script (built later) will call
    ///    MoveByDice(diceValue) whenever it is this token's turn to move.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerToken : MonoBehaviour
    {
        public enum TokenState { InYard, OnBoard, InHomeStretch, Finished }

        [Header("Identity")]
        [Tooltip("Which player color this token belongs to.")]
        [SerializeField] private GridManager.PlayerColor color;

        [Tooltip("Which of the 4 yard spots (0-3) this token starts on.")]
        [Range(0, 3)]
        [SerializeField] private int yardSlot;

        [Header("Movement Feel")]
        [Tooltip("How many cells the token hops through per second.")]
        [SerializeField] private float moveSpeed = 4f;

        [Tooltip("How high the token arcs up while hopping from one cell to the next.")]
        [SerializeField] private float hopHeight = 0.3f;

        [Tooltip("How many cells make up this token's home stretch (must match GridManager's lists).")]
        [SerializeField] private int homeStretchLength = 6;

        /// <summary>Current high-level state of the token (yard / on the shared path / home stretch / finished).</summary>
        public TokenState State { get; private set; } = TokenState.InYard;

        /// <summary>True while the token is playing its hop-to-hop movement animation.</summary>
        public bool IsMoving { get; private set; }

        public GridManager.PlayerColor Color => color;

        /// <summary>Raised after the token finishes animating a move (whole dice roll consumed).</summary>
        public event Action<PlayerToken> OnMoveFinished;

        /// <summary>Raised when this token gets captured and sent back to its yard.</summary>
        public event Action<PlayerToken> OnTokenCaptured;

        // Distance travelled since leaving the yard.
        // -1                          = still in the yard.
        // 0 .. (mainLen - 2)          = walking the shared main path.
        // (mainLen - 1) .. FinishDist-1 = walking this color's home stretch.
        // FinishDist                  = reached home, finished.
        private int distanceTravelled = -1;

        /// <summary>
        /// Total distance (in steps) from the yard to home for this token's
        /// color. Guarded: if GridManager.Instance isn't available yet (e.g.
        /// this token woke up before the board finished setting up), returns
        /// a large sentinel instead of throwing, so CanMove()/MoveRoutine()
        /// safely treat the token as "can't move yet" rather than crashing.
        /// </summary>
        private int FinishDistance
        {
            get
            {
                if (GridManager.Instance == null)
                {
                    Debug.LogError("PlayerToken (" + name + "): GridManager.Instance is null - is a GridManager present in the scene?", this);
                    return 999;
                }

                return (GridManager.Instance.MainPathLength - 1) + homeStretchLength;
            }
        }

        /// <summary>How many more steps this token needs to reach home. 0 once Finished.</summary>
        public int StepsRemaining => State == TokenState.Finished ? 0 : Mathf.Max(0, FinishDistance - distanceTravelled);

        private void Start()
        {
            SnapToYard();
        }

        /// <summary>Instantly places the token on its yard spot (no animation). Used on game start or after a capture.</summary>
        public void SnapToYard()
        {
            StopAllCoroutines();
            IsMoving = false;
            State = TokenState.InYard;
            distanceTravelled = -1;
            transform.position = GridManager.Instance.GetYardPosition(color, yardSlot);
        }

        /// <summary>Whether this token is allowed to use the given dice value right now.</summary>
        public bool CanMove(int diceValue)
        {
            if (IsMoving || State == TokenState.Finished) return false;
            if (State == TokenState.InYard) return diceValue == 6;
            return distanceTravelled + diceValue <= FinishDistance;
        }

        /// <summary>Call this with the rolled dice value to move the token (does nothing if the move is not legal).</summary>
        public void MoveByDice(int diceValue)
        {
            if (!CanMove(diceValue)) return;

            int targetDistance = (State == TokenState.InYard) ? 0 : distanceTravelled + diceValue;
            StartCoroutine(MoveRoutine(targetDistance));
        }

        /// <summary>Called by GameManager when an opponent lands on this token's cell.</summary>
        public void SendBackToYard()
        {
            SnapToYard();
            OnTokenCaptured?.Invoke(this);
        }

        /// <summary>Main path index this token currently sits on, or -1 if it isn't on the shared path.</summary>
        public int CurrentMainPathIndex()
        {
            if (State != TokenState.OnBoard) return -1;
            int mainLen = GridManager.Instance.MainPathLength;
            return (GridManager.Instance.GetStartIndex(color) + distanceTravelled) % mainLen;
        }

        /// <summary>True if the token is currently standing on a safe (star) cell.</summary>
        public bool IsOnSafeCell()
        {
            int index = CurrentMainPathIndex();
            return index >= 0 && GridManager.Instance.IsSafeCell(index);
        }

        // ---------------- Movement animation ----------------

        private IEnumerator MoveRoutine(int targetDistance)
        {
            IsMoving = true;
            targetDistance = Mathf.Min(targetDistance, FinishDistance);

            int step = distanceTravelled;
            int safety = 0;

            // Safety net: a single move should never take more than one lap of the board
            // (FinishDistance steps, ~57 by default). If it ever needed more than that,
            // something is wrong (e.g. a corrupted distance/GridManager setup) - bail out
            // instead of looping forever and freezing the turn.
            while (step < targetDistance && safety < 1000)
            {
                step++;
                Vector3 targetPos = GetWorldPositionForDistance(step);
                yield return HopTo(targetPos);

                distanceTravelled = step;
                State = (step >= FinishDistance) ? TokenState.Finished
                       : (step > GridManager.Instance.MainPathLength - 2) ? TokenState.InHomeStretch
                       : TokenState.OnBoard;

                safety++;
            }

            if (safety >= 1000)
                Debug.LogError("PlayerToken (" + name + "): MoveRoutine safety limit hit (step=" + step +
                    ", targetDistance=" + targetDistance + "). Aborting this move instead of freezing the game - " +
                    "check GridManager's waypoint lists and this token's Home Stretch Length.", this);

            IsMoving = false;
            OnMoveFinished?.Invoke(this);
        }

        private Vector3 GetWorldPositionForDistance(int distance)
        {
            int mainLen = GridManager.Instance.MainPathLength;
            if (distance <= mainLen - 2)
            {
                int mainIndex = GridManager.Instance.GetStartIndex(color) + distance;
                return GridManager.Instance.GetMainPathPosition(mainIndex);
            }

            int homeStep = distance - (mainLen - 1); // 0-based index into this color's home stretch
            return GridManager.Instance.GetHomePathPosition(color, homeStep);
        }

        private IEnumerator HopTo(Vector3 targetPos)
        {
            Vector3 startPos = transform.position;
            float duration = 1f / Mathf.Max(moveSpeed, 0.01f);
            float t = 0f;
            int safety = 0;

            // Safety net: if Time.deltaTime is ever stuck at 0 (e.g. Time.timeScale == 0,
            // such as a paused game), "t" would never reach "duration" and this would hop
            // forever. Bail out after an absurd number of frames instead of hanging.
            while (t < duration && safety < 100000)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / duration);
                Vector3 pos = Vector3.Lerp(startPos, targetPos, p);
                pos.y += Mathf.Sin(p * Mathf.PI) * hopHeight; // small arc so the hop looks alive
                transform.position = pos;
                safety++;
                yield return null;
            }

            if (safety >= 100000)
                Debug.LogWarning("PlayerToken (" + name + "): HopTo safety limit hit (Time.deltaTime may be stuck at 0, e.g. Time.timeScale == 0). Snapping to target instead of freezing.", this);

            transform.position = targetPos;
        }
    }
}
