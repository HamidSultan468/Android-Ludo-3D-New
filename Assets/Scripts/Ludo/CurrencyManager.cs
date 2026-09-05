using System;
using System.Collections.Generic;
using UnityEngine;

namespace LudoEmpire.Ludo
{
    /// <summary>Pluggable persistence backend so the currency store can move from PlayerPrefs to a real DB later.</summary>
    public interface ICurrencyStorage
    {
        long LoadCoins();
        void SaveCoins(long amount);
    }

    /// <summary>
    /// Default storage backend. PlayerPrefs has no native 64-bit integer type, so the balance
    /// is persisted as a string and parsed back defensively.
    /// </summary>
    public class PlayerPrefsCurrencyStorage : ICurrencyStorage
    {
        private const string CoinsKey = "LudoEmpire_Coins";

        public long LoadCoins()
        {
            string raw = PlayerPrefs.GetString(CoinsKey, "0");
            return long.TryParse(raw, out long value) ? Math.Max(0, value) : 0;
        }

        public void SaveCoins(long amount)
        {
            PlayerPrefs.SetString(CoinsKey, amount.ToString());
            PlayerPrefs.Save();
        }
    }

#if LUDO_EMPIRE_USE_SQLITE
    /// <summary>
    /// Optional SQLite-backed storage. Only compiled when LUDO_EMPIRE_USE_SQLITE is defined
    /// in Project Settings > Player > Scripting Define Symbols AND a Mono.Data.Sqlite (or
    /// equivalent) plugin has been added to the project - it is not required out of the box.
    /// </summary>
    public class SQLiteCurrencyStorage : ICurrencyStorage
    {
        private readonly string _connectionString;

        public SQLiteCurrencyStorage()
        {
            string dbPath = System.IO.Path.Combine(Application.persistentDataPath, "ludo_empire_currency.db");
            _connectionString = $"URI=file:{dbPath}";
            EnsureSchema();
        }

