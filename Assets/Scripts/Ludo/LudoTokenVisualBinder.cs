using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LudoEmpire.Ludo
{
    /// <summary>
    /// Runtime companion dropped in by the scene builder: binds each spawned goti's Transform to
    /// its logical <see cref="LudoBoardLogic"/> token so moves animate automatically. Compiled into
    /// player builds (unlike the editor tool below) - safe to ignore or remove if you bind visuals yourself.
    /// </summary>
    public class LudoTokenVisualBinder : MonoBehaviour
    {
        [Serializable]
        public class Binding
        {
            public PlayerColor color;
            public int tokenId;
            public Transform visual;
        }

        [SerializeField] private LudoBoardLogic board;
        [SerializeField] private List<Binding> bindings = new List<Binding>();

        public void Configure(LudoBoardLogic targetBoard, List<Binding> tokenBindings)
        {
            board = targetBoard;
            bindings = tokenBindings ?? new List<Binding>();
        }

        private void Start()
        {
            if (board == null)
            {
                Debug.LogWarning("[LudoTokenVisualBinder] No board assigned; goti visuals will not animate.", this);
                return;
            }

            foreach (var binding in bindings)
            {
                if (binding == null || binding.visual == null) continue;
                board.SetTokenVisual(binding.color, binding.tokenId, binding.visual);
            }
        }
    }
}
