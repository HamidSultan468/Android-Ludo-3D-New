# Ludo 3D Android — Chapters List (Master Tracker)

Ye ab is project ki **asal (master) tracking file** hai. Purani `TASKLIST.md`
abhi bhi mojood hai (purana record), lekin aage se sirf isi file ko follow
karenge.

**Qaida:** Jab bhi aap **"next"** likhenge, agle **5 khaali `[ ]` chapters**
mukammal kiye jayenge, phir unhe `[x]` tick kar diya jayega.

---

## Part A — Core Game Scripts (Mukammal ✅)

- [x] **Chapter 1** — Project & GitHub Setup (alag git repo, private GitHub repo, .gitignore)
- [x] **Chapter 2** — Board Grid System (`GridManager.cs`)
- [x] **Chapter 3** — Player Token Movement (`PlayerToken.cs`)
- [x] **Chapter 4** — Token Selection Input (`TokenSelector.cs`)
- [x] **Chapter 5** — Dice System (`DiceManager.cs`)
- [x] **Chapter 6** — Game Turn Manager (`GameManager.cs`)
- [x] **Chapter 7** — Dice UI (`DiceUI.cs`)
- [x] **Chapter 8** — Turn Indicator UI (`TurnIndicatorUI.cs`)
- [x] **Chapter 9** — Game Over UI (`GameOverUI.cs`)
- [x] **Chapter 10** — Main Menu UI (`MainMenuUI.cs`)
- [x] **Chapter 11** — Computer Player / AI (`AIPlayer.cs`)
- [x] **Chapter 12** — Audio System (`AudioManager.cs`)
- [x] **Chapter 13** — Neon Glow Visual Effect (`NeonGlowController.cs`)
- [x] **Chapter 14** — Save/Settings System (`SaveManager.cs` + `SettingsUI.cs`)
- [x] **Chapter 15** — Android Build Guide (`ANDROID_BUILD_GUIDE.md`)

## Part B — Unity Scene Automation Tools (Naya Kaam)

Ye "Editor Tools" hain — chhote scripts jo Unity ke andar ek **menu button**
ke through khud-ba-khud GameObjects banayenge, components lagayenge, aur
references jorenge — taake aapko har cheez haath se drag-drop na karni paRe.

- [x] **Chapter 16** — Board Waypoint Generator Tool (`BoardWaypointGenerator.cs` — 52 main-path + home-path + yard points khud banane wala Editor tool)
- [x] **Chapter 17** — Scene Bootstrapper Tool (`SceneBootstrapper.cs` — GameManager, DiceManager, AudioManager, SaveManager, GridManager, TokenSelector ek click mein ban jayein)
- [x] **Chapter 18** — Token Prefab Generator (`TokenPrefabGenerator.cs` — 16 tokens khud ban kar PlayerToken + Collider lagein, Prefab bhi save ho)
- [x] **Chapter 19** — Basic UI Canvas Builder (`UICanvasBuilder.cs` — Roll button, dice text, turn text, win panel, settings panel khud ban jaye)
- [x] **Chapter 20** — Auto-Wiring Tool (`SceneAutoWirer.cs` — sab scripts ke Inspector references khud-ba-khud jurr jayein)

## Part C — Testing & Launch

- [x] **Chapter 21** — `PLAYTEST_CHECKLIST.md` (2-4 players ka poora match check karne ki checklist — khelna aapko hai)
- [x] **Chapter 22** — `BUILD_TROUBLESHOOTING.md` (Android build ke aam errors ka hal)
- [x] **Chapter 23** — `PLAY_STORE_LISTING.md` (App name, description, asset checklist — Play Store ke liye ready)
- [x] **Chapter 24** — `CODE_REVIEW_NOTES.md` (Core scripts ka dobara review — koi bug nahi mila)
- [x] **Chapter 25** — `LAUNCH_CHECKLIST.md` (Publish karne se pehle ka final checklist)

---

**Note (honest baat):** Chapters 21-23 mein kuch hisse (Unity Editor mein
click karna, phone connect karna, Play Store account) **sirf aap** kar sakte
hain — main Unity ki GUI khud nahi chala sakta. Un chapters mein, jo hissa
code/script se ho sakta hai wo main karunga, aur jo manual click hai uska
saaf step-by-step guide de dunga.
