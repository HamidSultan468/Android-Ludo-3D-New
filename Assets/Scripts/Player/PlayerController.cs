using System;
using System.Collections;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Components")]
    public Animator animator;

    [Header("Ludo Board Track")]
    public Transform[] waypoints; // بورڈ کی تمام ٹائلز کا ٹریک
    public float moveSpeed = 5f;  // گوٹی چلنے کی رفتار

    [Header("Audio (optional placeholder)")]
    [Tooltip("Auto-resolved from this GameObject if left empty. Played once per waypoint hop while walking.")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("Footstep sound played on each tile hop. Leave empty to stay silent.")]
    [SerializeField] private AudioClip footstepClip;
    [Range(0f, 1f)] [SerializeField] private float footstepVolume = 0.6f;

    // Actual parameter names on BattleCharacter_Ontroller (verified against the .controller asset) -
    // note every one of these is a Bool parameter, not a true Mecanim Trigger, and casing matters
    // (Animator parameter lookups are case-sensitive string keys).
    private const string ParamIsWalking = "isWalking";
    private const string ParamAttack = "AttackTrigger";
    private const string ParamDeath = "DeathTrigger";
    private const string ParamVictory = "VictoryTrigger";

    // How long a "trigger-style" bool stays true before PulseBool resets it - long enough for a
    // transition with a short/no exit-time to catch the true->false edge, short enough not to block
    // a same-named animation from re-firing shortly after (e.g. two captures in one turn).
    private const float TriggerPulseDuration = 0.15f;

    private int currentWaypointIndex = 0; // گوٹی کا موجودہ پوائنٹ
    private bool isMoving = false;        // کیا گوٹی چل رہی ہے؟

    /// <summary>True while this player's token is mid-move. Callers (e.g. the dice roller) should
    /// check this before starting another move so rolls can't overlap an in-progress walk.</summary>
    public bool IsMoving => isMoving;

    /// <summary>
    /// Instantly teleports to a world position and resets the internal waypoint index, skipping animation
    /// entirely - used to place a token at its starting yard slot, or to snap it back to base after a capture.
    /// Cancels any move currently in progress. <paramref name="waypointIndex"/> should mirror whatever indexing
    /// scheme the caller's <see cref="waypoints"/> array uses for "currently sitting here" (e.g. -1 for
    /// "before waypoints[0]", matching Ludo's in-base state); pass -1 if not tracking a path position.
    /// </summary>
    public void WarpToPosition(Vector3 worldPosition, int waypointIndex = -1)
    {
        StopAllCoroutines(); // cancel any in-flight MoveRoutine so it can't fight this teleport
        isMoving = false;
        currentWaypointIndex = waypointIndex;
        transform.position = worldPosition;
    }

    void Start()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
    }

    void Update()
    {
        if (animator == null) return; // guard: nothing to drive without an Animator

        // ٹیسٹنگ کے لیے کی بورڈ شارٹ کٹس - Death سے F, Attack سے Space, Victory سے V //
        if (Input.GetKeyDown(KeyCode.F)) PlayDeath();
        if (Input.GetKeyDown(KeyCode.Space)) PlayAttack();
        if (Input.GetKeyDown(KeyCode.V)) PlayVictory();

        // دبانے پر ڈائریکٹ واک ٹیسٹ - صرف اس وقت جب حقیقی MoveRoutine نہ چل رہا ہو //
        if (!isMoving)
        {
            animator.SetBool(ParamIsWalking, Input.GetKey(KeyCode.W));
        }
    }

    /// <summary>Plays the "sent home"/captured reaction. Wired by <see cref="LudoEmpire.Ludo.LudoBoardLogic"/>
    /// whenever this character's token gets captured by an opponent.</summary>
    public void PlayDeath() => PulseBool(ParamDeath);

    /// <summary>Plays a capture reaction. Wired by <see cref="LudoEmpire.Ludo.LudoBoardLogic"/> whenever
    /// this character's token captures an opponent's.</summary>
    public void PlayAttack() => PulseBool(ParamAttack);

    /// <summary>Plays the "reached home" celebration. Wired by <see cref="LudoEmpire.Ludo.LudoBoardLogic"/>
    /// whenever this character's token completes its journey.</summary>
    public void PlayVictory() => PulseBool(ParamVictory);

    /// <summary>
    /// Sets a bool parameter true, then automatically resets it false shortly after - the closest
    /// equivalent to a Mecanim Trigger for a controller whose Attack/Death/Victory parameters are all
    /// authored as plain Bools (as BattleCharacter_Ontroller's currently are). Safe to call with no
    /// Animator assigned or before any transition consumes the pulse actually exists in the graph yet.
    /// </summary>
    private void PulseBool(string paramName)
    {
        if (animator == null) return;
        animator.SetBool(paramName, true);
        StartCoroutine(ResetBoolAfterDelay(paramName, TriggerPulseDuration));
    }

    private IEnumerator ResetBoolAfterDelay(string paramName, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (animator != null) animator.SetBool(paramName, false);
    }

    /// <summary>Plays the footstep clip once, if both an AudioSource and clip are assigned. Called once
    /// per waypoint hop from <see cref="MoveRoutine"/> - a pure no-op placeholder otherwise, so movement
    /// never depends on audio being configured.</summary>
    private void PlayFootstep()
    {
        if (audioSource == null || footstepClip == null) return;
        audioSource.PlayOneShot(footstepClip, footstepVolume);
    }

    // ---------- گوٹی چلانے کا لاجک ---------- //

    // ڈائس کا نمبر پاس کر کے اس فنکشن کو کال کریں
    // onComplete (optional) فائر ہوتا ہے جب چلنا مکمل ہو جائے - ڈائس رولر اسے باری بدلنے کے لیے استعمال کرتا ہے
    public void MoveSteps(int steps, Action onComplete = null)
    {
        if (isMoving)
        {
            Debug.LogWarning($"[PlayerController] MoveSteps({steps}) ignored on '{name}': already moving.", this);
            return;
        }

        StartCoroutine(MoveRoutine(steps, onComplete));
    }

    private IEnumerator MoveRoutine(int steps, Action onComplete)
    {
        isMoving = true;

        if (waypoints == null || waypoints.Length == 0)
        {
            Debug.LogWarning($"[PlayerController] MoveRoutine on '{name}' has no waypoints assigned; skipping movement.", this);
            isMoving = false;
            onComplete?.Invoke();
            yield break;
        }

        if (animator != null)
        {
            animator.SetBool(ParamIsWalking, true); // چلاتے وقت Walk اینیمیشن آن
        }

        for (int i = 0; i < steps; i++)
        {
            // چیک کریں کہ گوٹی بورڈ کے آخری پوائنٹ سے آگے نہ نکلے
            if (currentWaypointIndex < waypoints.Length - 1)
            {
                currentWaypointIndex++;

                Transform waypoint = waypoints[currentWaypointIndex];
                if (waypoint == null)
                {
                    // Gap in an assembled path (e.g. an unassigned board waypoint) - the index still
                    // advances so future moves stay in sync, this hop just has nothing to walk to.
                    continue;
                }

                Vector3 targetPosition = waypoint.position;
                PlayFootstep();

                // کریکٹر کا رخ اگلی ٹائل کی طرف کریں
                Vector3 direction = (targetPosition - transform.position).normalized;
                if (direction != Vector3.zero)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(direction);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 10f * Time.deltaTime);
                }

                // اگلی ٹائل تک ہموار رفتاری سے پہنچیں
                while (Vector3.Distance(transform.position, targetPosition) > 0.05f)
                {
                    transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
                    yield return null;
                }

                transform.position = targetPosition; // پوزیشن پرفیکٹ سیٹ کریں
            }
        }

        if (animator != null)
        {
            animator.SetBool(ParamIsWalking, false); // رکنے پر Walk اینیمیشن بند
        }

        isMoving = false;
        onComplete?.Invoke();
    }
}