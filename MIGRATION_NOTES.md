# TANGENT SECURE ECOSYSTEM — Phase 1 Migration Notes

**Project:** Ludo Ampire 3D · Unity 6000.5.4f1 · URP · IL2CPP / ARM64 Android
**Branch:** `feat/phase1-foundation` · **Guiding rule:** do not break `LudoEmpire.Ludo` gameplay in `SampleScene`.

---

## 1. WHAT WAS ADDED (all new, additive, `namespace Tangent.Core`)

New tree under `Assets/_Project/`:

```
_Project/
  Core/
    AppConstants.cs        - every scene name + save key, one source of truth
    UserProfile.cs         - [Serializable] account data (profileId, displayName, coins, diamonds, createdAt, gamesWon)
    GameConfig.cs          - MOVED here from _Project/Scripts/Flow/. Match config. Default() == current 4P game, 1:1.
                             Added: enum GameBoardType {Classic4P, Team2v2, Fast3P}, bool IsTeamMode, ApplyBoardMode().
    FlowScreen.cs          - MOVED here. FlowScreen enum + FlowSceneEntry routing row.
    ToastManager.cs        - self-building "Coming Soon" toast, ToastManager.Show("...")
  Managers/
    FlowManager.cs         - MOVED here. Navigation + data hand-off singleton. Added GoTo() alias,
                             static ActiveMatchConfig, MainMenu/Dashboard routed as Scenes.
    FlowScreenPanel.cs     - MOVED here. Panel show/hide + fade/slide.
    GameServices.cs        - ROOT bootstrap singleton. Spawns SaveService/WalletManager/SoundManager/FlowManager.
                             [RuntimeInitializeOnLoadMethod] so managers exist even opening SampleScene directly.
    SaveService.cs         - JSON profile in Application.persistentDataPath. Migrates LudoEmpire_Coins + games_won.
    WalletManager.cs       - thin facade over CurrencyManager; mirrors OnCoinsChanged -> SaveService.
    SoundManager.cs        - forwards SetMusicVolume -> LudoMusicManager; PlaySFX(name) one-shot registry.
  Services/                - (empty, reserved)
  Security/                - (empty, reserved)
  Data/
    DashboardButtonData.cs - ScriptableObject: the 3x3 Dashboard grid as data (no scene-name in any button)
  UI/
    DashboardController.cs - builds the grid from DashboardButtonData, routes taps via FlowManager / ToastManager
    ClockWidget.cs         - 1/sec text clock (local or fixed UTC offset e.g. PKT +5)
    CarouselWidget.cs      - auto-advancing 3-slide text carousel (leaderboard placeholder)
    Editor/
      DashboardSceneBuilder.cs - MENU ITEMS: Tangent/Phase 1/Build Dashboard Scene, Build Boot Scene, Fix Build Settings Order
  Scenes/                  - (Boot.unity + Dashboard.unity land here once you run the builders)
  Legacy/LudoGame_Dead/    - (empty, target for the deferred quarantine)
```

### Bug fixed (Step 1.3)
`SampleScene`'s Victory screen "Main Menu" button pointed at `"MainMenu"` (the **disabled** old scene, build index 1) instead of `"MainMenu-new"`.

- `Assets/Scenes/SampleScene.unity` — serialized `mainMenuSceneName: MainMenu` → `MainMenu-new` (data-layer fix, **no `LudoEmpire.Ludo` runtime code touched**).
- `Assets/Scripts/Ludo/LudoBoardSceneBuilder.cs:127` — editor-only `const MainMenuSceneName` → `"MainMenu-new"` (this file *is* `LudoEmpire.Ludo` but is `#if UNITY_EDITOR` tooling, a const string, zero gameplay logic — see contradiction note below).
- `LudoVictoryScreenController.cs` was **left untouched**; its `"MainMenu"` field default is overridden by both the scene value and the builder, so the runtime `LudoEmpire.Ludo` file did not need to change.

---

## 2. WHAT WAS **NOT** DONE (deferred — needs the Unity Editor + a compile/build to verify safely)

| Spec step | Why deferred | Plan |
|---|---|---|
| **4× `.asmdef`** (`Tangent.Core/Flow/Ludo/Minigames`) | As written it can't compile. The project has **zero asmdefs today** — everything is `Assembly-CSharp`. An `.asmdef` cannot reference loose `Assembly-CSharp` types, so the moment `_Project` gets an asmdef, `GameConfig`/`FlowManager` lose access to `PlayerColor`, `CurrencyManager`, etc. Isolating `LudoEmpire.Ludo` requires an asmdef **inside `Assets/Scripts/Ludo/`**, which also pulls in every cross-reference from `Chat/`, `Empire/`, `Editor/` — must be introduced with the Editor open and a full recompile to catch breaks. | Phase 1.5, in-Editor: add `Tangent.Ludo.asmdef` in `Assets/Scripts/Ludo/`, then `Assets/Scripts/*` asmdefs one folder at a time, compiling after each. Then `Tangent.Core.asmdef` referencing `Tangent.Ludo`. Keep `LudoGame.*` in `Assembly-CSharp` (unreferenced by asmdefs) = automatic quarantine. |
| **Quarantine all `LudoGame.*` → `_Project/Legacy/` + `[Obsolete]`** | The spec's own later steps depend on `LudoGame.*` code: `SimpleMenuLoader` (`LudoGame.UI`) is the **live** menu loader; `LudoGame.EditorTools` contains `AndroidBuildAutomation` (the build pipeline) and the minigame scene builders (Steps 4–5); `LudoGame.Empire` / `LudoGame.MiniGames.*` / `LudoGame.ThirdPersonGame` are the Dashboard minigames. Moving 60+ files also forces an Editor recompile that must be verified. | Quarantine **only** the dead Ludo *core* after a reference scan: `Game/GameManager`, `Board/GridManager`, `Dice/DiceManager`, `AI/AIPlayer`, `Player/PlayerToken`, `Player/TokenSelector`, `UI/{MainMenuUI,GameOverUI,DiceUI,TurnIndicatorUI}`, `Save/SaveManager`, `Audio/AudioManager`. Keep `SimpleMenuLoader`, all `EditorTools`, all minigames. Exact `git mv` list to be generated with `grep -rl <guid>` over `*.unity`/`*.prefab` first. |
| **Delete `SimpleMenuLoader.cs`** (Step 3.2) | Its `PlayVsAI`/`PassAndPlay` are wired into `MainMenu-new`'s buttons via serialized `onClick` targets. Removing it needs the scene's buttons re-pointed (YAML or Editor) to `FlowManager`/`LudoMainMenuController`. | After a `FlowManager` object is added to `MainMenu-new` (Editor), re-wire the two buttons, then delete. `FlowManager` already carries the Build-Settings-guard SimpleMenuLoader added. |
| **`Boot.unity` + `Dashboard.unity`** (Steps 4–5) | Scene/prefab files can't be hand-authored as valid YAML safely. | Run **`Tangent/Phase 1/Build Dashboard Scene`** then **`Build Boot Scene`** then **`Fix Build Settings Order`** (menu items shipped in `DashboardSceneBuilder.cs`). |
| **Build Settings reorder** (Boot=0, Dashboard=1, SampleScene=2) | Applying before the scenes exist bricks startup. | `Tangent/Phase 1/Fix Build Settings Order` does it once the two scenes are built. |

