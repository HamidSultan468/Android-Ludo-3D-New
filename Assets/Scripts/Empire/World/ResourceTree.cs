using UnityEngine;

namespace LudoGame.Empire
{
    /// <summary>
    /// A choppable tree. Call Chop() once per chop action (a tap via
    /// TreeChopInput) - after enough chops it fells, granting Wood (or
    /// whatever material is assigned) to EmpireManager's inventory.
    ///
    /// Named "ResourceTree" (not "Tree") because Unity has its own built-in
    /// "Tree" component (for terrain trees) - a script sharing that exact
    /// name breaks AddComponent/GetComponent, even across namespaces (Unity
    /// warns: "Script 'Tree' has the same name as built-in Unity component").
    ///
    /// Setup: put this on a tree model with a Collider (needed for
    /// TreeChopInput's tap raycast), and assign the Wood MaterialDefinition.
    /// </summary>
    public class ResourceTree : MonoBehaviour
    {
        [SerializeField] private MaterialDefinition material;
        [SerializeField] private int hitsToFell = 3;
        [SerializeField] private int yieldPerFell = 4;

        private int hitsRemaining;

        public bool IsFelled { get; private set; }

        /// <summary>Raised when this tree is fully chopped down, with the amount of material granted.</summary>
        public event System.Action<ResourceTree, int> OnFelled;

        private void Awake()
        {
            hitsRemaining = Mathf.Max(1, hitsToFell);
        }

        /// <summary>Call once per chop action. Returns true if this chop felled the tree.</summary>
        public bool Chop()
        {
            if (IsFelled) return false;

            hitsRemaining--;
            if (hitsRemaining > 0) return false;

            IsFelled = true;

            float multiplier = EmpireManager.Instance != null ? EmpireManager.Instance.GetGatherMultiplier() : 1f;
            int yield = Mathf.RoundToInt(yieldPerFell * multiplier);

            EmpireManager.Instance?.Inventory.Add(material, yield);
            OnFelled?.Invoke(this, yield);

            gameObject.SetActive(false); // simple "chopped down" feedback - swap for an animation/VFX later
            return true;
        }
    }
}
