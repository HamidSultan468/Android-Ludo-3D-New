using System;
using System.Text;
using UnityEngine;

namespace LudoEmpire.Ludo
{
    /// <summary>Which physical board to build for the match.</summary>
    public enum LudoBoardType
    {
        /// <summary>The standard 15x15 Neon-Glow Ludo board (current default).</summary>
        ClassicLudo = 0,
        /// <summary>Same layout, "battlefield" theming/props. Geometry hook only for now.</summary>
        BattlefieldLudo = 1
    }

    /// <summary>Coarse preset used by the Dashboard / Mode screens. Maps onto the fuller
    /// <see cref="GameConfig"/> fields via <see cref="GameConfig.ApplyBoardMode"/>. Classic4P is a
    /// 1:1 match for the current shipped game.</summary>
    public enum GameBoardType
    {
        Classic4P = 0,
        Team2v2 = 1,
        Fast3P = 2
    }

    /// <summary>Which rule variation the board logic applies. Standard = classic Ludo, unchanged.</summary>
    public enum LudoRuleSet
    {
        /// <summary>Classic Ludo: 6 to leave base, exact count home, 3-sixes forfeit, extra turn on 6/capture.</summary>
        Standard = 0,
        /// <summary>Reserved for the Empire economy variant. Behaves as Standard until designed.</summary>
        Empire = 1,
        /// <summary>Quicker matches: one token already on the board at start, no 6 required to bring others out.</summary>
        FastMode = 2
    }

    /// <summary>How players are grouped. FreeForAll = classic Ludo (no teams), fully backward compatible.</summary>
    public enum LudoTeamMode
    {
        FreeForAll = 0,
        OneVsOne = 1,
        OneVsAI = 2,
        OneVsThree = 3,
        TwoVsTwo = 4
    }

    /// <summary>Who drives a colour slot.</summary>
    public enum LudoSlotController
    {
        Empty = 0,
        Human = 1,
        AI = 2
    }

    /// <summary>One colour seat in the match.</summary>
    [Serializable]
    public struct LudoPlayerSlot
    {
        public PlayerColor color;
        public LudoSlotController controller;
        public AIDifficulty aiDifficulty;
        [Tooltip("0 = no team (free-for-all). 1/2 = team id for TwoVsTwo etc.")]
        public int team;
        public string displayName;

        public bool IsActive => controller != LudoSlotController.Empty;
        public bool IsHuman => controller == LudoSlotController.Human;
        public bool IsAI => controller == LudoSlotController.AI;
    }

    /// <summary>
    /// Everything the board scene needs to set itself up, chosen on the Mode / Player-Select screens and
    /// handed across the scene load by <see cref="FlowManager"/>. Plain serializable data - no Unity
    /// object references - so it round-trips through JSON and survives a scene load intact.
    ///
    /// Reading it is additive: <see cref="LudoBoardLogic"/> and <see cref="LudoBoardSceneBuilder"/> only
    /// change behaviour for non-default values. A brand-new GameConfig (or none at all) reproduces the
    /// current 4-player Player-vs-AI Standard game exactly.
    /// </summary>
    [Serializable]
    public class GameConfig
    {
        public LudoBoardType boardType = LudoBoardType.ClassicLudo;
        public LudoRuleSet ruleSet = LudoRuleSet.Standard;

        /// <summary>Dashboard-facing coarse preset. Kept in sync with the detailed fields below by
        /// <see cref="ApplyBoardMode"/>.</summary>
        public GameBoardType boardMode = GameBoardType.Classic4P;

        [Range(2, 4)]
        public int playerCount = 4;

        public LudoTeamMode teamMode = LudoTeamMode.FreeForAll;

        /// <summary>True when the match groups players into teams (2v2). Read-only convenience.</summary>
        public bool IsTeamMode => teamMode == LudoTeamMode.TwoVsTwo;

        /// <summary>One entry per colour seat, in turn order. Length is always 4; inactive seats have
        /// <see cref="LudoSlotController.Empty"/>.</summary>
        public LudoPlayerSlot[] slots = DefaultSlots();

        // ---- derived helpers -------------------------------------------------------------------

        /// <summary>Colours that actually play, in turn order.</summary>
        public PlayerColor[] ActiveTurnOrder()
        {
            int n = 0;
            for (int i = 0; i < slots.Length; i++) if (slots[i].IsActive) n++;
            var result = new PlayerColor[Mathf.Max(1, n)];
            int w = 0;
            for (int i = 0; i < slots.Length && w < result.Length; i++)
                if (slots[i].IsActive) result[w++] = slots[i].color;
            if (w == 0) result[0] = PlayerColor.Red;
            return result;
        }

        public bool HasAnyAI()
        {
            for (int i = 0; i < slots.Length; i++) if (slots[i].IsAI) return true;
            return false;
        }

        public bool HasAnyHuman()
        {
            for (int i = 0; i < slots.Length; i++) if (slots[i].IsHuman) return true;
            return false;
        }

        /// <summary>Back-compat bridge for <see cref="LudoGameModeController"/>: PlayerVsAI if any seat is
        /// AI-controlled, otherwise PassAndPlay (local hotseat).</summary>
        public LudoGameMode LegacyGameMode => HasAnyAI() ? LudoGameMode.PlayerVsAI : LudoGameMode.PassAndPlay;