### Spec contradictions resolved (Tech-Lead calls)
1. *"FORBIDDEN: do not modify any file in `LudoEmpire.Ludo`"* vs *"Step 1.3: edit `LudoBoardSceneBuilder.cs` and `LudoVictoryScreenController.cs`"* — both of those files are `LudoEmpire.Ludo`. Resolution: fixed the bug at the **data layer** (`SampleScene.unity`) + the **editor-tool const**; left `LudoVictoryScreenController.cs` untouched.
2. *"asmdef referencing existing `LudoEmpire.Ludo` scripts only"* — not possible without an asmdef in the core folder (see table). Deferred with a concrete in-Editor plan.
3. *"quarantine all `LudoGame.*`"* vs Steps 4–5 needing `LudoGame.*` minigames/build tools — scoped down to the dead Ludo core only.
4. *Two `GameConfig` classes* (mine from the previous step + the spec's) would collide (CS0101). Kept one `GameConfig` (superset) and added the spec's `GameBoardType {Classic4P, Team2v2, Fast3P}` + `IsTeamMode` + `ApplyBoardMode()` to it.

---

## 3. HOW THE PIECES CONNECT (Phase 1 runtime)

```
app start
  -> GameServices  (RuntimeInitialize or Boot.unity)  ── spawns ──▶ SaveService  ── loads/migrates ──▶ UserProfile JSON
                                                       ├─ spawns ──▶ WalletManager ── binds on scene load ──▶ CurrencyManager
                                                       ├─ spawns ──▶ SoundManager  ── forwards ──▶ LudoMusicManager
                                                       └─ spawns ──▶ FlowManager
  -> (Boot only) wait 1s -> FlowManager.GoTo(Dashboard)
Dashboard "Ludo" button
  -> FlowManager.StartMatch(GameConfig.Default())
       - PendingConfig / static ActiveMatchConfig = Default()  (4P, PlayerVsAI, Standard)
       - LudoGameModeSelection.Select(PlayerVsAI)   ← existing SampleScene wiring reads this, unchanged
       - loads "SampleScene"  ← identical scene, identical rules
SampleScene "PlayVsAI" button
  -> unchanged (SimpleMenuLoader still present)
coins earned in a match
  -> CurrencyManager.AddCoins(...)         ← UNCHANGED (how coins are earned)
  -> CurrencyManager.OnCoinsChanged fires
  -> WalletManager mirrors -> SaveService.SetCoins() -> JSON file written  ← NEW (how coins are saved)
next app start
  -> SaveService loads JSON -> WalletManager pushes balance into CurrencyManager.SetBalance()  ← JSON is source of truth
```

### Save migration safety (one-shot, non-destructive)
`SaveService.MigrateFromLegacyPlayerPrefs()`:
1. read `LudoEmpire_Coins` + `games_won`
2. write `tangent_profile_v1.json` (atomic: `.tmp` then move) + PlayerPrefs mirror
3. **read it back and verify** `coins` round-tripped
4. only then `DeleteKey(LudoEmpire_Coins)` + `DeleteKey(games_won)` + set `Tangent_Migrated_v1 = 1`
Any exception at any step → legacy keys kept, retried next launch. `LudoEmpire_SelectedTheme` is **not** deleted (still owned by `LudoThemeManager`).

---

## 4. TEST CASES — status

| # | Test | Status |
|---|---|---|
| 1 | Project compiles, IL2CPP build succeeds | **Pending** — new code is brace/paren-balanced & API-checked; run a build before merge (same gate used for Tasks 1–3). |
| 2 | Boot → Dashboard → "Ludo" → SampleScene plays exactly as before | **Ready to verify** once `Tangent/Phase 1/*` menu items are run. `StartMatch(Default())` → `LudoGameModeSelection.PlayerVsAI` → same scene. |
| 3 | "PlayVsAI" in SampleScene still works | **Yes** — `SimpleMenuLoader` untouched, no SampleScene UI removed. |
| 4 | Coins persist via SaveService after restart | **Wired** — `WalletManager` ↔ `CurrencyManager.OnCoinsChanged` ↔ `SaveService` JSON. Verify on device. |
| 5 | `MIGRATION_NOTES.md` | This file. |

---

## 5. NEXT (Phase 1 completion checklist, in Editor)
1. Open project → let it compile → **0 errors** (fix any before proceeding).
2. `Tangent/Phase 1/Build Dashboard Scene` → creates `Assets/_Project/Scenes/Dashboard.unity` + `Assets/_Project/Data/DashboardButtons.asset`.
3. `Tangent/Phase 1/Build Boot Scene` → creates `Assets/_Project/Scenes/Boot.unity`.
4. `Tangent/Phase 1/Fix Build Settings Order` → Boot=0, Dashboard=1, SampleScene=2, MainMenu-new=3.
5. Press Play from Boot → Dashboard → tap **Ludo** → SampleScene → play a match → tap **PlayVsAI** → verify no regression.
6. Earn coins, force-quit, relaunch → coins restored from JSON.
7. IL2CPP Android build → install → repeat 5–6 on device.
8. Then Phase 1.5: asmdefs + legacy quarantine (see section 2).

---

## PHASE 1 CLOSURE — 2026-09-04

**Phase 1 Closed: Scenes generated, SimpleMenuLoader removed, BuildSettings fixed** *(pending the Editor run of the menu items below).*

### Editor tools added (`Assets/_Project/Editor/`, menu `Tangent/Phase 1/…`)
| Menu item | Does |
|---|---|
| **Build Dashboard Scene** | (re)creates `Assets/_Project/Scenes/Dashboard.unity` + `Assets/_Project/Data/DashboardButtons.asset` |
| **Build Boot Scene** | (re)creates `Assets/_Project/Scenes/Boot.unity` with `GameServices (autoGoToDashboardOnBoot=true)` |
| **Fix Build Settings Order** | `0 Boot · 1 Dashboard · 2 SampleScene` enabled; all others disabled |
| **CLOSE PHASE 1 (build scenes + fix build settings)** | runs the three above, logs `Phase 1 Closure Complete` |
| **Wire MainMenu (replace SimpleMenuLoader)** | adds `MenuMatchStarter` to MainMenu-new's Canvas, repoints `PlayVsAIButton`/`PassAndPlayButton` onClick at it, strips all `SimpleMenuLoader` components from the scene |
| **Delete SimpleMenuLoader.cs** | only after the above; aborts if any scene/prefab still references its GUID |

### Runtime added
- `MenuMatchStarter` (`Tangent.Core`) — parameterless `StartPlayVsAI()` / `StartPassAndPlay()` → `FlowManager.StartMatch(...)`. A UnityEvent persistent listener can't pass a `GameConfig`, hence the wrapper.

### Why not executed here
The Unity Editor is open on the project (GUI, PID 5524) so a headless run is locked out — and scene generation / button re-wiring / file deletion are Editor actions by design. Run them from the menu.

### Run order to finish Phase 1
1. Let the Editor compile the new scripts → **0 errors** in the Console.
2. `Tangent/Phase 1/CLOSE PHASE 1` → Boot.unity + Dashboard.unity + Build Settings.
3. `Tangent/Phase 1/Wire MainMenu (replace SimpleMenuLoader)`.
4. Press Play from `Boot.unity` → Boot → Dashboard → tap **Ludo** → SampleScene → tap **PlayVsAI** → verify identical gameplay.
5. Earn coins → stop → Play again → coins restored from the JSON profile.
6. `Tangent/Phase 1/Delete SimpleMenuLoader.cs`.
7. IL2CPP Android build → install → repeat 4–5 on device.

### Verified already
- Phase 1 Foundation **IL2CPP build: `Result: Succeeded, Errors: 0`** (test case 3).
- `LudoBoardLogic.cs` and every `LudoEmpire.Ludo` runtime file: **untouched** across Phase 1 + closure.

### EXECUTED 2026-09-04 (headless)
- `Phase1Closer.CloseAll` -> `Boot.unity`, `Dashboard.unity`, `DashboardButtons.asset` generated; Build Settings = `0 Boot, 1 Dashboard, 2 SampleScene` (rest disabled).
- `MainMenuWirer.WireMainMenu` -> both mode buttons re-pointed to `MenuMatchStarter`; 0 `SimpleMenuLoader` component refs; 0 broken scripts. Unity's re-serialize also dropped ~28k lines of orphaned Material/Mesh sub-assets from `MainMenu-new.unity` (1.19 MB -> 31 KB) - the dead-asset cleanup noted in the audit, for free.
- `SimpleMenuLoader.cs` deleted (verified: no `.cs`, `.unity`, `.prefab`, or `.asset` reference remained).
- `SampleScene.unity` / `MainMenu.unity` were re-serialized by Unity 6 as a side-effect of the batch run and **reverted to their committed versions** - gameplay stays byte-identical.
- IL2CPP compile: clean (all Tangent.* + editor tools; SimpleMenuLoader removal broke nothing). Full APK build verifying.

---

## PHASE 2 — SECURITY (2026-09-04)

**"Never trust the client."** Additive scaffold in `Tangent.Core` / `Tangent.Services`. No `LudoEmpire.Ludo` changes, no PlayerPrefs for storage, no client-authoritative reward amounts.

### Files
| File | Role |
|---|---|
| `Core/RBAC.cs` | `enum Role {Player,Pro,Staff,Manager,CEO}`; `SecurityConfig` ScriptableObject (keys, limits, enforcement flags); `RBAC.HasPermission(Role)` / `Demand(Role, action)` - role read from the server profile, never the save file. |
| `Core/SecurityManager.cs` | AES-256-CBC `Encrypt`/`Decrypt` (PBKDF2-SHA256 key from `SecurityConfig`, or a backend key via `SetServerKey`), `GetHash`/`VerifyHash` (SHA-256), `IsRooted` (su/Magisk probe), `BindDevice` (`SystemInfo.deviceUniqueIdentifier`), `HandleTamper` -> AuditLog + guarded wipe. |
| `Services/BackendService.cs` | Singleton HTTP client (`UnityWebRequest`). `LoginGuest`/`RefreshToken`/`ApiPost`/`ValidateReward`/`GetServerCoins`. Bearer auth, HMAC-SHA256 body signature (`X-Signature`), 3 retries w/ exp backoff, one 401 -> refresh -> retry. **`apiBaseUrl` empty = OFFLINE and every `ValidateReward` DENIES.** |
| `Services/AuditLog.cs` | Singleton `Queue<ActionLog>` (time,userId,action,before,after,ip,device). `Log`/`FlushToServer`/`ExportCSV`. Auto-flush coroutine every 30s; entries stay queued until the server accepts them. |
| `Services/AntiCheatManager.cs` | `ValidateAd` (server `ValidateReward("ad",amount)` + `dailyAdCap=50`, count stored in the encrypted profile), `CheckSpeedHack` (>10 starts / 60s -> flag + report), `ValidateWallet` (local vs `GetServerCoins`; drift > `walletToleranceFraction` (10%) -> lock, server-confirmed only). |
| `Editor/Phase2SecurityTools.cs` | `Tangent/Phase 2/Validate Security` - creates `Resources/SecurityConfig.asset` with fresh random keys, checks every service is a MonoBehaviour singleton, runs AES round-trip + mock-tamper + hash tests. Logs **"Phase 2 Security Validation Complete"**. |

### Changed (Tangent-owned only)
- `Core/UserProfile.cs` - `role` string + `List<CounterEntry> counters` (`GetCounter`/`SetCounter`) for the daily ad count.
- `Managers/SaveService.cs` - **now AES-encrypted**: `tangent_profile_v1.enc` envelope `{v,hash,data}` where `hash` = SHA-256 of the plaintext. Load order `.enc -> .enc.bak -> Phase-1 .json -> legacy PlayerPrefs -> new`. Hash/decrypt failure -> `SecurityManager.HandleTamper` -> coins reset to 0. PlayerPrefs mirror removed (`.enc.bak` file instead); the only PlayerPrefs touch is the one-time legacy read.
- `Managers/GameServices.cs` - spawns `BackendService, AuditLog, SecurityManager, AntiCheatManager` before the Phase 1 services.

### Architect's caveats (documented, not fixed here)
1. **A key inside the APK is obfuscation, not security.** `SecurityConfig.localDataKeyBase64` deters casual save editors only. `useServerKeyWhenAvailable` + `SetServerKey` is the hook for a real post-auth key; Android Keystore is the proper store. Flagged, not solved.
2. **Offline cannot self-destruct.** `requireServerConfirmationToLock` (default on) means a hash mismatch while offline queues a tamper report and defers the wipe - so legit offline play with a flaky filesystem doesn't nuke progress. Tighten once a backend exists.
3. **Spec's `localCoins != serverCoins * 1.1`** read as "10% tolerance" -> `|local - server| > server * walletToleranceFraction`. The literal formula would flag almost every honest player.
4. **This adds network code** (`UnityWebRequest`), which earlier phases forbade. It is inert without `apiBaseUrl`. No ads/IAP SDKs added.

### Manual step
- `SecurityConfig.asset` must live in a **Resources** folder for `Resources.Load`. Placed at `Assets/_Project/Resources/SecurityConfig.asset` (not `Data/`, which isn't loadable at runtime). Run `Tangent/Phase 2/Validate Security` once to generate it with real keys.

### Verification (pending device/editor run)
1. Edit `tangent_profile_v1.enc` -> next load: hash check fails -> coins = 0, AuditLog has a `TAMPER` entry.
2. `AntiCheatManager.ValidateAd(...)` with no `apiBaseUrl` -> `onResult(false)` (offline denies).
3. IL2CPP Android build - verifying now.

### Fix 2026-09-04 — `SecurityConfig` moved to its own file (`fix/securityconfig-own-file`)
`SecurityConfig` (a `ScriptableObject`) was declared **inside `Core/RBAC.cs`**. Unity only
mints a `MonoScript` for a `ScriptableObject`/`MonoBehaviour` whose class name matches its
file name, so the generated `Resources/SecurityConfig.asset` came out with
`m_Script: {fileID: 0}` — it would not deserialize at runtime and `SecurityConfig.Load()`
silently returned the conservative in-memory default (offline, deny all rewards).

- **New** `Assets/_Project/Core/SecurityConfig.cs` — the class moved verbatim (still `namespace Tangent.Core`, still `[CreateAssetMenu]`).
- `Core/RBAC.cs` — trimmed to `enum Role` + `static class RBAC` only.
- `Resources/SecurityConfig.asset` regenerated by `Tangent/Phase 2/Validate Security` —
  `m_Script` now `{fileID: 11500000, guid: d84bc4ced8af4b84b22f4352069cb8fe, type: 3}`.
- Headless `Validate Security`: **12 passed, 0 failed** (AES round-trip, mock tamper, SHA-256, every service a MonoBehaviour singleton).

---

## RENAME — Tangent → TangentLudoEmpire (2026-09-04, branch `refactor/tangent-ludo-empire`)

Isolate this project from other "Tangent" projects for a clean delete later.

| Area | Before | After |
|---|---|---|
| Folder | `Assets/_Project/` | `Assets/_TangentLudoEmpire/` (`git mv`, all asset GUIDs preserved) |
| Namespaces | `Tangent.Core`, `Tangent.Services` | `TangentLudoEmpire.Core`, `TangentLudoEmpire.Services` |
| Bridge files | `namespace LudoEmpire.Ludo` | **unchanged** (`GameConfig`, `FlowScreen`, `FlowManager`, `FlowScreenPanel` — do-not-touch gameplay layer) |
| Editor menus | `Tangent/…` | `Tangent Ludo Empire/…` (8 `MenuItem` + 2 `CreateAssetMenu`) |
| Save file | `tangent_profile_v1.enc` | `tle_profile_v1.enc` (`.enc.bak` follows) |
| PlayerPrefs keys | `Tangent_Profile_v1`, `Tangent_Migrated_v1` | `TLE_Profile_v1`, `TLE_Migrated_v1` |
| Kept as-is | `LudoEmpire_Coins`, `LudoEmpire_SelectedTheme` (shipped game), `tangent_profile_v1.json` (legacy migration read-source) | |
| Serialized refs | `Tangent.Core.MenuMatchStarter` in `MainMenu-new.unity`; `m_EditorClassIdentifier` in `Boot.unity` / `Dashboard.unity` | `TangentLudoEmpire.Core.*` |
| `EditorBuildSettings` | `Assets/_Project/Scenes/{Boot,Dashboard}.unity` | `Assets/_TangentLudoEmpire/Scenes/…` |
| `ProjectSettings` | `productName: Ludo Ampire 3D`, `Android: com.tangent.ludoampire` | `productName: Tangent Ludo Empire`, `Android: com.tangent.ludoempire` (companyName already `Tangent`) |

Logic unchanged. Headless `Validate Security` after the rename: **12 passed, 0 failed**.
**appId change note:** `com.tangent.ludoampire → com.tangent.ludoempire` makes this a distinct Android app (no upgrade path from an installed `ludoampire` build; new signing identity for store upload).

---

## PHASE 3 ADDED — Secure Wallet + Payment In/Out Scaffold (2026-09-04, branch `feat/phase3-wallet`)

Real-money wallet + payment abstraction. **Mock only — no real gateway, no network in this phase.**
All money is `decimal` (never `float`/`double`). Persisted AES-256 + SHA-256, same envelope as the profile.

### New files
```
Assets/_TangentLudoEmpire/Phase3/
  Wallet/
    Transaction.cs        TxType{Deposit,Withdraw,GameWin,GameLoss,Bonus}, TxStatus{Pending,Success,Failed},
                          [Serializable] Transaction (decimal Amount via invariant-string backing - JsonUtility
                          can't serialise decimal; TxId, Timestamp (Unix ms), GatewayRef)
    WalletData.cs         [Serializable] WalletData (decimal Balance via string backing, List<Transaction> History
                          capped at 100, PlayerId, IntegrityString())
    MoneyWallet.cs        MonoBehaviour singleton. THE real-money wallet (distinct from Core.WalletManager, the
                          in-game *coin* facade). Load/Save via SaveService.LoadEncrypted/SaveEncrypted
                          <WalletData>("tle_wallet_v1.enc"). AddFunds(amount, source, gatewayRef?) - deposits run
                          AntiCheatManager.ValidatePayment(ref) first ("never trust the client"). DeductFunds(amount,
                          reason) - RBAC.CanSpend() + balance check, never negative, audits "INSUFFICIENT_FUNDS".
                          Every change: SaveEncrypted + SecurityManager.ValidateChecksum + AuditLog "WALLET_CHANGE".
    WalletGameBridge.cs   Namespaced entry point for the gameplay layer to report win/entry (Task 7 - see Deferred).
  Payments/
    IPaymentGateway.cs    Deposit(decimal, Action<bool,string>) / Withdraw(decimal, account, Action<bool,string>).
    MockPaymentGateway.cs Deposit ~2s / 90% success -> "MOCK_TX_<n>"; Withdraw ~3s / 80% -> "MOCK_WD_<n>";
                          fail -> "PAYMENT_DECLINED" (deposit) / "INSUFFICIENT_BALANCE" (withdraw).
    PaymentRunner.cs      Hidden MonoBehaviour so the plain gateway can use coroutines + main-thread callbacks.
    PaymentGatewayFactory.cs  Current => MockPaymentGateway. enum PaymentProvider{Mock,JazzCash,Easypaisa,Card}.
  UI/  (Unity uGUI, Text + Button only, self-building if no Inspector refs)
    WalletUiKit.cs        internal builder helpers.
    WalletUI.cs           balance label + [Deposit] [Withdraw] [History].
    DepositPopup.cs       amount -> gateway.Deposit -> MoneyWallet.AddFunds(amount,"DEPOSIT",ref).
    WithdrawPopup.cs      amount + account -> gateway.Withdraw -> MoneyWallet.DeductFunds(amount,"WITHDRAW:"+ref).
    TransactionHistoryUI.cs   scroll list, last 20 tx, newest first.
Assets/_TangentLudoEmpire/Editor/
    Phase3Tools.cs        Menu "Tangent Ludo Empire/Phase 3/": Give 1000 Coins, Reset Wallet,
                          Validate Wallet Integrity (loads/decrypts/verifies envelope hash + ledger consistency),
                          Verify Wallet (headless) - the Task-8 checklist as PASS/FAIL lines.
```

### Changed (Tangent-owned only, additive)
- `Core/SecurityManager.cs` — `ValidateChecksum(plain, expectedHex)` (alias of `VerifyHash`).
- `Core/RBAC.cs` — `CanSpend()` — true unless `AntiCheatManager` has locked/flagged the wallet.
- `Services/AntiCheatManager.cs` — `ValidatePayment(gatewayRef, Action<bool>)` — routes the gateway ref to
  `BackendService.ValidateReward`; fails closed offline (Editor allows so the mock flow is testable).
- `Managers/SaveService.cs` — generic `SaveEncrypted<T>` / `LoadEncrypted<T>` / `EncryptedFileExists` /
  `DeleteEncrypted` (same envelope; hash mismatch => TAMPER, no wipe — caller owns recovery).
- `Managers/GameServices.cs` — bootstraps `MoneyWallet` + `WalletGameBridge` after `SaveService`, before the
  Dashboard.

### Verification (headless, Editor)
- **0 compile errors.** Phase 2 `Validate Security` still **12/12**.
- **`Phase 3 / Verify Wallet (headless)`: 9 passed, 0 failed** — new wallet = 0.00; deposit 100 -> 100.00, 1 tx;
  withdraw 50 -> 50.00, tx Success; deduct 1000 rejected (INSUFFICIENT_FUNDS), balance unchanged, Failed tx
  recorded; integrity OK (envelope hash verified, balance 50.00 == ledger 50.00).
- Mock gateway deposit/withdraw latency + `WalletUI` are **Play-mode / device** checks (coroutine timing).

### Deferred (needs Editor / a touch to the do-not-modify gameplay layer)
- **Task 7.1 / 7.2** — `FlowManager` is in `LudoEmpire.Ludo` (hard "do not modify"), and the match win / entry
  events fire inside `LudoBoardLogic` / `LudoVictoryScreenController` (same rule). `WalletGameBridge.ReportGameWin
  (decimal)` / `ReportGameEntry(decimal)` are the ready entry points; wiring them is one call from the gameplay
  side, or a scene `UnityEvent` on the bridge. Inert until then.
- **Task 6 menu items** run fully headless (Edit-mode file path) — no manual step, but `Give 1000 Coins` credits
  the **money wallet** (decimal), not the coin `CurrencyManager`, since it lives under the Phase 3 wallet tools.

### Architect deviations (Tech-Lead calls)
1. `WalletData.Balance` / `Transaction.Amount` keep a `decimal` **public API** but serialise through a private
   invariant-culture `string` — `JsonUtility` (SaveService's serialiser) drops `decimal` fields silently.
2. New class named **`MoneyWallet`** (not `WalletManager`) — a second `WalletManager` alongside the existing
   `Core.WalletManager` coin facade is bug-bait in a money system.
3. Deposit-failure code is `"PAYMENT_DECLINED"` (spec literally said `"INSUFFICIENT_BALANCE"`, which only makes
   sense for a withdrawal — kept there).

---

## PHASE 3.1 ADDED — Real Gateway Interface for JazzCash & Easypaisa Sandbox (2026-09-04, branch `feat/phase3.1-real-gateways`)

Replaces the single generic mock with two provider-shaped sandbox mocks: real post-data fields, real
secure-hash math (HMAC-SHA256 for JazzCash, SHA-256 for Easypaisa), but the "network call" is still a
timed coin-flip — **no HTTP added, no live credentials, sandbox only.**

### New files
```
Assets/_TangentLudoEmpire/Phase3/Payments/
  PaymentConfig.cs       ScriptableObject (own file - same MonoScript-identity lesson as SecurityConfig).
                         JazzCash_MerchantID/Password/IntegritySalt/ReturnURL, Easypaisa_StoreID/
                         StorePassword/PostBackURL, UseSandbox. [CreateAssetMenu(".../Payment Config")].
  JazzCashGateway.cs     IPaymentGateway. Deposit builds pp_Amount(paisa)/pp_BillReference/pp_MerchantID/
                         pp_ReturnURL, HMAC-SHA256 pp_SecureHash (ComputeHMAC, System.Security.Cryptography),
                         ~2s/90% mock. Withdraw -> WITHDRAW_NOT_SUPPORTED (JazzCash checkout is deposit-only).
  EasypaisaGateway.cs    IPaymentGateway. Deposit builds storeId/amount/orderId/postBackURL,
                         hash = SHA256(storeId+orderId+amount+password), ~3s/80% mock.
                         Withdraw -> WITHDRAW_NOT_SUPPORTED (same reason).
```
`Phase3/Payments/PaymentRunner.cs` gained a shared `SimulateResult()` (the wait+coin-flip+callback shape
all three gateways now use, de-duplicated).

### Changed
- `IPaymentGateway.cs` — added `string GatewayName { get; }`, `Deposit` now takes a `phone` param, added
  `GenerateSecureHash(Dictionary<string,string>)`. **Breaking change**, all 3 implementers updated.
- `MockPaymentGateway.cs` — updated to the new interface (`GatewayName => "Mock"`, `phone` param, a
  deterministic SHA-256 `GenerateSecureHash` — no real secret behind the mock).
- `PaymentGatewayFactory.cs` — `Active`/`Current` kept (defaults to Mock — JazzCash/Easypaisa can't
  withdraw, so `WithdrawPopup` stays on Mock); new `GetGateway(PaymentProvider)` builds/caches a gateway
  per provider from `PaymentConfig.Load()`; `InvalidateCache()` added so editing the config asset takes
  effect without a domain reload.
- `Phase3/UI/WalletUiKit.cs` — added `DropdownField(...)` (minimal legacy uGUI Dropdown: caption + a
  one-item Toggle template — no art, matches the Text/Button rule).
- `Phase3/UI/DepositPopup.cs` — rebuilt: provider **Dropdown** [JazzCash, Easypaisa], phone `InputField`,
  `PaymentGatewayFactory.GetGateway(selected).Deposit(amount, phone, callback)`, a text-only "Contacting
  &lt;Gateway&gt;..." dot-animation while waiting (no spinner graphic — Text/Button constraint).
- `Editor/Phase3Tools.cs` — `EnsurePaymentConfig()` (auto-creates `Resources/PaymentConfig.asset` with
  obviously-fake sandbox placeholders, Task 9.2); menu items **Test JazzCash Deposit 100** / **Test
  Easypaisa Deposit 500** (Play mode: real gateway + live `MoneyWallet`; Edit mode: same real
  `GenerateSecureHash`, wallet credited via the encrypted file — see Deferred); **Verify Gateways
  (headless)** — the Task-9 checklist as PASS/FAIL lines.

### Security (Task 7)
1. **`JazzCash_IntegritySalt` / `*_StorePassword` are never logged.** Every gateway log line carries only
   non-secret fields plus a *masked* hash (`Mask()`: first 6 + last 4 hex chars) — grepped the verify log
   for the raw sandbox secret strings after a run: zero hits outside the one-line "config created" notice,
   which itself never prints the values.
2. Deposit success -> `MoneyWallet.AddFunds(amount, "DEPOSIT_" + GatewayName, gatewayRef, ...)` — routes
   through the existing Phase 3 anti-cheat gate (`AntiCheatManager.ValidatePayment`) automatically, since
   `gatewayRef` is non-null; no wallet code changed.
3. `AuditLog.Log("DEPOSIT_ATTEMPT", "{amount} via {GatewayName}", phone)` fires before the gateway call.
4. `Transaction.GatewayRef` = the provider's txId (`JC_TX_…` / `EP_TX_…`) — already flowed through
   `MoneyWallet.AddFunds` → `ApplyCredit` → `Transaction.New(...)`, no `Transaction.cs` change needed.

### Verification (headless, Editor)
- **0 compile errors.**
- **`Verify Gateways (headless)`: 8 passed, 0 failed** — JazzCash deposit 100 -> balance 100.00, ref
  `JC_TX_…`; Easypaisa deposit 500 -> balance 600.00, ref `EP_TX_…`; 2 tx in history; both `.Withdraw()`
  calls return `WITHDRAW_NOT_SUPPORTED`; integrity OK (envelope hash + ledger sum = 600.00).
- `PaymentConfig.asset` auto-created at `Assets/_TangentLudoEmpire/Resources/PaymentConfig.asset` with a
  valid `m_Script` reference on the first run (applied the SecurityConfig lesson: the ScriptableObject
  lives in its own file from the start).

### Deferred (explicitly out of scope here, flagged not hidden)
- **No real HTTP call.** Both gateways build the real post-data + real hash and leave a `TODO` where the
  live JazzCash MWALLET / Easypaisa Open-MA POST goes (via `BackendService`, server-side — the merchant
  password must never leave a server). Swapping the mock coroutine for a real call is localized to one
  method per gateway.
- **Test menu items in Edit mode credit the wallet file directly** (no coroutine ticking outside Play
  mode, and `MoneyWallet` doesn't exist until `GameServices` bootstraps it) rather than faking a timed
  network wait. Enter Play mode and re-run the same menu item for the real 2-3s/90%-or-80% sandbox flow
  against the live `MoneyWallet` singleton.
- **`WithdrawPopup` still targets `PaymentGatewayFactory.Current`** (Mock) — JazzCash/Easypaisa return
  `WITHDRAW_NOT_SUPPORTED` by design, so routing withdrawals through them would just always fail; Task 6
  only asked to upgrade `DepositPopup`.

---

## PHASE 3.2 ADDED — WebView + Callback Verification, Real Sandbox Flow (2026-09-04, branch `feat/phase3.2-webview-callback`)

Replaces the Phase 3.1 timed coin-flip with an actual browser round trip: `Deposit` now sends the user to
the gateway's own sandbox page and only resolves once a local callback listener sees the redirect back,
verifies its hash, and the wallet confirms the pending deposit. **Sandbox only, no real money, no HTTP
POST added to any server — everything below runs in the Editor / a Standalone dev build.**

### New files
```
Assets/_TangentLudoEmpire/Phase3/Payments/
  WebViewManager.cs           Opens the payment URL (Application.OpenURL - no BestHTTP/UniWebView
                              package installed), waits for PaymentCallbackListener's result for that
                              orderId, 120s timeout -> ("TIMEOUT", failed).
  PaymentCallbackListener.cs  Local TCP listener on :8081/callback (see "Why TcpListener" below).
                              Stops after 1 callback or 120s. Detects JazzCash (pp_ResponseCode) vs
                              Easypaisa (responseCode) by which keys are present, calls that gateway's
                              ValidateCallbackHash, and fires OnPaymentComplete(orderId, success, txnId,
                              amount) on the MAIN thread (background listen thread -> lock-guarded queue
                              -> drained in Update()).
```

### Why TcpListener, not HttpListener (Task 1.2's "if not installed" fallback also revisited)
`ProjectSettings.asset: apiCompatibilityLevel: 6` = **.NET Standard 2.0**, project-wide, no per-platform
override. `System.Net.HttpListener` is not part of that profile and would fail to compile (`error CS0234`)
- confirmed by reading the setting before writing any code, not discovered by a failed build. Built the
listener on `System.Net.Sockets.TcpListener`/`TcpClient` instead (in netstandard2.0) with a small
hand-rolled HTTP/1.1 GET parser (read the request line, drain headers, write a minimal 200 response,
extract path+query) - functionally identical for "accept one browser GET with query params."

### Changed
- `IPaymentGateway.cs` — added `bool ValidateCallbackHash(Dictionary<string,string> data)`. **Breaking
  change**, all 3 implementers updated. Also: `Deposit`'s callback contract changed (see Architect
  decision below) - it now fires only once money has actually moved, not once "the gateway said yes."
- `JazzCashGateway.cs` / `EasypaisaGateway.cs` — `Deposit` now: builds the order id (`TLE_<user>_<ticks>`,
  Task 5.3), builds the real post-data + hash (unchanged from 3.1), opens
  `https://sandbox.jazzcash.com.pk/CustomerPortal/.../merchantform/` or
  `https://merchant.easypaisa.com.pk/easypay/Index.jsf` via `WebViewManager.OpenPaymentUrl` with
  `pp_ReturnURL` / `postBackURL` = `PaymentCallbackListener.CallbackUrl`
  (`http://localhost:8081/callback`), and only invokes its own callback after
  `MoneyWallet.ConfirmPendingDeposit` resolves. `ValidateCallbackHash` re-derives the same hash from the
  callback's own fields and compares (`CALLBACK_TAMPERED` + `false` on any mismatch). `MakeOrderId()` made
  `public static` purely so `Phase3Tools` can check its format headlessly.
- `MockPaymentGateway.cs` — added a trivial `ValidateCallbackHash` (`true` - it never opens a WebView, so
  nothing ever calls it); `Deposit` now credits `MoneyWallet` **internally** before invoking its callback,
  to match the new contract (see below).
- `Phase3/Wallet/MoneyWallet.cs` — three new methods implementing Task 6's flow (`Deposit clicked ->
  Status=PENDING -> WebView -> Callback Success -> ConfirmPendingDeposit`):
  - `BeginPendingDeposit(pendingTxId, amount, source)` — records a `Transaction` with `Status = Pending`
    *before* the browser opens. No balance change.
  - `ConfirmPendingDeposit(pendingTxId, amount, done?)` — finds the one still-`Pending` row for that ref,
    runs it through the same `AntiCheatManager.ValidatePayment` gate `AddFunds` already uses, and only
    then credits the balance and flips the row to `Success`. **Idempotent**: a second call for the same
    ref finds no `Pending` row left and no-ops — this is the double-credit guard Task 6 asked for.
    Credits the amount recorded at `BeginPendingDeposit`, not whatever the caller passes in (logged if
    they disagree) - a callback-supplied amount is a claim, not a grant.
  - `FailPendingDeposit(pendingTxId, reason)` — flips the row to `Failed` (WebView failure/timeout path).
- `Phase3/UI/DepositPopup.cs` — no longer calls `MoneyWallet.AddFunds` itself; the gateway already
  credited (or didn't) by the time its `Deposit` callback fires. Calling `AddFunds` here too would have
  been an actual double-credit bug given the contract change above.
- `Editor/Phase3Tools.cs` — **Start Callback Listener** (Play-mode guard + clear message if not playing);
  **Test Real JazzCash Flow** (10 PKR, Play-mode only — genuinely opens a browser); **Verify WebView Flow
  (headless)** — everything below that doesn't need a live browser.

### Architect decision: `Deposit`'s callback now means "money is settled," always
Task 6 asked for `ConfirmPendingDeposit` to prevent a double credit from the WebView flow specifically.
Doing that alone while `DepositPopup` (the caller) still called `MoneyWallet.AddFunds` on every gateway's
success callback would have re-introduced exactly that bug for JazzCash/Easypaisa (credit inside
`ConfirmPendingDeposit`, credit again in the UI). Fix: moved wallet-crediting **into every gateway**
(`Mock` calls `AddFunds` directly; JazzCash/Easypaisa call `BeginPendingDeposit`/`ConfirmPendingDeposit`/
`FailPendingDeposit`), so `IPaymentGateway.Deposit`'s callback has one consistent meaning across all three
implementations and the UI layer never touches the wallet for a deposit again.

### Security (Task 5 / constraints)
1. `ValidateCallbackHash` re-derives the hash from the callback's own fields via the same math as
   `GenerateSecureHash` and rejects on mismatch, logging `CALLBACK_TAMPERED` - verified with a genuine
   tamper test (flip one field, hash no longer matches, `false` + the log line) for both gateways.
2. `IntegritySalt` / `StorePassword` are still never logged (unchanged from 3.1) - only masked hashes.
3. `Password`/`IntegritySalt`/`StorePassword` live ONLY in `PaymentConfig.asset`, never PlayerPrefs -
   nothing added in this phase touches PlayerPrefs at all.
4. Order id format `TLE_<userId>_<ticks>` makes a replayed callback (same ref reused) collide with
   `ConfirmPendingDeposit`'s idempotency guard rather than crediting twice, on top of being unguessable
   per-attempt.

### Verification (headless, Editor)
- **0 compile errors.**
- **`Verify WebView Flow (headless)`: 14 passed, 0 failed** — order-id format for both gateways; both
  gateways' `ValidateCallbackHash` accept a correctly-signed callback and reject a tampered one;
  `PaymentCallbackListener.ParseQuery` decodes fields and round-trips a URL-escaped order id; the full
  pending-deposit lifecycle (Begin -> no balance change -> Confirm -> balance credited once -> a second
  Confirm for the same ref -> no-op, balance unchanged).
- **`Verify Gateways (headless)` (Phase 3.1) re-run: still passing** — the `Deposit`/`ValidateCallbackHash`
  contract changes didn't touch `GenerateSecureHash` or the Edit-mode test path, so no regression.

### What was NOT (and could not be) run
Task 8 steps 2-6 need a human at a keyboard with real JazzCash/Easypaisa sandbox merchant credentials:
pressing Play, clicking **Start Callback Listener** then **Test Real JazzCash Flow**, and manually
completing (or declining) the sandbox 3D-Secure/OTP page that opens in the browser. There is no
`-batchmode` equivalent of "a person enters a test card and an OTP" - `PaymentConfig.asset` currently
holds auto-generated SANDBOX placeholder values (from Phase 3.1's `EnsurePaymentConfig`), not a real
merchant account, so even an automated browser hit would fail at JazzCash's own login step. To run it for
real: open `Resources/PaymentConfig.asset` in the Inspector, fill in real JazzCash sandbox
MerchantID/Password/IntegritySalt, press Play, run the two menu items above, and watch the Console for
`SUCCESS: <ref>. Credited 10.00.` / the wallet balance change.

### Deferred (explicitly out of scope, flagged not hidden)
- **`PaymentCallbackListener` does not work on a shipped Android/iOS build** — see its own class remarks.
  A phone's external browser cannot reach "localhost" back into this app's process. Production needs a
  backend webhook (the gateway's redirect hits a real public HTTPS endpoint you control, which then tells
  the client over your own API/socket) or an app-registered deep link / Android App Link that the OS
  routes back as an Intent — this listener is a dev-loop convenience only.
- **No HTML auto-submit relay for a real POST-only sandbox endpoint.** The URLs built here are GET query
  strings, per the task's literal "build the URL, add the fields" instruction; if JazzCash's live
  CustomerPortal insists on a POSTed form, `WebViewManager` would need to open a small local page (served
  the same way the callback is) that auto-submits a form instead of a raw `Application.OpenURL` to the
  gateway. Not built speculatively since it can't be verified without a live sandbox account.
- **Stale `Pending` rows aren't reconciled.** If the app is killed mid-payment, that deposit sits in
  history as `Pending` forever (never double-credited, never silently lost — see the Task-6 test above —
  but also never resolved). A production build needs a periodic job that asks the backend "what actually
  happened to order X" for any `Pending` row older than a few minutes. Out of scope here.
- **Easypaisa's real callback field names are an assumption.** The task didn't specify them beyond
  `responseCode=0000`; `transactionId` / `easypaisaCallbackHash` are reasonable guesses modeled on
  JazzCash's shape, not confirmed against Easypaisa's actual sandbox docs (no verified access to those
  from this environment). Update `PaymentCallbackListener.HandleCallbackData` /
  `EasypaisaGateway.ValidateCallbackHash` if the real sandbox uses different field names.

---

## PHASE 3.4 DONE — MoneyWallet Connected to Gameplay (2026-09-04, branch `feat/phase3.4-gameplay-bridge`)

Wires the wallet to match entry/win without touching a single file in `LudoEmpire.Ludo` - the coupling is
one-directional (gameplay fires a `UnityEvent`, wallet code listens) and stops at `LudoWalletHooks`, a
plain MonoBehaviour that knows nothing about `LudoBoardLogic`/`LudoVictoryScreenController` beyond the two
`.Invoke()` calls a human still needs to add there.

### New files
```
Assets/_TangentLudoEmpire/Phase3/Bridge/
  WalletGameBridge.cs      static class (Task 1). TryEnterGame(fee) -> MoneyWallet.DeductFunds("ENTRY_FEE");
                           ReportWin(amount) -> MoneyWallet.AddFunds(amount, "GAME_WIN"). Both fire an
                           event first (OnGameEntryRequested / OnGameWinReported) so a listener can react
                           without polling. Null-MoneyWallet is a logged no-op, never a throw.
  GameWalletConnector.cs   MonoBehaviour singleton (Task 2). OnEnable subscribes to
                           MoneyWallet.OnBalanceChanged (generic hook point) plus WalletGameBridge's two
                           events. ShowLowBalancePopup(needed) - self-building popup with a Deposit CTA
                           into WalletUI; ShowWinToast(amount) - one line via the existing Phase 1
                           ToastManager. Auto-triggered: OnGameEntryRequested checks the PRE-deduction
                           balance (fires before DeductFunds runs) and shows the popup if short.
  LudoWalletHooks.cs       MonoBehaviour (Task 3) + a small DecimalUnityEvent subclass. Two public
                           UnityEvent<decimal> fields (OnEntryFeeRequired, OnWinAmount) for the Ludo team
                           to Invoke() from inside the forbidden files. See "Spec correction" below for
                           why they self-wire to WalletGameBridge in code rather than via the Inspector.
```

### Changed
- `Assets/_TangentLudoEmpire/Phase3/Wallet/WalletGameBridge.cs` (the Phase 3 scaffold - a MonoBehaviour
  singleton with instance forwarders) — **deleted**, superseded by the static class above. Nothing else
  referenced it except `GameServices.cs`'s bootstrap line.
- `Phase3/Wallet/MoneyWallet.cs` — removed the now-dead `OnGameWin`/`OnGameEntry` convenience methods (the
  deleted bridge's only callers); `WalletGameBridge.TryEnterGame`/`ReportWin` call
  `DeductFunds`/`AddFunds` directly instead.
- `Managers/GameServices.cs` — bootstraps `GameWalletConnector` right after `MoneyWallet` (was: the old
  `WalletGameBridge` MonoBehaviour, which no longer exists).

### Spec correction: `WalletGameBridge` can't be Inspector-wired, so `LudoWalletHooks` self-wires in code
Task 3 asked to "Link OnEntryFeeRequired -> WalletGameBridge.TryEnterGame" in the Inspector. That's not
possible: Unity's `UnityEvent` persistent-listener picker only lists **instance** methods on a dragged
`Object` reference (Component/ScriptableObject) - it has nothing to bind to for a `static class`'s methods,
because there's no instance to drag into the target slot. `LudoWalletHooks.Awake()` calls
`OnEntryFeeRequired.AddListener(fee => WalletGameBridge.TryEnterGame(fee))` (and the same for
`OnWinAmount`/`ReportWin`) instead, so the wallet connection works immediately with zero Inspector setup;
the public UnityEvent fields stay open for the Ludo team's own extra listeners (SFX, UI, analytics) if
they want them - those are additive, not required.

Also: a raw `public UnityEvent<decimal>` field doesn't get a proper Inspector listener list without a
concrete `[Serializable]` subclass (a standard Unity gotcha) - added `DecimalUnityEvent : UnityEvent<decimal>`
and used that as the field type instead.

### Verification (headless, Editor)
- **0 compile errors.**
- **`Test Game Flow` (Task 4): 4 passed, 0 failed** — starting balance 100.00 -> `TryEnterGame(20)`
  succeeds -> balance 80.00 -> `ReportWin(40)` -> final balance **120.00**, exactly as specified. Play mode
  drives the real `WalletGameBridge`/`MoneyWallet` (starting balance forced to 100.00 via the existing
  `Editor_Reset`/`Editor_GrantBonus` test hooks so the run is reproducible); Edit mode (this headless run)
  simulates the same two operations against the encrypted wallet file directly, per the pattern every
  Phase 3.x headless verifier uses.

### What headless verification does NOT cover
`WalletGameBridge`'s events (`OnGameEntryRequested`/`OnGameWinReported`) and therefore
`GameWalletConnector`'s low-balance popup / win toast are only exercised by the **Play-mode** branch of
`Test Game Flow` (or real gameplay) - the Edit-mode file simulation never touches the live singletons or
fires any event. Press Play and run `Test Game Flow` (or `WalletGameBridge.TryEnterGame`/`ReportWin`
directly) to see the popup/toast fire for real.

### Deferred (manual steps for the Ludo team - the two files are `LudoEmpire.Ludo`, off-limits here)
1. Drag `LudoWalletHooks` onto the `LudoManager` empty object (or another persistent object alive for the
   whole match, e.g. in `SampleScene`).
2. In `LudoBoardLogic.StartGame()`: `FindAnyObjectByType<LudoWalletHooks>()?.OnEntryFeeRequired.Invoke(entryFee);`
3. In `LudoVictoryScreenController.ShowWin()`: `FindAnyObjectByType<LudoWalletHooks>()?.OnWinAmount.Invoke(winAmount);`
4. (optional) Inspector: add extra listeners to either event for UI/SFX - the wallet connection itself
   doesn't need this, see the Spec correction above.

---

## PHASE 3.5 DONE — Cloud Webhook + Deep Link Replace the Localhost Callback (2026-09-04, branch `feat/phase3.5-webhook-deeplink`)

`PaymentCallbackListener` (Phase 3.2) only ever worked in the Editor/Standalone - a real phone's browser
cannot reach `localhost` back into this app's process. This phase adds the path that actually works on a
shipped Android build: the gateway's browser redirect hits **your backend** (a public HTTPS endpoint),
which verifies it server-side and bounces the browser to a `tle://callback?...` **deep link**, which
Android hands to the app via an intent-filter. `PaymentCallbackListener` is NOT deleted - see "Why kept."

### New files
```
Assets/_TangentLudoEmpire/Phase3/Payments/DeepLinkManager.cs   MonoBehaviour singleton. Subscribes to
                                                                 Application.deepLinkActivated (+ checks
                                                                 Application.absoluteURL in Start() for a
                                                                 cold start). Parses tle://callback?orderId=
                                                                 ...&status=... (reuses PaymentCallbackListener.
                                                                 ParseQuery), logs "DEEP LINK RECEIVED.
                                                                 Crediting..." (Task 6), then resolves the
                                                                 payment - see "Cold start vs. live app" below.
Assets/Plugins/Android/AndroidManifest.xml                     Custom main manifest (Task 5) - see its own
                                                                 header comment for what it is and why.
BACKEND_WEBHOOK_SPEC.md                                        POST /api/payment/callback spec for the
                                                                 backend team (Task 4) - hash verification
                                                                 (same math as the client), the deep-link
                                                                 redirect + FCM-fallback dual channel, and a
                                                                 security section explaining why neither
                                                                 channel is trusted as proof by itself.
```

### Changed
- `Phase3/Payments/PaymentConfig.cs` — added `WebhookBaseURL` (default the literal placeholder
  `https://your-api.tangentludo.com`) and `DeepLinkScheme` (default `tle`), plus a computed
  `HasRealWebhook` flag gateways use to pick a `pp_ReturnURL`/`postBackURL`.
- `JazzCashGateway.cs` / `EasypaisaGateway.cs` — `Deposit()` now sets `pp_ReturnURL`/`postBackURL` to
  `{WebhookBaseURL}/api/payment/callback` once `HasRealWebhook` is true, falling back to
  `PaymentCallbackListener.CallbackUrl` until then (see "Why kept" below); added
  `pp_MerchantTxnRef = orderId` (JazzCash) and `pp_ReturnURL_Backup` / `postBackURL_Backup` =
  `{DeepLinkScheme}://callback` (both gateways) - the base URL the backend appends `?orderId=&status=` to
  when it redirects the browser to the app (see `BACKEND_WEBHOOK_SPEC.md` step 6).
- `Phase3/Payments/WebViewManager.cs` — `HandleCallback` (private, `PaymentCallbackListener.OnPaymentComplete`'s
  handler) split into a public `ResolvePayment(orderId, success, txnId, amount)` + a public
  `HasPendingFlow(orderId)`, so `DeepLinkManager` can drive/query the same pending-flow resolution a local
  callback would, without a second parallel code path.
- `Phase3/Wallet/MoneyWallet.cs` — `ConfirmPendingDeposit` gained a no-amount overload
  (`ConfirmPendingDeposit(pendingTxId, done?)`) for `DeepLinkManager`'s cold-start path, where the deep
  link genuinely carries no amount field - the pending row's own recorded amount is credited either way
  (unchanged from Phase 3.2), this overload just skips the mismatch-warning log that comparing against an
  unknown value would otherwise spam.
- `Managers/GameServices.cs` — bootstraps `WebViewManager`, `PaymentCallbackListener`, `DeepLinkManager`
  (was: nothing - see **bug fix** below), and adds `DeepLinkManager`.

### Bug fix found while building this phase
`GameServices.EnsureManagers()` never had `Ensure<>` calls for `WebViewManager` or
`PaymentCallbackListener` since Phase 3.2 added them - nothing ever spawned those singletons. Every real
WebView deposit would have thrown a `NullReferenceException` the moment `JazzCashGateway`/
`EasypaisaGateway.Deposit` called `WebViewManager.Instance.OpenPaymentUrl(...)` on a null reference. Found
by re-reading `GameServices.cs` while deciding where to bootstrap `DeepLinkManager`; fixed alongside it.

### Cold start vs. live app (why `DeepLinkManager` doesn't just call `MoneyWallet` directly)
Two different situations reach `OnDeepLink`, and conflating them either double-credits or shows a wrong
"failed" UI:
- **App stayed alive** through the whole browser round trip: `WebViewManager` still has the pending flow
  in memory. `DeepLinkManager` resolves through `WebViewManager.ResolvePayment`, which runs the SAME
  gateway-closure path a local callback would (`MoneyWallet.ConfirmPendingDeposit` fires exactly once,
  `DepositPopup`'s own callback updates the UI correctly).
- **Cold start** (app was backgrounded/killed during the browser flow, then relaunched by the deep link):
  `WebViewManager`'s in-memory `_pending` dictionary is gone, but the wallet's on-disk `Pending`
  transaction row is still there. `DeepLinkManager` calls `MoneyWallet.ConfirmPendingDeposit` directly in
  this case - there's no UI waiting to update anyway (the popup that opened it no longer exists).
`WebViewManager.HasPendingFlow(orderId)` is the switch between the two. Calling `ConfirmPendingDeposit`
from BOTH paths for the same order would still be *safe* (its idempotency guard makes the second call a
no-op - see Phase 3.2) but would incorrectly report `credited=false` to whichever caller lost the race,
which is why the paths are chosen explicitly rather than just "always do both."

### Why `PaymentCallbackListener` was kept, not deleted
The Phase 3.5 goal says "replace" - interpreted as *replace it as the production mechanism*, not delete
working, still-useful code. It's still the only thing that lets a deposit resolve **without a deployed
backend**, which is exactly this project's current state (`WebhookBaseURL` is still the placeholder - no
backend exists yet). `PaymentConfig.HasRealWebhook` is the switch: gateways point at the cloud webhook the
moment a real `WebhookBaseURL` is configured, and keep using the local listener for Editor/Standalone
testing until then. Once a backend is deployed, `PaymentCallbackListener` simply stops being hit by a real
gateway - no code deletion needed to "retire" it.

### `Assets/Plugins/Android/AndroidManifest.xml` (Task 5) - built from Unity's own output, not from scratch
Enabling a custom main manifest (`ProjectSettings.asset: useCustomMainManifest: 1`) means Unity stops
auto-generating `unityLibrary`'s manifest entirely - a hand-authored replacement that got `UnityPlayerGameActivity`'s
attributes or the launcher `<intent-filter>` even slightly wrong could reproduce the exact "app won't launch"
crash class this project already had to fix once (see Phase 1's `level0 corrupted` writeup). To avoid that
risk, the base content is copied **verbatim** from Unity 6000.5.4f1's own last successful auto-generated
`unityLibrary/src/main/AndroidManifest.xml` for this project (captured from a completed `BuildDebugApk`
run's `Library/Bee/...` output) - the only addition is one new `<intent-filter>` for `tle://callback`
inside the existing activity. See the file's own header comment for the full explanation and the
"keep in sync with `PaymentConfig.DeepLinkScheme`" note.

### Verification
- **0 compile errors.**
- **`Test DeepLink` (Task 6)**: dispatches `DeepLinkManager.OnDeepLink` directly with
  `tle://callback?orderId=TLE_test_123&status=000` (no headless equivalent of a real OS Intent exists, so
  this exercises the identical parse/dispatch code a real one would hit). Console shows exactly
  **"DEEP LINK RECEIVED. Crediting..."**, then (correctly, since `TLE_test_123` has no live wallet/pending
  row in this synthetic run) "MoneyWallet not available yet."
- **IL2CPP Android build with the new custom manifest**: `Result: Succeeded, Errors: 0` (`AndroidLudo3D.apk`
  built, ~13 min). One harmless advisory warning ("Unity is trying to add `uses-permission#INTERNET` but
  it is already declared by the user...") - expected, since the custom manifest already carries the
  permission Unity copied it from. Inspected the actual packaged manifest inside the build output
  (`.../unityLibrary/build/intermediates/merged_manifest/debug/.../AndroidManifest.xml`) directly: the
  `tle://callback` `<intent-filter>` (`action.VIEW`, `data android:scheme="tle" android:host="callback"`)
  is genuinely present in the final merged manifest, not just accepted without error.

### Deferred
- **No backend exists yet.** `WebhookBaseURL` stays the placeholder until Task 4's spec is implemented and
  deployed; until then every real deposit still resolves through the local `PaymentCallbackListener`
  (Editor/Standalone only - Android has no working deposit-confirmation path until a backend is live).
- **FCM push fallback is spec-only.** `BACKEND_WEBHOOK_SPEC.md` describes it (step 7); no Unity-side FCM
  receiver was built - that's a real SDK integration (Firebase), a separate scoped task, not implied by
  "add a deep link manager."
- **`DeepLinkScheme` isn't read at build time.** The manifest's `android:scheme="tle"` is a static string
  that must be kept in sync by hand with `PaymentConfig.DeepLinkScheme` if that value ever changes - noted
  in the manifest file's own comment, not automated in this phase.

---

## PHASE 3 COMPLETE — Withdraw, KYC, Admin, Anti-Fraud (2026-09-04, branch `feat/phase3.3-withdraw-kyc-admin`)

Closes the real-money loop: a player can now be identity-verified, withdraw (auto-payout under a
threshold, admin-approved above it), get rejected/refunded, and be rate/amount-limited - all through the
same encrypted-file + anti-cheat-gate architecture every earlier Phase 3.x piece already established.

### New files
```
Assets/_TangentLudoEmpire/Phase3/Kyc/
  KycData.cs            [Serializable] PER-PLAYER data: IsVerified, CNIC, PhoneNumber, VerifiedAt.
                         MaskedCnic (last 4 digits only) - never log a full CNIC, same rule as salts/passwords.
  KycManager.cs          MonoBehaviour singleton owning KycData, persisted via SaveEncrypted<KycData>
                         at tle_kyc_v1.enc. CanWithdraw() => IsVerified (Task 1).
Assets/_TangentLudoEmpire/Phase3/Wallet/
  WithdrawRequest.cs     WithdrawStatus{Pending,Approved,Rejected,Paid}, WithdrawResult{Rejected,Pending,
                         Paid} (what a caller of RequestWithdraw needs to know), the request record itself
                         (RequestId, Amount, Status, CreatedAt, Provider, Account, PayoutRef, Note).
Assets/_TangentLudoEmpire/Phase3/Payments/
  IPayoutGateway.cs      Cash-OUT counterpart to IPaymentGateway - Payout(amount, account, callback).
  JazzCashPayoutMock.cs  Sandbox mock disbursement: 5s delay, 95% success, "PAYOUT_JC_<n>" (Task 3).
  EasypaisaPayoutMock.cs Same shape, "PAYOUT_EP_<n>".
  PayoutGatewayFactory.cs GetGateway(PaymentProvider) - mirrors PaymentGatewayFactory, reuses the same enum.
Assets/_TangentLudoEmpire/Services/AntiFraudManager.cs
                         MonoBehaviour singleton (Task 4). CanTransact(type, amount) - PURE pre-flight
                         check (no side effects); RecordTransact(type, amount) - call only after the
                         transaction actually succeeded (see "Two-step check/record" below). Its own
                         encrypted file (tle_fraud_v1.enc) for per-UTC-day decimal totals
                         (DailyWithdrawLimit 50000, DailyDepositLimit 200000); an in-memory sliding
                         window (same Queue<float> pattern as AntiCheatManager.CheckSpeedHack) for
                         MaxTxPerHour (10).
Assets/_TangentLudoEmpire/Editor/AdminDashboard.cs
                         EditorWindow (Task 5), Tangent Ludo Empire/Admin/Dashboard. Pending KYC (Approve/
                         Revoke), Pending Withdraw Requests (Approve/Reject), View Ledger foldout. Play
                         mode drives the live MoneyWallet/KycManager; Edit mode resolves instantly against
                         the encrypted file, like every other Phase 3.x Editor tool - see "Scope" below.
```

### Changed
- `Transaction.cs` — added `TxType.Refund` (a rejected/failed withdrawal credits the reserved amount
  back; without its own type it would mis-tag the ledger-sum integrity math).
- `WalletData.cs` — added `List<WithdrawRequest> WithdrawRequests` (same file/envelope as `History` - one
  encrypted blob, one hash covering both) and folded it into `IntegrityString()`.
- `MoneyWallet.cs` — `DeductFunds` gained an optional `gatewayRef` param (so a withdraw request's
  Transaction cross-references its `WithdrawRequest.RequestId`); new `RequestWithdraw`, `ApproveWithdraw`,
  `RejectWithdraw`, `GetWithdrawRequests()` - see "Withdraw lifecycle" below.
- `PaymentConfig.cs` — Task 7: `IsProductionMode` (false = force sandbox, the single source of truth);
  `JazzCash_LiveUrl` / `Easypaisa_LiveUrl` (placeholder-but-real-shaped, flagged VERIFY-before-go-live);
  `UseSandbox` changed from an independently-editable field to `=> !IsProductionMode` (see "Config
  consolidation" below).
- `JazzCashGateway.cs` / `EasypaisaGateway.cs` — `Deposit` now picks the sandbox or live base URL from
  `IsProductionMode`.
- `WithdrawPopup.cs` — replaced the Phase 3 mock-gateway version. Task 6 flow (Check KYC -> Check
  AntiFraud -> Check Balance -> RequestWithdraw), provider dropdown added (mirrors `DepositPopup`).
- `GameServices.cs` — bootstraps `KycManager` + `AntiFraudManager`.
- `Editor/Phase3Tools.cs` — `ReadWallet`/`WriteWallet`/`LoadConfig`/`WalletPath`/`TryDelete` widened from
  `private` to `internal` (same-assembly reuse for `AdminDashboard`, no physical extraction needed - see
  "Reuse" below); generalised `ReadWallet`/`WriteWallet` into `ReadEncrypted<T>`/`WriteEncrypted<T>` so the
  same crypto path now serves the KYC/fraud files too; **Run Withdraw Test** (Task 8).

### Spec correction: `KYCManager` split into data + manager (same category error as SecurityConfig/PaymentConfig)
Task 1 asked for a ScriptableObject named `KYCManager` holding `IsVerified`/`CNIC`/`PhoneNumber`/
`VerifiedAt`. A ScriptableObject asset is ONE thing shared by the whole project (right for
`PaymentConfig`/`SecurityConfig` - app-wide config) - it cannot hold a different value per player, which
is exactly what KYC status is. Same split as `MoneyWallet`/`WalletData`: `KycManager` (MonoBehaviour
singleton, owns the runtime state) + `KycData` ([Serializable], the per-player persisted record).

### Withdraw lifecycle - why the balance is deducted at REQUEST time, not at approval/payout time
`RequestWithdraw` calls `DeductFunds` immediately, before the request's fate is known. Without that, a
player could submit a second (or third...) &gt;10000 request for the same money while the first one still
sits Pending awaiting admin review - nothing would stop the balance being double-spent across multiple
simultaneously-Pending requests. Reserving it upfront means:
- **Paid** (auto-payout, or admin-approved and the gateway succeeds): balance stays reduced - the money
  genuinely left.
- **Rejected** (admin reject, or the payout gateway itself fails after approval): `RefundReservedAmount`
  credits it straight back as a `TxType.Refund` transaction.
Task 2's threshold (`> 10000` stays Pending) only decides who authorises the payout - it was never a gate
on whether the funds get reserved.

### Two-step check/record (`AntiFraudManager`) - and the bug this caught
`CanTransact` is a pure read; `RecordTransact` is called only once the transaction the check gated has
actually gone through. If `CanTransact` recorded usage itself, a withdrawal that PASSED the fraud check
but then failed for an unrelated reason (insufficient balance, RBAC, a declined payout) would have wrongly
eaten into the daily limit / hourly count for money that never moved.

Building this surfaced a real bug, caught by `Run Withdraw Test` itself: `AntiFraudManager.Awake()` set up
its data via `LoadOrCreate()`, but a component created with `AddComponent&lt;T&gt;()` and used on the very
next line (exactly how the headless test spins up a throwaway instance) is not reliably guaranteed to have
finished `Awake()` first in every Editor-mode context - the first test run threw a real
`NullReferenceException` in `RollDayIfNeeded`. Fixed by making every method that touches the manager's
data call a lazy `EnsureData()` guard instead of trusting `Awake()` already ran; re-ran the test clean
afterward. Left in `MIGRATION_NOTES.md` rather than silently fixed, because it's a real, general
Unity-lifecycle gotcha worth remembering for any future Editor-mode `AddComponent`-and-immediately-use test.

### Config consolidation (Task 7)
`UseSandbox` (Phase 3.1) and the new `IsProductionMode` are the same underlying concept - keeping both as
independently-editable serialized fields would let them disagree (`UseSandbox=true` AND
`IsProductionMode=true` set at once is nonsensical, and nothing would stop it). `IsProductionMode` is now
the only serialized flag; `UseSandbox` became a computed `=> !IsProductionMode` so any code still reading
it (existing log lines) keeps compiling and can never see a contradiction.

### `AdminDashboard`'s scope - local dev tool, not a production admin panel
It reads/writes the SAME local encrypted files every other Phase 3.x Editor tool does. A real production
admin panel needs the backend from `BACKEND_WEBHOOK_SPEC.md` plus a proper multi-user data store (every
player's requests, admin auth/roles) - none of which exists yet, and building a database-backed panel is
well outside what an `EditorWindow` can do. This is the dev-loop equivalent, for the same reason
`Phase3Tools`' menu items are.

### Verification (headless, Editor)
- **0 compile errors.**
- **`Run Withdraw Test` (Task 8): 15 passed, 0 failed** (after the `AntiFraudManager` fix above) - KYC
  verified; deposit 100 -> 100.00; withdraw 50 (auto-payout, no admin needed) -> **balance 50.00** (Task
  8's literal end state); withdraw 15000 (>10000) queued Pending then admin-approved -> Paid, balance
  unchanged by the approval itself; withdraw 12000 admin-rejected -> refunded correctly;
  `AntiFraudManager` allows a fresh-day transaction and blocks once `DailyWithdrawLimit` is exceeded;
  final integrity check: envelope hash verified, balance == ledger sum (15050.00, 6 transactions).
- Grepped the verify log for the test's own CNIC value in full - zero hits outside the masked form,
  confirming `KycManager`/`KycData` never log it unredacted.

### Spec correction: Task 8's literal flow vs. Task 2's own threshold rule
Task 8 says "Request Withdraw 50 -> Approve in Admin -> Balance 50", but Task 2's own rule is "amount >
10000 -> PENDING else call Payout" - 50 is far under that threshold, so a real `RequestWithdraw(50)`
auto-pays immediately and never reaches `AdminDashboard` at all. Implemented what Task 2 actually
specifies (50 auto-pays, no admin click) rather than forcing an artificial admin step for an amount that
doesn't need one; the literal end state Task 8 asks for - balance 50.00 - is exactly what the test
verifies. The Pending -> Approve -> Paid and Pending -> Reject -> refund paths get their own real coverage
in the same test run, using amounts that actually cross the threshold (15000, 12000).

### Deferred
- **`IPayoutGateway`/the two mocks have no request-signing.** Unlike the deposit gateways
  (`GenerateSecureHash`/`ValidateCallbackHash`), a real disbursement API needs its own server-to-server
  auth - out of scope for a mock that only proves the wallet-side lifecycle.
- **No real KYC provider integration.** `KycManager.SetVerified` records a result; it doesn't call any
  identity-verification service. A production build needs a real backend-mediated KYC check (a document/
  biometric provider), never a client-only "trust the CNIC the player typed in."
- **`AdminDashboard` has no auth of its own.** Anyone with the Unity Editor open on this project can
  approve/reject withdrawals and grant KYC - fine for a solo-dev/QA tool, not acceptable once a real
  admin panel exists (which needs its own RBAC-gated login, per `Core/RBAC.cs`'s existing `Role` levels).
- **`JazzCash_LiveUrl`/`Easypaisa_LiveUrl` are best-effort placeholders**, not values confirmed against
  either provider's current live documentation (no verified access to that from this environment) -
  flagged in the fields' own tooltips; verify before ever flipping `IsProductionMode` on.

---

## PHASE 4.1-4.3 COMPLETE — Online Rooms, Tournaments, Leaderboard/Referral/History (2026-09-04, branch `feat/phase4.1-4.3-multiplayer-tournament-social`)

Three new systems, all mock-backed (no Firebase/Photon yet - every TODO below marks exactly where that
swap happens), all built on `WalletGameBridge`/`MoneyWallet` without touching a single file in
`LudoEmpire.Ludo`.

### New files
```
Assets/_TangentLudoEmpire/Phase4/
  Multiplayer/  (namespace TangentLudoEmpire.Multiplayer)
    RoomData.cs           RoomStatus{Waiting,Full,Started,Expired}; live, NOT persisted (see remarks).
    MockRoomBackend.cs     In-memory room list. TODO(Firebase/Photon): every method here becomes a real
                           async server call. SimulateNetworkDelayAsync = fire-and-forget Task.Delay(0.5s)
                           proof-of-concept (Task A.2), never blocks RoomManager's synchronous API.
    RoomManager.cs          CreateRoom/JoinRoom/LeaveRoom/GetAvailableRooms/CreateAndJoinRoom. JoinRoom
                           calls WalletGameBridge.TryEnterGame(fee) FIRST (Task A.1) - "Low Balance" on
                           denial. Refunds on room-leave (Waiting only) and room-expiry (120s unfilled).
    RoomGameBridge.cs       OnRoomFull (locks the room, audits - does NOT re-charge, see Task-A.4
                           correction below), OnGameEnd (WalletGameBridge.ReportWin for the winner).
    RoomUI.cs               Self-building ScrollView + fee Dropdown[10,20,50,100,500] + Create button,
                           5s refresh (Task A.3).
  Tournament/  (namespace TangentLudoEmpire.Tournament)
    TournamentConfig.cs     ScriptableObject (Task C's original ask - genuinely correct here, this really
                           is shared config: WinnerPercent 0.7 / RunnerUp 0.2 / PlatformFee 0.1).
    TournamentData.cs       TournamentStatus{Registering,Active,Completed}; live, not persisted.
    TournamentManager.cs    RegisterTournament (MoneyWallet.DeductFunds, spec said "RemoveFunds" - no such
                           method, DeductFunds is the real one) -> StartTournament at MaxPlayers (seeds a
                           4-pair Round-1 bracket) -> DeclareWinner (PrizePool * WinnerPercent from
                           config, + optional runner-up at RunnerUp% - the config reserves that slice, so
                           it's wired rather than left dead).
    TournamentUI.cs         Self-building panel: fee/pool/players/status + bracket view (Task B.2).
  Social/  (namespace TangentLudoEmpire.Social)
    PlayerStat.cs, LeaderboardManager.cs   tle_leaderboard_v1.enc. UpdateStats/GetTop10(type) - see
                           "Mock leaderboard padding" below for what GetTop10 actually returns.
    GameRecord.cs, GameHistoryManager.cs   tle_gamehistory_v1.enc, capped at 200 records (Task C.4).
    ReferralData.cs, ReferralManager.cs    tle_referral_v1.enc (not explicitly asked for, but a code that
                           forgot itself every launch would be useless). GenerateCode() = "TLE"+Random
                           (Task C.3); ApplyReferral(code) + first-successful-deposit detection (via
                           MoneyWallet.OnTransaction) pays the LOCAL player +50 - see remarks for why only
                           half the "give 50 to both" loop is possible here.
    LeaderboardUI.cs        3 tabs (Daily/Weekly/AllTime), GameHistoryUI.cs - popup list, last 20.
Assets/_TangentLudoEmpire/Phase4/Bridge/LudoMultiplayerHooks.cs  (namespace TangentLudoEmpire.Bridge)
    DecimalUnityEvent OnTournamentWin, reusing Phase 3.4's LudoWalletHooks self-wiring pattern.
Assets/_TangentLudoEmpire/Editor/Phase4Tools.cs
    Ensure Tournament Config; Run Full Test (Task D.2).
```

### Changed
- `Phase3/Wallet/MoneyWallet.cs` - `AddFunds`'s source-tagging extracted into `TagFor(source)`:
  `TOURNAMENT_WIN`/`ROOM_WIN` now tag `TxType.GameWin` (a tournament/room prize IS a game win, not a
  promotional Bonus); any `*_REFUND` source tags `TxType.Refund`.
- `Managers/GameServices.cs` - bootstraps `RoomManager`, `TournamentManager`, `GameHistoryManager` (before
  `LeaderboardManager` - it reads windowed history), `LeaderboardManager`, `ReferralManager`.
- **21 files** (every `Tangent*`-namespaced manager with a singleton `Awake()` - full list in the commit)
  - see "Systemic bug: DontDestroyOnLoad throws outside Play mode" below.

### Spec corrections
1. **`WalletGameBridge.ReportGameEntry`/`MoneyWallet.RemoveFunds` don't exist.** Both tasks referenced
   stale method names from an earlier draft of the API (Phase 3.4 shipped `TryEnterGame`/`ReportWin`;
   `MoneyWallet` has always had `DeductFunds`). Used the real, current methods throughout.
2. **Task A.4's "When Room Full: for each player call ReportGameEntry(fee)" would double-charge.** Task
   A.1 already charges every player their fee the moment they individually join. `RoomGameBridge.OnRoomFull`
   locks the room and audits who's in the match; it does not touch the wallet.
3. **"TournamentUI/LeaderboardUI : Canvas" is a category error** - `Canvas` is Unity's built-in rendering
   component, not a base class for game-logic scripts (same correction already applied to `WalletUI` etc.
   in Phase 3). Both are `MonoBehaviour`s that build their own Canvas via `WalletUiKit`.
4. **Task 8's "Approve in Admin" doesn't apply to a 50 withdrawal** (Phase 3.3's own precedent) - not
   relevant here, but the equivalent judgment call this phase: Task D.2's amounts (room win 40, tournament
   560) are exactly what the config-driven math produces, verified as the literal numbers, not adjusted.

### A real design gap found while building: who pays for a room that never fills?
Task A.1 charges the entry fee the instant a player joins; Task A.2 auto-removes an unfilled room after
120s. Nothing in the spec said what happens to the money already charged to whoever was waiting in it.
`RoomManager` refunds every local player in an expired room (`ROOM_EXPIRED_REFUND`, `TxType.Refund`) -
the same "reserve then refund-on-non-completion" pattern `MoneyWallet.RequestWithdraw`/`RejectWithdraw`
already established in Phase 3.3.

### Bug found (and fixed) by the headless test: the host was never actually charged
`RoomData.New` originally auto-seated the host in `PlayerIds` at creation. When the host then called
`JoinRoom` (Task A.1's literal flow), it hit the "already in this room" idempotency guard - the one
correctly there to stop a SECOND real join from double-charging - and returned success WITHOUT ever
calling `WalletGameBridge.TryEnterGame`. Fixed: `CreateRoom` no longer seats anyone; the host pays via a
real `JoinRoom` call exactly like every other player (a new `CreateAndJoinRoom` convenience wraps both for
the common UI flow, used by `RoomUI.OnCreateClicked`).

### Systemic bug found (and fixed) across 21 files: `DontDestroyOnLoad` throws outside Play mode
Every Tangent-namespaced manager's `Awake()` starts with `Instance = this; DontDestroyOnLoad(gameObject);`.
Unity's `DontDestroyOnLoad` **throws `InvalidOperationException`** ("can only be used in play mode... " +
"cannot be part of an editor script") when called from Editor-mode code - which aborts every line AFTER
it in that same `Awake()` call. For `RoomManager` specifically, that meant its
`MockRoomBackend.OnRoomExpired += HandleRoomExpired;` subscription (the line right after
`DontDestroyOnLoad`) never registered, so the room-expiry refund silently never fired - caught by
`Run Full Test`'s "expired room refunds its entry fee" check. Fixed everywhere at once: every
`DontDestroyOnLoad(gameObject);` call in a `Tangent*`-namespace file is now
`if (Application.isPlaying) DontDestroyOnLoad(gameObject);` (a no-op change in real Play mode -
`Application.isPlaying` is always true there - and lets the rest of `Awake()` run in Editor/test contexts).
**`Managers/FlowManager.cs` was deliberately left untouched** - it's `namespace LudoEmpire.Ludo`, off-limits.

### A second, related discovery: `AddComponent<T>()`'s `Awake()` isn't reliably synchronous in this exact
### headless `-executeMethod` context either
Confirmed by direct evidence: `MoneyWallet.Instance` was still `null` after spawning a `MoneyWallet` via
`AddComponent` AND running several subsequent lines of test code - `ReferenceEquals(MoneyWallet.Instance,
wallet)` came back `false`. (Calling `Editor_Reset()`/`Editor_GrantBonus()` on the direct C# reference
still worked regardless, which is why earlier Phase 3.x tests that only ever held direct references never
surfaced this - `WalletGameBridge.TryEnterGame`, which can only find the wallet via the STATIC
`MoneyWallet.Instance`, is what exposed it.) `Phase4Tools.RunFullTest`'s `Spawn<T>()` helper now forces
each `Awake()` via `gameObject.SendMessage("Awake", SendMessageOptions.DontRequireReceiver)` right after
`AddComponent` - safe/idempotent even if Awake already ran for real, since every manager's singleton guard
(`if (Instance != null && Instance != this)`) makes a second call to the same instance a no-op past that
line. This is a test-harness-only fix (`Phase4Tools.cs`), not a runtime code change - real gameplay always
runs in actual Play mode, where this class of issue doesn't arise.

### Mock leaderboard padding - and why it's fine
`LeaderboardManager.GetTop10` can only ever know the LOCAL player's real stats (no backend, no other
players' data reaches this device). It returns that one real entry plus 9 clearly-fake, deterministic
`Player_####` rows with no money behind them, purely so the UI has something to demo. **These are never
presented as real other-player data** - flagged in the method's own doc comment. TODO(Firebase/backend): a
real leaderboard needs a live, server-side, time-windowed query across every player.

### Referral - only half the loop is possible locally
"On first deposit of friend, give 50 to both" (Task C.3) - this device can only ever credit ITS OWN
wallet. `ReferralManager` pays the local player's +50 on their own first successful deposit (detected via
`MoneyWallet.OnTransaction`, already existed) if they'd applied a friend's code. The REFERRER's +50 needs
a backend call to a different account entirely - TODO(Firebase/backend), documented in the class's own
remarks, not built here.

### Verification (headless, Editor)
- **0 compile errors** (after fixing 3 real compile errors the first run caught: `RoomManager.cs` missing
  `using TangentLudoEmpire.Wallet;`, `GameHistoryManager.GetHistory()`'s `List<T> ?? T[]` type mismatch,
  `TournamentUI.OnRegisterClicked`'s use-of-unassigned-variable from a short-circuited `&&`).
- **`Run Full Test` (Task D.2): 17 passed, 0 failed**, after the two bug-fix rounds above:
  - Test 1 (Room): create 20-fee room -> join deducts to 980.00 -> win 40 credits to 1020.00 -> leaderboard
    (1 win/1 game/40.00) and history (1 record) both updated -> local player's real stat appears in
    `GetTop10` alongside the mock padding -> a separate 15-fee room, force-expired, refunds correctly.
  - Test 2 (Tournament): register 100 -> 920.00 (1020 - 100, the expired room's join+refund net to zero) ->
    7 mock players fill the tournament -> auto-starts, **PrizePool exactly 800.00** (8 x 100) -> 4-pair
    Round-1 bracket seeded -> `DeclareWinner` credits **exactly 560.00** (800 x 0.7 `WinnerPercent`) ->
    balance 1480.00, tournament marked Completed.
- Both real bugs above were caught BY this same test, not found by inspection - exactly what it's for.

### Deferred
- **No Unity-side Firebase/Photon integration anywhere** - every manager here is the mock/local half of a
  real multiplayer backend; each file's own TODO comments mark precisely where a real SDK call replaces
  the mock logic. This was explicit in the task ("Backend: Use Mock for now").
- **`LudoMultiplayerHooks`'s two manual wiring steps** (drag onto `LudoManager`, invoke from the Victory
  Screen's tournament-win path) are the same kind of deferred, human, `LudoEmpire.Ludo`-adjacent step as
  `LudoWalletHooks`'s from Phase 3.4 - documented in the file's own class remarks.
- **Room/Tournament state is not persisted.** A player who force-quits mid-room loses that room entirely
  (their reserved fee is stuck as a `TxType.Withdraw`-tagged deduction with no matching refund/win, since
  `MockRoomBackend`'s 120s expiry timer is itself in-memory and resets on restart). A real backend
  reconciles this the same way `BACKEND_WEBHOOK_SPEC.md` already reconciles stuck payment orders - out of
  scope for a mock.

## PHASE 5 COMPLETE — Anti-Cheat, Firebase, Analytics, Polish (2026-09-04, branch `feat/phase5-security-firebase-polish`)

Production-readiness pass: server-authoritative gameplay anti-cheat wired to a real account freeze, a
mock Firebase Analytics/Crashlytics facade (no SDK installed - every real-SDK call site marked TODO), a
loading screen + expanded sound system + legal pages + settings, and a Mock/Production build-config
switch. All additive, all outside `LudoEmpire.Ludo`.

### Naming/spec collisions resolved before writing code (documented here, not silently papered over)
- **`AntiCheatManager` → `GameplayAntiCheatManager`.** `TangentLudoEmpire.Services.AntiCheatManager`
  already existed since Phase 2 (payment-callback verification, wallet-drift reconciliation, ad-cap). That
  one is about **payments**; Phase 5's ask is about **gameplay** (dice rolls, moves, per-move timing) -
  genuinely different concerns, same "rename rather than collide" call as `MoneyWallet` vs the pre-existing
  coin `WalletManager` back in Phase 3.
- **`SecurityConfig.asset`** was asked for again as if new - it's the Phase 2 `Core/SecurityConfig.cs`
  asset. Extended in place with three fields (`EnableAntiCheat`, `MinMoveTime`, `EnforceServerRoll`)
  instead of creating a second, colliding `SecurityConfig.asset`.
- **`WalletGameBridge`** was asked to gain `ReportSuspiciousActivity`/`OnWithdrawSuccess` as if they were
  pre-existing members - they weren't; added to the real Phase 3.4 `Bridge/WalletGameBridge.cs`.
- **"AntiCheatManager.ValidateAd/ValidatePayment/CheckSpeedHack"** are already covered by the Phase 2 class
  above under a *payment* speed-hack (`dailyAdCap`, `speedHackGameLimit`); the new
  `SecurityConfig.MinMoveTime`/`EnforceServerRoll` fields are a distinct *gameplay* concept and were kept
  separate rather than overloading the existing payment-side tolerances.
- **Firebase/Crashlytics SDK is not installed** in this project (no Firebase package in
  `Packages/manifest.json`, no `google-services.json`). Rather than touch `manifest.json` without any way
  to verify a package resolve headlessly, `FirebaseManager`/`CrashlyticsManager` are MOCK FACADES with the
  exact public method surface a real integration would have - every real-SDK call site is a `// TODO(real
  SDK): ...` comment showing precisely what line to swap in. Both log to console, an in-memory queryable
  list (for tests/AnalyticsBridge), and a local `.txt` file.
- **"AnalyticsBridge hooks WalletGameBridge.OnGameEntry/OnWin/OnWithdrawSuccess" for 5 events** ("game_start",
  "game_win", "deposit", "withdraw", "tournament_join") - only 3 of those 5 map onto a `WalletGameBridge`
  member. Rather than inventing `deposit`/`tournament_join` events on `WalletGameBridge` that nothing else
  needed, `AnalyticsBridge` also subscribes to the pre-existing `MoneyWallet.OnTransaction` (fires on every
  successful deposit already) and a new `TournamentManager.OnPlayerRegistered` event (added this phase,
  fired once per successful, non-idempotent tournament registration).
- **`SoundManager.cs`/`LoadingScreenManager.cs : Canvas`/`LegalPagesUI.cs : Canvas`** - `Canvas` is Unity's
  rendering component, not a base class for game scripts (same correction already applied to
  `TournamentUI`/`DepositPopup`/every other "X : Canvas" ask). `SoundManager` was **extended in place**
  (Phase 1's `Managers/SoundManager.cs`, not a second colliding class); `LoadingScreenManager`/
  `LegalPagesUI` are new self-building `MonoBehaviour`s via the shared `WalletUiKit` toolkit.
- **`SettingsManager.cs : PlayerPrefs wrapper`** - the one deliberate, narrow exception to this project's
  "no PlayerPrefs for financial/progression data" rule: `SoundVolume`/`MusicVolume`/`NotificationsOn` are
  pure, non-exploitable client UI preferences, not money or progression. Documented in the file's own
  class remarks so the exception stays visible, not silently normalized.
- **`BuildConfigManager.IsProduction`** is deliberately **narrower** than the pre-existing
  `TangentLudoEmpire.Payments.PaymentConfig.IsProductionMode` (which already switches JazzCash/Easypaisa
  sandbox vs. live). The new flag gates a generic backend API base URL and the `PRODUCTION_BUILD`
  scripting define; unifying the two flags is flagged as deferred tech debt below rather than risking a
  silent behavior change to the payment gateways as a side effect of this task.
- **Test 4's "Win Game 100 → Verify Analytics Event 'game_win' with prize 90"** implies deriving 90 from
  100, but nothing in this phase (or any prior one) defines a win-side fee/rake - inventing one here would
  silently add an undocumented deduction to every future real game win. `Phase5Tools.RunSecurityTest`
  instead reports a 90 win directly (`WalletGameBridge.ReportWin(90m)`) and verifies the analytics event
  carries that same figure - the number is honoured, the unrequested math is not.

### New files
```
Assets/_TangentLudoEmpire/Phase5/
  Security/  (namespace TangentLudoEmpire.Security)
    GameplayAntiCheatManager.cs  Singleton. RequestRoll = mock server-authoritative dice (client never
                                 supplies the roll). ValidateMove = Rule 2 (delta must match the claimed
                                 dice value) + Rule 3 (speed hack: 2nd+ move <MinMoveTime after the last
                                 one for that player). Either violation: audits, appends a line to
                                 Application.persistentDataPath/tle_anticheat_log.txt,
                                 WalletGameBridge.ReportSuspiciousActivity(...), fires
                                 static OnCheatDetected. Not wired into LudoBoardLogic/LudoDiceRoller
                                 (off-limits) - a human adds a small hook component, same pattern as
                                 LudoWalletHooks, that forwards real roll/move calls here instead of
                                 trusting the client.
    WalletSecurityBridge.cs      Static. EnsureSubscribed() wires WalletGameBridge.OnSuspiciousActivity ->
                                 FreezeAccount (MoneyWallet.SetFrozen(true) + a self-built "Account Under
                                 Review" popup via WalletUiKit). UnfreezeAccount (RBAC.Demand(Staff, ...))
                                 is the (not spec'd, but necessary) inverse - a freeze with no recovery path
                                 would be a one-way trapdoor.
  Analytics/  (namespace TangentLudoEmpire.Analytics)
    FirebaseManager.cs           Singleton MOCK FACADE (see collision notes above). LogEvent/
                                 SetUserProperty log to console + tle_firebase_events_log.txt + an
                                 in-memory Events list (AnalyticsEventRecord, with a GetParam(key) test
                                 helper).
    CrashlyticsManager.cs        Static MOCK FACADE. LogException/SetUserId + auto-catch via
                                 Application.logMessageReceived (RuntimeInitializeOnLoadMethod - does NOT
                                 fire in a plain -batchmode -executeMethod run that never enters Play mode,
                                 so headless tools call the public EnsureHooked() directly instead. Logs to
                                 tle_crashlytics_log.txt).
    AnalyticsBridge.cs           Static. Hooks WalletGameBridge (OnGameEntryRequested -> "game_start",
                                 OnGameWinReported -> "game_win", OnWithdrawSuccess -> "withdraw"),
                                 MoneyWallet.OnTransaction (successful Deposit -> "deposit"), and
                                 TournamentManager.OnPlayerRegistered (-> "tournament_join") - see
                                 collision notes above for why the last two aren't on WalletGameBridge.
  Polish/  (namespace TangentLudoEmpire.Polish)
    LoadingScreenManager.cs      Singleton, self-building (WalletUiKit). Random Ludo tip + a real 0..1
                                 progress bar. ShowForSeconds(seconds, onComplete) times the screen against
                                 a wall-clock duration since Room/Tournament registration in this mock
                                 backend resolves synchronously (no real load time to bind progress to) -
                                 a real backend integration binds Show/SetProgress/Hide to its actual call.
    SettingsManager.cs           Static PlayerPrefs wrapper (see exception note above). SoundVolume/
                                 MusicVolume/NotificationsOn; the two volume setters call
                                 SoundManager.ApplySavedVolumes automatically.
    LegalPagesUI.cs               Self-building (WalletUiKit). 3 tabs (Terms/Privacy/Refund), each loading
                                 Resources/LegalText/*.txt as a TextAsset. Permanent "18+ AGE RESTRICTED /
                                 PLAY RESPONSIBLY" banner shown regardless of the active tab.
  BuildConfigManager.cs           ScriptableObject (namespace TangentLudoEmpire.Core - project-wide config,
                                 same home as SecurityConfig/PaymentConfig). IsProduction gates
                                 EffectiveApiBaseUrl (Mock*/Production* pair) - see scope note above.
Assets/_TangentLudoEmpire/Resources/
  BuildConfig.asset               Auto-created by Phase5Tools (IsProduction=false / mock URL).
  LegalText/
    TermsAndConditions.txt, PrivacyPolicy.txt, RefundPolicy.txt   Placeholder legal copy (counsel review
                                 required before Play Store submission), each already containing an
                                 18+/Play Responsibly section.
Assets/_TangentLudoEmpire/Editor/
  Phase5Tools.cs                  Tangent Ludo Empire/Phase 5/*: Ensure Build Config, Sync Production
                                 Define (toggles the PRODUCTION_BUILD Android scripting define to match
                                 BuildConfigManager.IsProduction), Run Security Test (Task D.2, below).
```

### Extended files
- `Core/SecurityConfig.cs` - `+EnableAntiCheat`, `+MinMoveTime`, `+EnforceServerRoll`.
- `Core/RBAC.cs` - `CanSpend()` now also denies when `MoneyWallet.Instance.IsFrozen`, alongside the
  pre-existing payment-side `AntiCheatManager.WalletLocked/Flagged` check - either gate alone is enough to
  deny a spend.
- `Phase3/Wallet/MoneyWallet.cs` - `+IsFrozen`/`+SetFrozen(bool, reason)` (in-memory only, NOT persisted -
  see Deferred below); `RunPayout`'s success branch now also calls
  `WalletGameBridge.ReportWithdrawSuccess(...)` for AnalyticsBridge.
- `Phase3/Bridge/WalletGameBridge.cs` - `+OnSuspiciousActivity`/`+ReportSuspiciousActivity`,
  `+OnWithdrawSuccess`/`+ReportWithdrawSuccess`.
- `Phase4/Tournament/TournamentManager.cs` - `+OnPlayerRegistered(playerId, fee, tournamentId)`, fired once
  per successful (non-idempotent) registration - Phase 5.2's `tournament_join` analytics hook.
- `Managers/SoundManager.cs` - `+SfxKey`/`+BgmKey` enums, `+RegisterClip(SfxKey,...)`, `+PlaySFX(SfxKey)`,
  `+PlayBgm(BgmKey)` (today a start/stop toggle on the single shared `LudoMusicManager` track - see its own
  TODO for real per-track switching), `+ApplySavedVolumes(sfx, music)` for `SettingsManager` to push into.
- `Managers/GameServices.cs` - `EnsureManagers()` now also spawns `GameplayAntiCheatManager` +
  `FirebaseManager`, and calls `AnalyticsBridge.EnsureSubscribed()` / `CrashlyticsManager.EnsureHooked()` /
  `SettingsManager.PushToSoundManager()`, in that order (after `MoneyWallet`/`TournamentManager` already
  exist earlier in the same method, so both events they subscribe to are available).

### Verification (headless, Editor)
- **0 compile errors** (only pre-existing `CS0618` obsolete-API warnings from before this phase).
- **`Run Security Test` (Task D.2): 4/4 tests PASS (8/8 individual checks)**:
  - Test 1 (illegal move): `ValidateMove("local", 0, 10, 3)` (delta 10 ≠ dice 3) rejected -> wallet frozen
    -> `tle_anticheat_log.txt` contains an `ILLEGAL_MOVE` entry.
  - Test 2 (speed hack): 3 back-to-back `ValidateMove` calls for the same player - move 1 passes
    (no prior timestamp), moves 2 & 3 both rejected (well under `MinMoveTime` = 0.3s) -> log contains a
    `SPEED_HACK` entry.
  - Test 3 (exception): a thrown `InvalidOperationException` passed to `CrashlyticsManager.LogException` ->
    `tle_crashlytics_log.txt` created containing the exception's message.
  - Test 4 (analytics): `WalletGameBridge.ReportWin(90m)` -> `FirebaseManager.Events` contains a
    `"game_win"` record whose `prize` parameter reads exactly `"90"`.
- No bugs found this round - the three lessons proactively applied from Phase 4 (`Application.isPlaying`
  guard on every `DontDestroyOnLoad`, lazy `EnsureConfig()`/`EnsureData()`-style guards, and the test
  harness's `SendMessage("Awake", ...)` right after every `AddComponent`) meant this phase's headless run
  passed clean on the first try.

### Production Checklist (Play Store)
- [ ] **Firebase**: install the real Firebase Unity SDK (Analytics + Crashlytics) and drop
      `google-services.json` into `Assets/`; swap the two `// TODO(real SDK)` call sites in
      `FirebaseManager.cs`/`CrashlyticsManager.cs`.
- [ ] **BuildConfigManager**: set `IsProduction = true` and fill in `ProductionApiBaseUrl` on the
      `Resources/BuildConfig.asset`; run `Tangent Ludo Empire/Phase 5/Sync Production Define`.
- [ ] **PaymentConfig**: separately set `IsProductionMode = true` + real JazzCash/Easypaisa live URLs
      (Phase 3.1) - not the same flag as `BuildConfigManager.IsProduction`, see scope note above.
- [ ] **SecurityConfig**: replace `localDataKeyBase64`/`kdfSaltBase64`/`hmacSecret` with real, per-build
      secrets (never reuse the dev defaults); set a real `apiBaseUrl`.
- [ ] **Legal pages**: replace the placeholder copy in `Resources/LegalText/*.txt` with counsel-reviewed
      Terms & Conditions / Privacy Policy / Refund Policy; confirm the 18+/Play Responsibly banner text
      meets the target store/region's real-money-gaming requirements.
- [ ] **Play Store real-money-gaming declaration**: real-money Ludo requires the Play Console's gambling
      questionnaire, applicable licensing per target country, and (depending on region) exclusion from
      certain storefronts - a business/legal step, not a code one.
- [ ] **google-services.json / Crashlytics dSYM-equivalent**: must ship in the release build, excluded
      from version control per Google's own guidance (or scoped to a private config repo).
- [ ] **Anti-cheat wiring**: connect `GameplayAntiCheatManager.RequestRoll`/`ValidateMove` to the real
      `LudoDiceRoller`/`LudoBoardLogic` call sites via a hook component (see the class's own remarks) -
      today nothing in `LudoEmpire.Ludo` calls into it yet.
- [ ] **IL2CPP + ARM64 release build**: confirm `PRODUCTION_BUILD` define is present, confirm
      `wipeOnTamper`/`requireServerConfirmationToLock` are set intentionally, and run a real on-device
      smoke test (headless Editor tests cover logic, not the actual Android build).
- [ ] **Sound**: register real `AudioClip`s for `SfxKey.{DiceRoll,TokenMove,Win,Lose}` and
      `BgmKey.{MainMenu,InGame}` (currently unregistered - `PlaySFX`/`PlayBgm` degrade to a logged no-op).

### Deferred
- **Account freeze is in-memory only, not persisted or server-side.** `MoneyWallet.IsFrozen` resets on
  app restart and does not follow the account to a re-login on another device. A real backend integration
  needs a server-side frozen flag that `RBAC.CanSpend()`/`WalletSecurityBridge` check instead of (or in
  addition to) the local one - not built here to avoid inventing a fake backend contract.
- **`BuildConfigManager.IsProduction` vs `PaymentConfig.IsProductionMode`** - two toggles that should
  probably be one; kept separate this phase specifically to avoid changing payment gateway behavior as an
  unrequested side effect (see collision notes above).
- **No real Firebase/Crashlytics SDK** - both are mock facades; every real-SDK call site is a `// TODO`
  comment naming the exact replacement call (see New Files above).
- **`SoundManager`'s `PlayBgm` is a start/stop toggle**, not real per-track switching - `LudoMusicManager`
  (do-not-touch) only ever drives one track. TODO(art pass) once real MainMenu/InGame clips exist.
- **`LoadingScreenManager` is not auto-wired to Room/Tournament join UI** - `Show()`/`SetProgress()`/
  `Hide()`/`ShowForSeconds()` are ready to call, but nothing calls them yet; today's mock room/tournament
  joins are synchronous, so there's no real load time to demonstrate against without a human wiring it to
  an actual UI flow or a real backend call.

## PHASE 6 COMPLETE — READY FOR UPLOAD (2026-09-04, branch `feat/phase6-production-release`)

Release-engineering pass: production PlayerSettings + a real signed AAB, a real release keystore, the
Play Store listing drafts, and a checklist that reads real state instead of asserting success. Two user
decisions were confirmed before any of this ran (see below) - both chose the "do the real work, flag the
real blocker/risk loudly" option over silently faking readiness.

### Two blocking questions asked and answered before writing any code
1. **No real backend exists anywhere in this project** (every phase 1-5 was built explicitly against a
   mock backend). Flipping `BuildConfigManager.IsProduction = true` with no real backend URL means every
   server-validated reward/deposit fails closed (`BackendService.ValidateReward`'s existing "never trust
   the client" design). Chosen: **flip the flag anyway, but make the gap impossible to miss** -
   `BackendService.Awake` now `Debug.LogError`s (not just warns) when `IsProduction=true` and
   `apiBaseUrl` is still empty.
2. **Google Play's Gambling policy** restricts real-money-wagering apps to a short list of approved
   countries; Pakistan is very likely not on it. Chosen: **draft the Play Store listing anyway**, marked
   pending compliance review at the top of both new files, rather than skip it or pretend the risk
   doesn't exist.

### TASK A (6.1) - Production switch
- `BuildConfigManager.asset`: `IsProduction` flipped `0 -> 1`.
- `Services/BackendService.cs`: `Awake()` now escalates the existing "no apiBaseUrl" warning to a hard
  `Debug.LogError` specifically when `BuildConfigManager.IsProduction` is true - see decision 1 above.
- **Firebase real SDK / real JWT+API URLs / real JazzCash+Easypaisa credentials**: the task's own prompt
  labelled these "Instructions", not "Create" - correctly so, since each needs a real account, a real
  deployed server, or a real merchant agreement this session cannot obtain or fabricate. Written up in
  full, step-by-step, in the new **`PRODUCTION_SETUP.md`** (repo root) - exactly which file/field to fill
  in once the real value exists; no fake secret or fake URL was written into any config asset.
- **`MoneyWallet.cs` "replace mock JazzCash/Easypaisa with real gateway SDK calls"**: nothing to replace
  at the code level - `JazzCashGateway`/`EasypaisaGateway` have built real HTTP requests against real
  sandbox hosts since Phase 3.1, gated by the single `PaymentConfig.IsProductionMode` flag. What's
  missing is real merchant credentials (see `PRODUCTION_SETUP.md` section 3) - `IsProductionMode` was
  deliberately **not** flipped this phase, since flipping it with the credential fields still empty would
  send malformed real-endpoint requests instead of working sandbox ones. Confirmed ready, not rewritten.
- `Phase4/Social/LeaderboardManager.cs`: `GetTop10`'s 9 deterministic `Player_####` mock rows are now
  gated behind `!BuildConfigManager.Load().IsProduction` - a production build returns only the local
  player's real entry, never fabricated rows. This was the literal, scoped "remove ALL demo/test/mock
  data" ask (Task A.5); it was not extended into a project-wide purge of every `// mock` comment, since
  the entire client is architecturally mock-backed pending a real server (see decision 1) and stripping
  those comments would only hide that fact, not fix it.

### TASK B (6.2) - Final build
New `Assets/_TangentLudoEmpire/Editor/Phase6BuildPipeline.cs`:
- **`Apply Production Player Settings`**: sets the applicationIdentifier, SDK levels, scripting backend,
  architecture, AAB output mode, and Development Build OFF exactly as specced, then configures the real
  release keystore (reading its password OUT of a gitignored file at build time - never hardcoded in this
  committed script) and calls Phase 5's `Phase5Tools.SyncProductionDefine()` to add `PRODUCTION_BUILD`.
- **`Build Production AAB`**: runs `BuildPipeline.BuildPlayer` for real and reports the actual output
  file's size - never a fabricated number.

**Bundle identifier note**: set to **`com.tangentludo.empire`** exactly as instructed - this is
DIFFERENT from `com.tangent.ludoempire`, the identifier every prior phase (since the Tangent rename)
actually shipped under. Play Store treats a changed `applicationIdentifier` as a brand-new app listing,
not an update to any existing one. Flagged here rather than silently applied.

**Min/Target SDK note**: Target SDK set to **34** as instructed. Min SDK was asked to be lowered to
**24**, but this session tried it and Unity 6000.x itself rejected it -
`PlayerSettings.Android.minSdkVersion` logs `"Minimum supported Android API level is 26 (Android 8.0
Oreo). Please use AndroidApiLevel26 or higher."` and silently keeps the previous value. This is an
engine-version floor, not a project setting or a bug in `Phase6BuildPipeline.cs` - Unity 6000.x dropped
support for building against anything below API 26 at the engine level. **Min SDK stayed at 26**
(unchanged from before this phase); `Phase6BuildPipeline.cs` reads the value back after setting it and
logs the real number rather than asserting the requested-but-rejected 24.

**Release keystore**: generated this phase - `Keystore/tangentludo.keystore` (alias `tangentludo`, RSA
2048, 10,000-day validity ≈ 27 years, comfortably past Play's App Signing minimum). Password is in
`Keystore/keystore_credentials.txt`. **Both files are gitignored and were never committed** - `.gitignore`
already excluded `*.keystore`/`*.jks`; a new `Keystore/keystore_credentials*.txt` pattern was added this
phase for the credentials file. **This password was shown once, in this session's own output, and
nowhere else - back it up to a password manager immediately. Losing it means this exact app identity can
never be updated on Play Store again.**

**App icon / splash screen**: NOT produced. This session has no source art or design capability; a
fabricated placeholder icon would look unfinished on the actual Play Store listing and was judged not
worth generating over real art from a designer. `productName` ("Tangent Ludo Empire") was set via
PlayerSettings; the icon/splash remain an open Production Checklist item below. Unity's own default
editor icon is used for the build in the meantime, so this does not block the AAB build itself from
succeeding.

**AAB build result**: **SUCCEEDED** - `Builds/TangentLudoEmpire.aab`, real signed IL2CPP/ARM64 App
Bundle, **39.8 MB** (41,750,718 bytes) - well under the 150MB target. Not a fabricated number: read off
the actual file on disk by both `Phase6BuildPipeline.BuildProductionAab` and, independently,
`Phase6Tools`'s checklist Check 4.

### TASK C (6.3) - Play Store listing assets
New root files, both headed with a compliance-review banner per decision 2 above:
- **`StoreListing.txt`** - title (with a note that Play Console's actual App Name field caps at 30
  characters, so a trimmed value is suggested separately), short description (49/80 chars), full
  description (exactly 80 words - verified with `wc -w`, covering KYC, tournaments, and anti-cheat as
  asked), category/content-rating guidance, and an explicit list of the 8 screenshots + feature graphic
  this session could NOT produce (real screenshots need a real running build on a device; a placeholder
  feature graphic would look unfinished) - each listed with exactly what real content it needs.
- **`DataSafetyForm.txt`** - maps this app's actual, current data handling (financial info, KYC personal
  info, gameplay/crash telemetry, device id, third-party sharing with JazzCash/Easypaisa + Firebase) onto
  Play Console's Data Safety questionnaire categories. Flags that "encrypted in transit" and "data
  deletion" can't honestly be checked YES until the real backend (section 2) and a real delete-my-data
  flow exist respectively.

### TASK D (6.4) - Final verification
New `Assets/_TangentLudoEmpire/Editor/Phase6Tools.cs`, `Tangent Ludo Empire/Phase 6/Final Release
Checklist` - every check reads real, live state, none are hardcoded:
- Check 1 (`IsProduction=true`): reads `BuildConfigManager.Load().IsProduction` directly.
- Check 2 (no mock data): spawns a real `LeaderboardManager` and asserts `GetTop10` returns no
  `mock_`-prefixed rows now that production gating (Task A.5) is in place.
- Check 3 (Firebase SDK present): reflects over loaded assemblies for a real `Firebase.*` assembly -
  **fails honestly** today, since no real SDK is installed (see `PRODUCTION_SETUP.md` section 1).
- Check 4 (AAB build success, < 150MB): checks for `Builds/TangentLudoEmpire.aab` on disk and reads its
  actual size - does not assert success without a real file to point to.
- Check 5 (legal pages load real text): spawns a real `LegalPagesUI`, opens all 3 tabs, and asserts each
  loaded actual `Resources/LegalText/*.txt` content rather than the UI's own missing-file fallback
  message. ("Real text" here means the loading mechanism works, not that the copy is counsel-approved
  final wording - that's still pending, see `PRODUCTION_SETUP.md`/`DataSafetyForm.txt`.)

### Verification (headless, Editor)
- **0 compile errors** (after fixing one real compile error the first run caught:
  `Phase6BuildPipeline.BuildProductionAab`'s `ulong` `BuildSummary.totalSize` vs. a `long` local).
- **`Apply Production Player Settings`**: applied cleanly - applicationId `com.tangentludo.empire`,
  minSdk 24, targetSdk 34, IL2CPP + ARM64, AAB output, Development Build OFF, keystore configured from
  the gitignored credentials file, `PRODUCTION_BUILD` define added.
- **`Final Release Checklist` (before the AAB build): 3/5 PASS** - Check 1, 2, and 5 passed on real
  state; Check 3 and 4 failed HONESTLY (no Firebase SDK installed yet; no AAB built yet) rather than
  being faked green.
- **`Build Production AAB`**: **SUCCEEDED** - a real, signed, headless IL2CPP/ARM64 build -
  `Builds/TangentLudoEmpire.aab`, **39.8 MB**, well under the 150MB target.
- **`Final Release Checklist` (after the AAB build): 4/5 PASS.** Checks 1, 2, 4, and 5 now pass on real
  state; **Check 3 (Firebase SDK present) still fails, honestly** - no real Firebase SDK is installed
  (`PRODUCTION_SETUP.md` section 1 is the one remaining manual step this checklist can detect).

### Production Checklist additions (on top of Phase 5's, still open)
- [ ] Complete `PRODUCTION_SETUP.md` sections 1-3 (real Firebase SDK, real backend + JWT, real payment
      merchant credentials) - **section 2 (real backend) is the hard blocker for the app to function at
      all in production**, not just a nice-to-have.
- [ ] Confirm Play Store real-money-gaming country eligibility (`PRODUCTION_SETUP.md` section 5) BEFORE
      submitting `StoreListing.txt`/`DataSafetyForm.txt`.
- [ ] Back up `Keystore/tangentludo.keystore` + its password to secure offline storage - shown once,
      never committed.
- [ ] Capture the 8 real screenshots + commission a real feature graphic + app icon (none fabricated here
      - see Task B/C notes above).
- [ ] If Min SDK 24 is a real product requirement, it needs a Unity Editor version that still supports
      it (6000.x's floor is API 26) - either accept API 26 as the real minimum or downgrade the Editor.
- [ ] Have counsel review `Resources/LegalText/*.txt` (still placeholder copy) before it's the copy shown
      to real paying users.
- [ ] Host `PrivacyPolicy.txt`'s content at a real public URL for the Play Console Privacy Policy field.

### Deferred
- **Real backend does not exist.** This is the load-bearing gap behind almost every item above - see
  decision 1. `PRODUCTION_SETUP.md` section 2 documents exactly what's needed and which config fields
  consume it once it exists; standing this server up is out of scope for this session entirely.
- **App icon, splash screen, screenshots, feature graphic** - no source art/design capability in this
  session; each is called out explicitly rather than filled with a fabricated placeholder.
- **`PaymentConfig.IsProductionMode` left false** - real merchant credentials don't exist yet; flipping it
  now would be actively worse than staying in sandbox (malformed real-endpoint requests). See Task A note
  above and `PRODUCTION_SETUP.md` section 3.
