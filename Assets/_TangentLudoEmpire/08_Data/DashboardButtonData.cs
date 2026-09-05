using System;
using System.Collections.Generic;
using UnityEngine;
using LudoEmpire.Ludo; // FlowScreen

namespace TangentLudoEmpire.Core
{
    /// <summary>What a Dashboard grid button does when tapped.</summary>
    public enum DashboardAction
    {
        /// <summary>Show a "Coming Soon" toast (Phase 1 default for unfinished features).</summary>
        ComingSoon = 0,
        /// <summary>Navigate via FlowManager to <see cref="DashboardButtonEntry.targetScreen"/>.</summary>
        GoToScreen = 1,
        /// <summary>Start a Ludo match with the default config (equivalent to today's "Play vs AI").</summary>
        StartLudoMatch = 2,
        /// <summary>Quit the application.</summary>
        QuitApp = 3
    }

    [Serializable]
    public class DashboardButtonEntry
    {
        public string id = "ludo";
        public string label = "Ludo";
        [Tooltip("Optional icon sprite; may be null in Phase 1.")]
        public Sprite icon;
        public DashboardAction action = DashboardAction.ComingSoon;
        [Tooltip("Used when Action = GoToScreen.")]
        public FlowScreen targetScreen = FlowScreen.None;
        [Tooltip("Toast text when Action = ComingSoon.")]
        public string comingSoonText = "Coming Soon";
    }

    /// <summary>
    /// The 3x3 Dashboard button grid, authored as data so the layout isn't baked into a scene and no
    /// button hardcodes a scene name. Create one via
    /// <c>Assets &gt; Create &gt; Tangent &gt; Dashboard Button Data</c> and point
    /// <c>DashboardSceneBuilder</c> at it.
    /// </summary>
    [CreateAssetMenu(fileName = "DashboardButtons", menuName = "Tangent Ludo Empire/Dashboard Button Data", order = 0)]
    public class DashboardButtonData : ScriptableObject
    {
        [Tooltip("Rendered left-to-right, top-to-bottom into a 3x3 grid.")]
        public List<DashboardButtonEntry> buttons = new List<DashboardButtonEntry>();

        /// <summary>The Phase 1 default set: Ludo is live, everything else toasts "Coming Soon".</summary>
        public static List<DashboardButtonEntry> DefaultSet()
        {
            return new List<DashboardButtonEntry>
            {
                new DashboardButtonEntry { id = "ludo",        label = "Ludo",                       action = DashboardAction.StartLudoMatch },
                new DashboardButtonEntry { id = "killabandar", label = "Killa Bandar",               action = DashboardAction.ComingSoon },
                new DashboardButtonEntry { id = "gullidanda",  label = "Gulli Danda",                action = DashboardAction.ComingSoon },
                new DashboardButtonEntry { id = "tournament",  label = "Tournament",                 action = DashboardAction.ComingSoon },
                new DashboardButtonEntry { id = "settings",    label = "Settings",                   action = DashboardAction.ComingSoon },
                new DashboardButtonEntry { id = "language",    label = "Language",                   action = DashboardAction.ComingSoon },
                new DashboardButtonEntry { id = "base",        label = "Base",                       action = DashboardAction.ComingSoon },
                new DashboardButtonEntry { id = "links",       label = "Links",                      action = DashboardAction.ComingSoon },
                new DashboardButtonEntry { id = "exit",        label = "Exit",                       action = DashboardAction.QuitApp },
            };
        }
    }
}
