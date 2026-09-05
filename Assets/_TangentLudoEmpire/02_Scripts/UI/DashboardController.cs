using UnityEngine;
using UnityEngine.UI;
using LudoEmpire.Ludo; // FlowManager, GameConfig, FlowScreen

namespace TangentLudoEmpire.Core
{
    /// <summary>
    /// Runs the Dashboard scene: spawns the 3x3 button grid from a <see cref="DashboardButtonData"/>
    /// asset and wires each button's tap to a <see cref="DashboardAction"/>. No scene names are typed
    /// here - navigation goes through <see cref="FlowManager"/>, "Coming Soon" goes through
    /// <see cref="ToastManager"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class DashboardController : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private DashboardButtonData buttonData;

        [Header("Scene refs (filled by DashboardSceneBuilder)")]
        [SerializeField] private RectTransform gridParent;
        [SerializeField] private Text coinsLabel;

        [Header("Style")]
        [SerializeField] private Color buttonColor = new Color(0.20f, 0.60f, 0.90f);   // #3399E5
        [SerializeField] private Color buttonTextColor = Color.white;
        [SerializeField] private Vector2 buttonSize = new Vector2(300f, 300f);
        [SerializeField] private float buttonSpacing = 24f;

        private void Start()
        {
            BuildGrid();
            RefreshCoins();
        }

        private void OnEnable()
        {
            if (SaveService.Instance != null) SaveService.Instance.OnProfileChanged += HandleProfileChanged;
        }

        private void OnDisable()
        {
            if (SaveService.Instance != null) SaveService.Instance.OnProfileChanged -= HandleProfileChanged;
        }

        private void HandleProfileChanged(UserProfile _) => RefreshCoins();

        private void RefreshCoins()
        {
            if (coinsLabel == null) return;
            long coins = WalletManager.Instance != null ? WalletManager.Instance.GetCoins()
                       : (SaveService.Instance != null ? SaveService.Instance.Profile.coins : 0);
            coinsLabel.text = $"{coins:N0}";
        }

        private void BuildGrid()
        {
            if (gridParent == null) { Debug.LogWarning("[DashboardController] No gridParent assigned."); return; }

            var entries = (buttonData != null && buttonData.buttons != null && buttonData.buttons.Count > 0)
                ? buttonData.buttons
                : DashboardButtonData.DefaultSet();

            // Ensure a GridLayoutGroup on the parent for tidy 3-wide layout.
            var grid = gridParent.GetComponent<GridLayoutGroup>();
            if (grid == null) grid = gridParent.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = buttonSize;
            grid.spacing = new Vector2(buttonSpacing, buttonSpacing);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            grid.childAlignment = TextAnchor.MiddleCenter;

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var go = new GameObject($"Btn_{entry.id}", typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(gridParent, false);
                go.GetComponent<Image>().color = buttonColor;

                var labelGO = new GameObject("Label", typeof(RectTransform), typeof(Text));
                labelGO.transform.SetParent(go.transform, false);
                var lrt = labelGO.GetComponent<RectTransform>();
                lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.sizeDelta = Vector2.zero;
                var txt = labelGO.GetComponent<Text>();
                txt.text = entry.label;
                txt.alignment = TextAnchor.MiddleCenter;
                txt.color = buttonTextColor;
                txt.fontSize = 40;
                txt.font = font;
                txt.horizontalOverflow = HorizontalWrapMode.Wrap;

                var captured = entry; // avoid closure-over-loop-var
                go.GetComponent<Button>().onClick.AddListener(() => OnButton(captured));
            }
        }

        private void OnButton(DashboardButtonEntry entry)
        {
            Debug.Log($"[DashboardController] Button '{entry.id}' -> {entry.action}");
            switch (entry.action)
            {
                case DashboardAction.StartLudoMatch:
                    FlowManager.Instance.StartMatch(GameConfig.Default());
                    break;

                case DashboardAction.GoToScreen:
                    if (entry.targetScreen != FlowScreen.None) FlowManager.Instance.Go(entry.targetScreen);
                    else ToastManager.Show("Coming Soon");
                    break;

                case DashboardAction.QuitApp:
                    Application.Quit();
                    break;

                case DashboardAction.ComingSoon:
                default:
                    ToastManager.Show(string.IsNullOrEmpty(entry.comingSoonText) ? "Coming Soon" : entry.comingSoonText);
                    break;
            }
        }

        /// <summary>Called by DashboardSceneBuilder right after AddComponent.</summary>
        public void Configure(DashboardButtonData data, RectTransform grid, Text coins)
        {
            buttonData = data;
            gridParent = grid;
            coinsLabel = coins;
        }
    }
}
