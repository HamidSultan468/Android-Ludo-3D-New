# Phase 6 Production Setup — manual steps only YOU can complete

Everything in this file requires a real credential, a real account, or a real deployed server that
this session has no way to obtain or fabricate safely. Each section says exactly which file/field to
fill in once you have the real value — the code on the other end is already built and ready to consume
it (see `MIGRATION_NOTES.md` PHASE 6 for what changed automatically).

## 1. Firebase (real Analytics + Crashlytics SDK)

`FirebaseManager.cs` / `CrashlyticsManager.cs` (Phase 5) are mock facades today — no Firebase package is
installed. To go real:

1. Create a Firebase project at https://console.firebase.google.com (or use an existing one).
2. Add an Android app inside it with package name **`com.tangentludo.empire`** (must match exactly —
   see `ProjectSettings` / `Phase6BuildPipeline.ApplyProductionPlayerSettings`).
3. Download the generated `google-services.json` and place it at `Assets/google-services.json`.
   **Do not commit it** — it identifies your Firebase project; add it to `.gitignore` if you keep this
   repo shared.
4. In Unity: **Window → Package Manager → Add package by name** →
   `com.google.firebase.app`, then `com.google.firebase.analytics` and
   `com.google.firebase.crashlytics` (or import the official Firebase Unity SDK `.unitypackage` from
   https://firebase.google.com/download/unity — either path works, package name is the modern route).
5. In `FirebaseManager.cs`, replace the body of `InitializeFirebaseApp()` with
   `Firebase.FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(...)`, and in `LogEvent`, replace
   the mock logging with `Firebase.Analytics.FirebaseAnalytics.LogEvent(...)` — both call sites are
   already marked with a `// TODO(real SDK)` comment naming the exact replacement.
6. In `CrashlyticsManager.cs`, same pattern: `LogException`/`SetUserId` each have a `// TODO(real SDK)`
   comment naming the exact `Firebase.Crashlytics.Crashlytics.*` call to swap in.
7. This session cannot verify a package resolve headlessly (no way to confirm Unity's Package Manager
   successfully pulled a new remote dependency without a live Editor + internet check), so it was not
   attempted automatically — do this step yourself, then re-run
   **Tangent Ludo Empire/Phase 6/Final Release Checklist** to confirm Check 3 ("Firebase SDK Present")
   goes green.

## 2. Real JWT keys + real backend API URL

**This is the #1 blocker to an actual launch.** Every phase of this project (1 through 5) was built
explicitly against a mock backend — no real server has ever been deployed. `SecurityConfig.apiBaseUrl`
being empty means `BackendService.ValidateReward` denies every server-validated reward by design
("never trust the client"). As of this phase, `BuildConfigManager.IsProduction = true` — with the
backend URL still empty, `BackendService.Awake` now logs a **hard error** (not just a warning) at
startup calling this out.

To fix, once a real backend exists (see `BACKEND_WEBHOOK_SPEC.md` for the contract every gateway/webhook
already expects it to implement):

1. Generate real, per-environment secrets server-side. Do **not** invent a "production-looking" secret
   and paste it into `SecurityConfig.asset` by hand — a signing/HMAC secret must come from wherever your
   backend generates and rotates its own keys.
2. Set `SecurityConfig.asset`'s `apiBaseUrl` to your real HTTPS backend base URL, `hmacSecret` to the
   real shared signing secret, and `kdfSaltBase64`/`localDataKeyBase64` to freshly-generated per-build
   values (never reuse the dev defaults committed for local testing).
3. Set `BuildConfigManager.asset`'s `ProductionApiBaseUrl` to the same real base URL (kept as a
   deliberately separate field from `SecurityConfig.apiBaseUrl` — see `MIGRATION_NOTES.md` Phase 5's
   scope note on why the two configs weren't unified).
4. JWT issuance/verification itself lives server-side, not in this client — `BackendService.LoginGuest`
   already expects a bearer token back from `/auth/guest` (or your real equivalent endpoint); no client
   code changes needed once real URLs are in place.

## 3. Real JazzCash / Easypaisa merchant credentials

`MoneyWallet` never talked to a "mock SDK" — since Phase 3.1, `JazzCashGateway`/`EasypaisaGateway`
already build and POST real MWALLET/checkout requests against sandbox hosts, gated by a single flag,
`PaymentConfig.IsProductionMode`. There is no gateway code left to write; there are real merchant
credentials left to obtain, which only your registered business can get from JazzCash/Easypaisa
directly (a merchant agreement, not a self-serve API signup):

1. Complete JazzCash's and/or Easypaisa's merchant onboarding (business registration, KYC, agreement).
2. Fill in `PaymentConfig.asset`: `JazzCash_MerchantID`, `JazzCash_Password`, `JazzCash_IntegritySalt`,
   `Easypaisa_StoreID`, `Easypaisa_StorePassword` with the real values they issue you.
3. Verify `JazzCash_LiveUrl`/`Easypaisa_LiveUrl` against your actual merchant integration docs — the
   values currently in the asset are a best-effort mirror of each sandbox host's shape, explicitly
   flagged `VERIFY` in the field's own tooltip, not values pulled from a live merchant portal.
4. Only then set `PaymentConfig.IsProductionMode = true`. **This phase deliberately did NOT flip it for
   you** — flipping it with empty credential fields would send malformed real-endpoint requests instead
   of working sandbox ones, which is strictly worse than staying in sandbox.
5. Set `PaymentConfig.WebhookBaseURL` to your real backend's public HTTPS base (see
   `BACKEND_WEBHOOK_SPEC.md`) once step 2's backend exists.

## 4. Release signing keystore

Already generated this phase: `Keystore/tangentludo.keystore` (alias `tangentludo`), password in
`Keystore/keystore_credentials.txt` — **both gitignored, neither is in this commit.**

**Back up both files to a password manager or secure offline storage right now.** Losing either one
means this exact app identity can never be updated on Play Store again — Google cannot recover or reset
a lost signing key for you.

## 5. Play Store real-money-gaming compliance

Google Play's Gambling policy restricts real-money-wagering apps to a short list of approved countries;
Pakistan is very likely **not** on that list as of this writing. Before submitting anything from
`StoreListing.txt`/`DataSafetyForm.txt`:

1. Check the current list of Google-approved countries for real-money gaming content in Play Console's
   own policy pages (this changes over time — verify at submission time, not against this document).
2. If Pakistan (or your actual target market) isn't covered, options include: pursuing Google's formal
   permission-request process for restricted content, targeting only an approved country, or
   distributing outside Play Store (direct APK, a regional store) instead.
3. Do not submit `StoreListing.txt` as-is without resolving this — it is marked a compliance-review
   draft for exactly this reason.