        /// <summary>The team id of a colour, or 0 for free-for-all.</summary>
        public int TeamOf(PlayerColor color)
        {
            for (int i = 0; i < slots.Length; i++)
                if (slots[i].color == color) return slots[i].team;
            return 0;
        }

        public LudoSlotController ControllerOf(PlayerColor color)
        {
            for (int i = 0; i < slots.Length; i++)
                if (slots[i].color == color) return slots[i].controller;
            return LudoSlotController.Empty;
        }

        public AIDifficulty DifficultyOf(PlayerColor color)
        {
            for (int i = 0; i < slots.Length; i++)
                if (slots[i].color == color) return slots[i].aiDifficulty;
            return AIDifficulty.Medium;
        }

        // ---- defaults / validation ----------------------------------------------------------

        public static LudoPlayerSlot[] DefaultSlots()
        {
            // Current shipped setup: Red = human, Green/Yellow/Blue = Medium AI.
            return new[]
            {
                new LudoPlayerSlot { color = PlayerColor.Red,    controller = LudoSlotController.Human, aiDifficulty = AIDifficulty.Medium, team = 0, displayName = "You" },
                new LudoPlayerSlot { color = PlayerColor.Green,  controller = LudoSlotController.AI,    aiDifficulty = AIDifficulty.Medium, team = 0, displayName = "Green" },
                new LudoPlayerSlot { color = PlayerColor.Yellow, controller = LudoSlotController.AI,    aiDifficulty = AIDifficulty.Medium, team = 0, displayName = "Yellow" },
                new LudoPlayerSlot { color = PlayerColor.Blue,   controller = LudoSlotController.AI,    aiDifficulty = AIDifficulty.Medium, team = 0, displayName = "Blue" },
            };
        }

        public static GameConfig Default() => new GameConfig();

        /// <summary>Expands the coarse <see cref="boardMode"/> preset into the detailed fields.
        /// Classic4P reproduces the current shipped game exactly.</summary>
        public GameConfig ApplyBoardMode(GameBoardType mode)
        {
            boardMode = mode;
            switch (mode)
            {
                case GameBoardType.Team2v2:
                    boardType = LudoBoardType.ClassicLudo;
                    ruleSet = LudoRuleSet.Standard;
                    playerCount = 4;
                    teamMode = LudoTeamMode.TwoVsTwo;
                    slots = DefaultSlots();
                    slots[0].team = 1; slots[2].team = 1; // Red + Yellow
                    slots[1].team = 2; slots[3].team = 2; // Green + Blue
                    break;

                case GameBoardType.Fast3P:
                    boardType = LudoBoardType.ClassicLudo;
                    ruleSet = LudoRuleSet.FastMode;
                    playerCount = 3;
                    teamMode = LudoTeamMode.FreeForAll;
                    slots = DefaultSlots();
                    slots[3].controller = LudoSlotController.Empty;
                    break;

                case GameBoardType.Classic4P:
                default:
                    boardType = LudoBoardType.ClassicLudo;
                    ruleSet = LudoRuleSet.Standard;
                    playerCount = 4;
                    teamMode = LudoTeamMode.FreeForAll;
                    slots = DefaultSlots();
                    break;
            }
            return Validated();
        }

        /// <summary>Clamps / repairs the config so the board scene can always trust it.</summary>
        public GameConfig Validated()
        {
            if (slots == null || slots.Length != 4) slots = DefaultSlots();

            int active = 0;
            for (int i = 0; i < slots.Length; i++) if (slots[i].IsActive) active++;

            playerCount = Mathf.Clamp(playerCount, 2, 4);

            // If the seat data disagrees with playerCount, trust playerCount: activate the first N,
            // empty the rest, keeping controller kind where already set.
            if (active != playerCount)
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    if (i < playerCount)
                    {
                        if (slots[i].controller == LudoSlotController.Empty)
                            slots[i].controller = (i == 0) ? LudoSlotController.Human : LudoSlotController.AI;
                    }
                    else
                    {
                        slots[i].controller = LudoSlotController.Empty;
                    }
                }
            }

            if (teamMode == LudoTeamMode.TwoVsTwo && playerCount != 4)
                teamMode = LudoTeamMode.FreeForAll;

            return this;
        }

        // ---- JSON round-trip (used by save/profile + optional transfer) ---------------------

        public string ToJson() => JsonUtility.ToJson(this);

        public static GameConfig FromJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return Default();
            try { return JsonUtility.FromJson<GameConfig>(json)?.Validated() ?? Default(); }
            catch { return Default(); }
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("GameConfig[").Append(boardType).Append(", ").Append(ruleSet)
              .Append(", ").Append(playerCount).Append("p, ").Append(teamMode).Append(" | ");
            for (int i = 0; i < slots.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(slots[i].color).Append(':').Append(slots[i].controller);
                if (slots[i].IsAI) sb.Append('(').Append(slots[i].aiDifficulty).Append(')');
                if (slots[i].team != 0) sb.Append(" T").Append(slots[i].team);
            }
            sb.Append(']');
            return sb.ToString();
        }
    }
}
