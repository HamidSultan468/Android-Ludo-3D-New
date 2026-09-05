using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace TangentLudoEmpire.Core
{
    /// <summary>Ticks a <see cref="Text"/> once per second with the current time - either device-local or
    /// a fixed UTC offset (e.g. +5 for PKT). Coroutine-driven, no per-frame Update.</summary>
    [RequireComponent(typeof(Text))]
    public class ClockWidget : MonoBehaviour
    {
        [SerializeField] private string prefix = "";
        [SerializeField] private string timeFormat = "HH:mm";
        [Tooltip("ON = use the fixed offset below; OFF = device local time.")]
        [SerializeField] private bool useFixedUtcOffset = false;
        [SerializeField] private float utcOffsetHours = 5f; // PKT

        private Text _text;

        private void Awake() => _text = GetComponent<Text>();
        private void OnEnable() => StartCoroutine(Tick());

        private IEnumerator Tick()
        {
            var wait = new WaitForSecondsRealtime(1f);
            while (true)
            {
                DateTime now = useFixedUtcOffset
                    ? DateTime.UtcNow.AddHours(utcOffsetHours)
                    : DateTime.Now;
                _text.text = string.IsNullOrEmpty(prefix) ? now.ToString(timeFormat) : $"{prefix} {now.ToString(timeFormat)}";
                yield return wait;
            }
        }

        /// <summary>Used by the scene builder to configure a spawned clock.</summary>
        public void Configure(string labelPrefix, bool fixedOffset, float offsetHours)
        {
            prefix = labelPrefix;
            useFixedUtcOffset = fixedOffset;
            utcOffsetHours = offsetHours;
        }
    }
}
