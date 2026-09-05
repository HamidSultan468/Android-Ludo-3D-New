using System;
using UnityEngine;

namespace LudoEmpire.Ludo
{
    /// <summary>Every navigable destination in the game flow. Panels live inside a scene and are
    /// shown/hidden; scenes are loaded. Which is which is decided by the <see cref="FlowSceneEntry"/>
    /// map on <see cref="FlowManager"/> - never hardcoded on a button.</summary>
    public enum FlowScreen
    {
        None = 0,

        // Front-end (typically panels inside the MainMenu scene)
        MainMenu = 1,
        Dashboard = 2,       // Minigames hub
        ModeSelect = 3,      // board type / rule set / game mode
        PlayerSelect = 4,    // player count / team mode / per-slot colour + AI difficulty
        Settings = 5,

        // Scenes
        Board = 20,          // the Ludo match (SampleScene)
        PlayerBase = 21,     // per-profile base / factories

        // Minigame scenes
        Minigame_ThirdPerson = 40,
        Minigame_GulliDanda = 41,
        Minigame_KillaBandar = 42,
        Minigame_Empire = 43,
    }

    /// <summary>How a <see cref="FlowScreen"/> is reached.</summary>
    public enum FlowScreenKind
    {
        /// <summary>A panel GameObject inside the current scene, toggled active/inactive.</summary>
        Panel = 0,
        /// <summary>A separate Unity scene, loaded via SceneManager.</summary>
        Scene = 1
    }

    /// <summary>One row of <see cref="FlowManager"/>'s routing table: maps a <see cref="FlowScreen"/> to a
    /// panel or a scene name. Editable in the Inspector so nothing else has to know scene names.</summary>
    [Serializable]
    public struct FlowSceneEntry
    {
        public FlowScreen screen;
        public FlowScreenKind kind;

        [Tooltip("Scene name (exactly as in Build Settings) when Kind = Scene. Ignored for panels.")]
        public string sceneName;

        [Tooltip("For panels: an optional explicit panel id. Blank = match the FlowScreen name.")]
        public string panelId;
    }
}
