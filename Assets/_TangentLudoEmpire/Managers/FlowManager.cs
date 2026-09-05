using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LudoEmpire.Ludo
{
    /// <summary>
    /// The single navigation + data-hand-off hub for the whole game (the "UIManager" from the design
    /// doc). Persists across scene loads, owns the routing table (<see cref="FlowScreen"/> -&gt; panel or
    /// scene), the back-stack for "return to where I came from" (minigames, base), and the
    /// <see cref="GameConfig"/> that the board scene reads on Start.
    ///
    /// Buttons never name scenes: they call <see cref="Go"/> / <see cref="Back"/> /
    /// <see cref="StartMatch"/> / <see cref="LaunchMinigame"/> with a <see cref="FlowScreen"/> value.
    ///
    /// Bootstraps itself on first access or at startup, so it works even if no FlowManager object was
    /// placed in the opening scene. Drop a "FlowManager" prefab in Resources/ (or in the MainMenu scene)
    /// to customise the routing table in the Inspector; otherwise a sensible default map is used.
    ///
    /// Mobile: event-driven, no Update loop, scene loads are async and timeScale-safe.
    /// </summary>
    [DisallowMultipleComponent]
    public class FlowManager : MonoBehaviour
    {
        // ---- singleton ---------------------------------------------------------------------

        private static FlowManager _instance;

        public static FlowManager Instance
        {
            get
            {
                if (_instance == null) Bootstrap();
                return _instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;

            _instance = FindAnyObjectByType<FlowManager>();
            if (_instance != null) { _instance.EnsureInit(); return; }

            var prefab = Resources.Load<FlowManager>("FlowManager");
            _instance = prefab != null ? Instantiate(prefab) : new GameObject("FlowManager").AddComponent<FlowManager>();
            _instance.name = "FlowManager";
            _instance.EnsureInit();
        }

        // ---- routing table ---------------------------------------------------------------

        [Header("Routing (Screen -> panel or scene). Empty = use the built-in default map.")]
        [SerializeField] private FlowSceneEntry[] routes = Array.Empty<FlowSceneEntry>();

        [Header("Transitions")]
        [Tooltip("Seconds the fade/slide covers a scene swap. UI listens to OnScreenChanging/Changed.")]
        [SerializeField] private float sceneTransitionSeconds = 0.25f;

        private readonly Dictionary<FlowScreen, FlowSceneEntry> _map = new Dictionary<FlowScreen, FlowSceneEntry>();
        private readonly Dictionary<string, FlowScreenPanel> _panels = new Dictionary<string, FlowScreenPanel>();
        private readonly Stack<FlowScreen> _history = new Stack<FlowScreen>();

        private bool _initialised;
        private bool _busy;

        /// <summary>The currently shown screen (panel or scene).</summary>
        public FlowScreen Current { get; private set; } = FlowScreen.None;

        /// <summary>Config handed to the board scene by <see cref="StartMatch"/>. Never null.</summary>
        public GameConfig PendingConfig { get; private set; } = GameConfig.Default();

        /// <summary>Raised just before a screen change (old screen). UI can start slide-out / fade-in.</summary>
        public event Action<FlowScreen> OnScreenChanging;
        /// <summary>Raised once the new screen is active. UI can slide-in.</summary>
        public event Action<FlowScreen> OnScreenChanged;

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureInit();
        }

        private void EnsureInit()
        {
            if (_initialised) return;
            _initialised = true;

            _map.Clear();
            foreach (var e in BuiltInRoutes()) _map[e.screen] = e;   // defaults first
            if (routes != null)
                foreach (var e in routes) if (e.screen != FlowScreen.None) _map[e.screen] = e; // Inspector overrides

            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDestroy()
        {
            if (_instance == this) SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        /// <summary>Built-in default routing so the game runs without any Inspector setup. Panel ids
        /// default to the screen name; scene names match the project's Build Settings.</summary>
        private static IEnumerable<FlowSceneEntry> BuiltInRoutes()
        {
            yield return Scene(FlowScreen.MainMenu, "MainMenu-new");
            yield return Scene(FlowScreen.Dashboard, "Dashboard");
            yield return Panel(FlowScreen.ModeSelect);
            yield return Panel(FlowScreen.PlayerSelect);
            yield return Panel(FlowScreen.Settings);

            yield return Scene(FlowScreen.Board, "SampleScene");
            yield return Scene(FlowScreen.PlayerBase, "PlayerBaseScene");

            yield return Scene(FlowScreen.Minigame_ThirdPerson, "ThirdPersonGameScene");
            yield return Scene(FlowScreen.Minigame_GulliDanda, "GulliDandaScene");
            yield return Scene(FlowScreen.Minigame_KillaBandar, "KillaBandarScene");
            yield return Scene(FlowScreen.Minigame_Empire, "EmpireScene");
        }

        private static FlowSceneEntry Panel(FlowScreen s) => new FlowSceneEntry { screen = s, kind = FlowScreenKind.Panel, panelId = s.ToString() };
        private static FlowSceneEntry Scene(FlowScreen s, string sceneName) => new FlowSceneEntry { screen = s, kind = FlowScreenKind.Scene, sceneName = sceneName };

        // ---- panel registration (called by FlowScreenPanel) -----------------------------

        internal void RegisterPanel(FlowScreenPanel panel)
        {
            if (panel == null || string.IsNullOrEmpty(panel.PanelId)) return;
            _panels[panel.PanelId] = panel;
        }

        internal void UnregisterPanel(FlowScreenPanel panel)
        {
            if (panel == null) return;
            if (_panels.TryGetValue(panel.PanelId, out var p) && p == panel) _panels.Remove(panel.PanelId);
        }

        // ---- navigation API -------------------------------------------------------------

        /// <summary>Go to a screen, remembering the current one for <see cref="Back"/>.</summary>
        public void Go(FlowScreen screen) => Navigate(screen, pushHistory: true);

        /// <summary>Alias for <see cref="Go"/> (matches the "GoTo(...)" call style used at boot).</summary>
        public void GoTo(FlowScreen screen) => Navigate(screen, pushHistory: true);

        /// <summary>Go to a screen without adding it to the back-stack (e.g. a replace).</summary>
        public void GoNoHistory(FlowScreen screen) => Navigate(screen, pushHistory: false);

        /// <summary>Return to the previous screen. Falls back to MainMenu if the stack is empty.</summary>
        public void Back()
        {
            FlowScreen target = _history.Count > 0 ? _history.Pop() : FlowScreen.MainMenu;
            Navigate(target, pushHistory: false);
        }

        /// <summary>Sets the match config and loads the board scene. Also updates the legacy
        /// <see cref="LudoGameModeSelection"/> so existing scene wiring keeps working.</summary>
        /// <summary>Static mirror of the last config passed to <see cref="StartMatch"/>. The board scene
        /// can read this even if it somehow resolves a different FlowManager instance. Falls back to
        /// <see cref="GameConfig.Default"/> so opening SampleScene directly still works.</summary>
        public static GameConfig ActiveMatchConfig { get; private set; } = GameConfig.Default();

        public void StartMatch(GameConfig config)
        {
            PendingConfig = (config ?? GameConfig.Default()).Validated();
            ActiveMatchConfig = PendingConfig;
            LudoGameModeSelection.Select(PendingConfig.LegacyGameMode);
            Go(FlowScreen.Board);
        }

        /// <summary>Launches a minigame scene, remembering where to come back to.</summary>
        public void LaunchMinigame(FlowScreen minigame)
        {
            if (minigame < FlowScreen.Minigame_ThirdPerson)
            {
                Debug.LogWarning($"[FlowManager] {minigame} is not a minigame screen.");
                return;
            }
            Go(minigame);
        }

        /// <summary>Return from a minigame / the board / the base to wherever we came from
        /// (usually the Dashboard or MainMenu).</summary>
        public void ReturnToHub() => Back();

        // ---- core navigation ----------------------------------------------------------

        private void Navigate(FlowScreen screen, bool pushHistory)
        {
            if (screen == FlowScreen.None || _busy) return;

            if (!_map.TryGetValue(screen, out FlowSceneEntry entry))
            {
                Debug.LogError($"[FlowManager] No route registered for {screen}.");
                return;
            }

            if (pushHistory && Current != FlowScreen.None && Current != screen)
                _history.Push(Current);

            OnScreenChanging?.Invoke(Current);

            if (entry.kind == FlowScreenKind.Scene)
            {
                StartCoroutine(LoadSceneRoutine(screen, entry.sceneName));
            }
            else
            {
                ShowPanel(screen, entry);
            }
        }

        private void ShowPanel(FlowScreen screen, FlowSceneEntry entry)
        {
            string id = string.IsNullOrEmpty(entry.panelId) ? screen.ToString() : entry.panelId;

            foreach (var kv in _panels)
            {
                bool show = kv.Key == id;
                if (kv.Value != null) kv.Value.SetShown(show);
            }

            Current = screen;
            OnScreenChanged?.Invoke(screen);
        }

        private IEnumerator LoadSceneRoutine(FlowScreen screen, string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError($"[FlowManager] {screen} has no scene name configured.");
                yield break;
            }

            if (!IsSceneInBuildSettings(sceneName))
            {
                Debug.LogError($"[FlowManager] Scene '{sceneName}' for {screen} is not in Build Settings - " +
                               "add it (File > Build Settings) or fix the route. Navigation aborted.");
                yield break;
            }

            _busy = true;

            // Give UI a moment to run its fade/slide-out before the hitch of activation.
            if (sceneTransitionSeconds > 0f)
                yield return new WaitForSecondsRealtime(sceneTransitionSeconds);

            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (op == null) { _busy = false; yield break; }

            while (!op.isDone) yield return null;

            // Old-scene panels have already removed themselves via FlowScreenPanel.OnDisable during the
            // unload; new-scene panels register via OnEnable. Nothing to clear here.
            Current = screen;
            _busy = false;
            OnScreenChanged?.Invoke(screen);
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Panels re-register themselves in the new scene via FlowScreenPanel.OnEnable.
            // If the loaded scene is a routed one and no explicit navigation drove it (e.g. Play in
            // Editor straight into it), reflect that as Current so Back() has somewhere to go.
            if (Current == FlowScreen.None || !SceneMatchesCurrent(scene.name))
            {
                foreach (var kv in _map)
                {
                    if (kv.Value.kind == FlowScreenKind.Scene &&
                        string.Equals(kv.Value.sceneName, scene.name, StringComparison.OrdinalIgnoreCase))
                    {
                        Current = kv.Key;
                        break;
                    }
                }
            }
        }

        private bool SceneMatchesCurrent(string sceneName)
        {
            return _map.TryGetValue(Current, out var e) && e.kind == FlowScreenKind.Scene &&
                   string.Equals(e.sceneName, sceneName, StringComparison.OrdinalIgnoreCase);
        }

        // ---- helpers ---------------------------------------------------------------

        /// <summary>Route lookup for tools / editor code.</summary>
        public bool TryGetRoute(FlowScreen screen, out FlowSceneEntry entry) => _map.TryGetValue(screen, out entry);

        /// <summary>Overrides one route at runtime (rarely needed - prefer the Inspector list).</summary>
        public void SetRoute(FlowSceneEntry entry)
        {
            if (entry.screen == FlowScreen.None) return;
            EnsureInit();
            _map[entry.screen] = entry;
        }

        private static bool IsSceneInBuildSettings(string sceneName)
        {
            int count = SceneManager.sceneCountInBuildSettings;
            for (int i = 0; i < count; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                if (string.Equals(System.IO.Path.GetFileNameWithoutExtension(path), sceneName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}