        private void EnsureSchema()
        {
            using (var connection = new Mono.Data.Sqlite.SqliteConnection(_connectionString))
            {
                connection.Open();
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "CREATE TABLE IF NOT EXISTS currency (id INTEGER PRIMARY KEY, coins INTEGER NOT NULL);";
                    cmd.ExecuteNonQuery();
                    cmd.CommandText = "INSERT OR IGNORE INTO currency (id, coins) VALUES (1, 0);";
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public long LoadCoins()
        {
            using (var connection = new Mono.Data.Sqlite.SqliteConnection(_connectionString))
            {
                connection.Open();
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT coins FROM currency WHERE id = 1;";
                    object result = cmd.ExecuteScalar();
                    return result != null ? Convert.ToInt64(result) : 0L;
                }
            }
        }

        public void SaveCoins(long amount)
        {
            using (var connection = new Mono.Data.Sqlite.SqliteConnection(_connectionString))
            {
                connection.Open();
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "UPDATE currency SET coins = @coins WHERE id = 1;";
                    cmd.Parameters.Add(new Mono.Data.Sqlite.SqliteParameter("@coins", amount));
                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
#endif

    /// <summary>Which persistence backend the manager should use.</summary>
    public enum CurrencyStorageBackend
    {
        PlayerPrefs,
        SQLite
    }

    /// <summary>
    /// Single source of truth for soft-currency (coins) shared across Ludo matches, the Jungle
    /// Tycoon upgrade tree, and mini-games. Persists immediately on every change and exposes
    /// events so any scene's HUD can stay in sync without polling.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class CurrencyManager : MonoBehaviour
    {
        [Header("Storage")]
        [SerializeField] private CurrencyStorageBackend storageBackend = CurrencyStorageBackend.PlayerPrefs;

        [Header("Debug")]
        [SerializeField] private int recentTransactionLogCapacity = 50;

        public static CurrencyManager Instance { get; private set; }

        /// <summary>Fired any time the balance changes, with the new total.</summary>
        public event Action<long> OnCoinsChanged;
        /// <summary>Fired when coins are earned: (amount, source tag).</summary>
        public event Action<long, string> OnCoinsEarned;
        /// <summary>Fired when coins are spent: (amount, reason tag).</summary>
        public event Action<long, string> OnCoinsSpent;
        /// <summary>Fired when a spend was attempted but the balance was insufficient.</summary>
        public event Action<long, long> OnInsufficientFunds; // (requested, available)

        private ICurrencyStorage _storage;
        private long _coins;
        private readonly List<string> _recentTransactions = new List<string>();

        public long Coins => _coins;
        public IReadOnlyList<string> RecentTransactions => _recentTransactions;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            _storage = CreateStorage(storageBackend);
            LoadBalance();
        }

        private static ICurrencyStorage CreateStorage(CurrencyStorageBackend backend)
        {
            if (backend == CurrencyStorageBackend.SQLite)
            {
#if LUDO_EMPIRE_USE_SQLITE
                return new SQLiteCurrencyStorage();
#else
                Debug.LogWarning("[CurrencyManager] SQLite backend requested but LUDO_EMPIRE_USE_SQLITE is not defined " +
                                  "(or no SQLite plugin is installed). Falling back to PlayerPrefs.");
                return new PlayerPrefsCurrencyStorage();
#endif
            }
            return new PlayerPrefsCurrencyStorage();
        }

        private void LoadBalance()
        {
            _coins = _storage != null ? Math.Max(0, _storage.LoadCoins()) : 0;
            OnCoinsChanged?.Invoke(_coins);
        }

        /// <summary>Adds coins to the balance. No-op (with a warning) for non-positive amounts.</summary>
        public void AddCoins(long amount, string source = "unknown")
        {
            if (amount <= 0)
            {
                Debug.LogWarning($"[CurrencyManager] AddCoins ignored non-positive amount ({amount}) from '{source}'.", this);
                return;
            }

            _coins += amount;
            Persist();
            LogTransaction($"+{amount} ({source})");

            OnCoinsEarned?.Invoke(amount, source);
            OnCoinsChanged?.Invoke(_coins);
        }

        /// <summary>Attempts to spend coins. Returns false (without changing the balance) if funds are insufficient.</summary>
        public bool TrySpendCoins(long amount, string reason = "unknown")
        {
            if (amount <= 0)
            {
                Debug.LogWarning($"[CurrencyManager] TrySpendCoins ignored non-positive amount ({amount}) for '{reason}'.", this);
                return false;
            }

            if (_coins < amount)
            {
                Debug.LogWarning($"[CurrencyManager] Insufficient funds for '{reason}': requested {amount}, have {_coins}.", this);
                OnInsufficientFunds?.Invoke(amount, _coins);
                return false;
            }

            _coins -= amount;
            Persist();
            LogTransaction($"-{amount} ({reason})");

            OnCoinsSpent?.Invoke(amount, reason);
            OnCoinsChanged?.Invoke(_coins);
            return true;
        }

        /// <summary>Directly sets the balance (e.g. debug tools, cloud-save restore). Clamped to zero or above.</summary>
        public void SetBalance(long amount)
        {
            _coins = Math.Max(0, amount);
            Persist();
            LogTransaction($"=set {_coins}");
            OnCoinsChanged?.Invoke(_coins);
        }

        public long GetBalance() => _coins;

        // --- Semantic wrappers for the specific game modules that share this wallet ---

        /// <summary>Coins earned from finishing/placing in a Ludo match.</summary>
        public void EarnFromLudoMatch(long amount, int placement = 1)
        {
            AddCoins(amount, $"LudoMatch(place={placement})");
        }

        /// <summary>Coins earned from a Jungle Tycoon mini-game.</summary>
        public void EarnFromMiniGame(long amount, string miniGameId)
        {
            AddCoins(amount, $"MiniGame:{miniGameId}");
        }

        /// <summary>Attempts to spend coins on a Jungle Tycoon upgrade; returns false if the player can't afford it.</summary>
        public bool SpendOnJungleUpgrade(long cost, string upgradeId)
        {
            return TrySpendCoins(cost, $"JungleUpgrade:{upgradeId}");
        }

        private void Persist()
        {
            if (_storage == null)
            {
                Debug.LogError("[CurrencyManager] No storage backend available; balance will not persist!", this);
                return;
            }
            _storage.SaveCoins(_coins);
        }

        private void LogTransaction(string entry)
        {
            _recentTransactions.Add(entry);
            int overflow = _recentTransactions.Count - Mathf.Max(1, recentTransactionLogCapacity);
            if (overflow > 0)
            {
                _recentTransactions.RemoveRange(0, overflow);
            }
        }
    }
}
