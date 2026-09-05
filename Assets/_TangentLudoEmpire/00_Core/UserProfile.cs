using System;
using System.Collections.Generic;
using UnityEngine;

namespace TangentLudoEmpire.Core
{
    /// <summary>A named integer counter (daily ad count, etc.). JsonUtility can't serialise a
    /// Dictionary, so counters live in a list.</summary>
    [Serializable]
    public struct CounterEntry
    {
        public string key;
        public int value;
    }

    /// <summary>
    /// The player's persisted account data. Plain <see cref="SerializableAttribute"/> so it round-trips
    /// through <see cref="JsonUtility"/> to a file in <c>Application.persistentDataPath</c>
    /// (see <see cref="SaveService"/>). Offline-first: <see cref="profileId"/> is a locally-minted GUID
    /// until/unless a real login is added later.
    ///
    /// PHASE 1 scope: coins mirror the existing <c>CurrencyManager</c> balance; diamonds are reserved.
    /// No server, no signing yet - that is a later Security phase.
    /// </summary>
    [Serializable]
    public class UserProfile
    {
        public int schemaVersion = 1;

        public string profileId = "";
        public string displayName = AppConstants.DEFAULT_PROFILE_NAME;

        public long coins = 0;
        public int diamonds = 0;

        /// <summary>ISO-8601 UTC. Set once on first creation.</summary>
        public string createdAt = "";
        /// <summary>ISO-8601 UTC. Bumped on every save.</summary>
        public string updatedAt = "";

        /// <summary>Carried over from the old <c>games_won</c> PlayerPrefs key during migration.</summary>
        public int gamesWon = 0;

        /// <summary>Server-issued account role name (Player/Pro/Staff/Manager/CEO). Client-side value is
        /// advisory only - RBAC re-reads it from the backend, never trusts the save file.</summary>
        public string role = "Player";

        /// <summary>Named counters (e.g. <c>adcount_20260904</c>). Managed via
        /// <see cref="GetCounter"/>/<see cref="SetCounter"/>.</summary>
        public List<CounterEntry> counters = new List<CounterEntry>();

        public int GetCounter(string key)
        {
            if (counters == null) return 0;
            for (int i = 0; i < counters.Count; i++)
                if (counters[i].key == key) return counters[i].value;
            return 0;
        }

        public void SetCounter(string key, int value)
        {
            counters ??= new List<CounterEntry>();
            for (int i = 0; i < counters.Count; i++)
                if (counters[i].key == key) { counters[i] = new CounterEntry { key = key, value = value }; return; }
            counters.Add(new CounterEntry { key = key, value = value });
        }

        public static UserProfile CreateNew()
        {
            string now = DateTime.UtcNow.ToString("o");
            return new UserProfile
            {
                schemaVersion = 1,
                profileId = Guid.NewGuid().ToString("N"),
                displayName = AppConstants.DEFAULT_PROFILE_NAME,
                coins = 0,
                diamonds = 0,
                createdAt = now,
                updatedAt = now,
                gamesWon = 0
            };
        }

        /// <summary>Fixes anything a bad/old JSON file could contain so callers can always trust it.</summary>
        public UserProfile Validated()
        {
            if (string.IsNullOrEmpty(profileId)) profileId = Guid.NewGuid().ToString("N");
            if (string.IsNullOrEmpty(displayName)) displayName = AppConstants.DEFAULT_PROFILE_NAME;
            if (coins < 0) coins = 0;
            if (diamonds < 0) diamonds = 0;
            if (gamesWon < 0) gamesWon = 0;
            if (string.IsNullOrEmpty(createdAt)) createdAt = DateTime.UtcNow.ToString("o");
            return this;
        }

        public string ToJson() => JsonUtility.ToJson(this, prettyPrint: true);

        public static UserProfile FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try { return JsonUtility.FromJson<UserProfile>(json)?.Validated(); }
            catch (Exception e) { Debug.LogWarning($"[UserProfile] FromJson failed: {e.Message}"); return null; }
        }
    }
}
