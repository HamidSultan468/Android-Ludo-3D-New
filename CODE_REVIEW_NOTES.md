# Chapter 24 — Code Review / Polish Pass

Maine game ki asal logic wali files dobara ghaur se paRhi hain: `GameManager.cs`,
`PlayerToken.cs`, `DiceManager.cs`, `GridManager.cs`, `TokenSelector.cs`,
`AudioManager.cs`. Maqsad tha: distance/index calculations, capture logic,
aur turn-flow mein koi chupi hui ghalti (bug) DhoonDna.

## Jo Check Kiya

- **Finish distance formula** (`(mainPathLength - 1) + homeStretchLength`) —
  sahi hai, token ko exact count par hi ghar (finish) mein janay deta hai,
  overshoot nahi hone deta (asal Ludo rule).
- **Capture logic** — apne hi rang ke token ko capture nahi karta, safe
  cells par capture nahi hota, aur List ko loop ke doran modify nahi karta
  (jo crash ki wajah ban sakta tha) — sab sahi.
- **Wrap-around** (jab token board ka chakkar poora karte hue index 51 se
  wapas 0 par aata hai) — position aur capture-check dono ek hi formula use
  karte hain, is liye token jahan dikhta hai wahi uska "asal" hisaab bhi hai
  — mismatch nahi.
- **Teen-chhakke (3 sixes) ka rule** — sirf usi player ki lagatar chaal
  tak counter chalta hai, agle player ke liye khud reset ho jata hai — sahi.

## Jo Mila (Halka Sa Note, Bug Nahi)

`PlayerToken.cs` mein ek `OnTokenCaptured` event bana hua hai jo **kahin use
nahi ho raha** — kyunke `GameManager` khud apna `OnTokenCaptured(mover,
captured)` event nikaal deta hai jo `AudioManager` sunta hai. Ye koi kharabi
nahi hai (game sahi chalta hai), bas ek chhota "dead code" — chahen tou baad
mein hata sakte hain, filhaal chhoRna bhi bilkul theek hai.

## Ek Jaani-Boojhi Kami (Future Scope)

Asal Ludo mein ek "block" rule hoti hai — agar ek hi rang ke 2 tokens ek
cell par hon, tou dushman us cell par capture nahi kar sakta. Ye rule abhi
implement nahi hai. Agar chahiye tou batayein, alag se add kar dunga.

**Nateeja:** Code structurally sahi hai, koi crash ya galat move wala bug
nahi mila.

## Update — "Roll 6 par game freeze" Report Ki Investigation

User ne report kiya ke 6 roll hone par game freeze ho jata hai. Maine
`GameManager.cs`, `DiceManager.cs`, `PlayerToken.cs` mein har `while` loop
dobara check kiya:

- Teeno files mein sirf 3 `while` loops hain, aur teeno **coroutines ke
  andar** hain (har frame `yield return` karte hain) — ye Unity ko kabhi
  bhi "freeze" nahi kar sakte, kyunke control har frame Unity ko wapas
  mil jata hai. **Koi asal infinite loop nahi mila.**
- Ek latent risk zaroor mili: agar `Time.timeScale` kabhi `0` ho jaye
  (jaise future mein koi pause-menu feature), `Time.deltaTime` hamesha `0`
  reh jata hai aur ye animation-loops hamesha ke liye ruk sakte hain. Isi
  ke against safety counters laga diye hain (`DiceManager.RollRoutine`,
  `PlayerToken.HopTo`, `PlayerToken.MoveRoutine`).
- **Sab se mumkin asal wajah:** Jab 6 aaye aur player ke 1 se zyada tokens
  move ho sakte hon (jaisa game shuru mein hota hai — sab 4 tokens yard
  mein), `GameManager` sahi tareeqe se **ruk kar player ke tap ka intezar
  karta hai** — ye normal Ludo rule hai, bug nahi. Agar tap register nahi
  ho raha (missing Collider, Board Origin align nahi hua, camera reference
  missing), tou ye "freeze" jaisa mehsoos hota hai.

**Fix:** `GameManager.cs` aur `TokenSelector.cs` mein `Debug.Log`/
`Debug.LogWarning` add kiye hain taake Console mein saaf pata chale kya ho
raha hai — "waiting for tap" hai, ya tap register hi nahi ho raha, ya
kisi aur wajah se reject ho raha hai.

## Update 2 — User Ne Confirm Kiya: Tap Kaam Nahi Kar Raha

User ne confirm kiya ke token tap/click asal mein register nahi ho raha
tha. Do fixes lagayi hain:

1. **`GameManager.cs` — Auto-Select Fallback:** Ab agar 1 se zyada tokens
   move ho sakte hon aur `autoSelectDelay` (default 5 second) tak koi tap
   na aaye, tou **pehla movable token khud-ba-khud** chun kar move kar diya
   jata hai — taake game kabhi bhi hamesha ke liye na ruke, tap kaam kare
   ya na kare. Inspector se ye delay adjust ya 0 kar ke band bhi ki ja
   sakti hai.
2. **`TokenSelector.cs` — Camera Fallback:** `Camera.main` tab `null`
   deta hai jab scene ka camera "MainCamera" tag ke saath na ho — is se
   pehle har tap chup-chap fail ho jati thi. Ab agar `Camera.main` na
   milay, koi bhi camera Scene mein DhoonD kar use kar liya jata hai, aur
   agar koi camera hi na mile tou saaf warning Console mein aati hai.

## Update 3 — Do Naye Masle: Camera Phir Bhi Nahi Mil Raha, Aur GridManager Crash

**Masla A — Camera phir bhi `null` reh raha tha:** Wajah ye thi ke
`FindFirstObjectByType` **sirf active GameObjects mein dhoondta hai** —
agar `Camera` (ya uska parent) inactive tha, ye miss ho jata tha. Fix:
`FindObjectsByType<Camera>(FindObjectsInactive.Include, ...)` use kiya,
jo inactive objects mein bhi dhoondta hai. Agar scene mein 1 se zyada
cameras hon (jaise board prefab ke andar koi extra camera), tou ab
pehle "MainCamera" tag wala, phir active wala, phir jo bhi mile — is
tarteeb se chunta hai (random/galat camera uthne ka khatra khatam).
`Update()` ko bhi "self-healing" bana diya — agar camera baad mein kho
jaye, wahin dobara DhoonD leta hai.

**Masla B — `MissingReferenceException` on `GridManager`:** Asal bug
mil gaya — **`GridManager.cs` ka Singleton pattern adhoora tha.**
`Awake()` mein `Instance` set hoti thi, lekin **`OnDestroy()` kabhi
`Instance` ko clear nahi karta tha**. Jab scene reload hoti (jaise
Restart button), purana GridManager destroy ho jata tha lekin static
`Instance` field usi murda object ki taraf ishara karta reh jata — jo
bhi baad mein (jaise 5-second auto-select timer) `GridManager.Instance`
access karta, crash ho jata.

**Fix:** `Instance` ab **self-healing** hai — agar cached reference
destroy ho chuki ho (Unity ka `== null` check destroyed objects ko
sahi pehchan leta hai), khud-ba-khud scene mein naya GridManager
DhoonD leta hai. Saath hi `OnDestroy()` add kiya jo `Instance` ko turant
saaf kar deta hai jab wahi object destroy ho.

## Update 4 — Camera Board Ki Taraf Nahi Dekh Raha Tha (Blank Game View)

Game View khaali/blank aa raha tha kyunke Camera ki position/rotation
`LudoBoard` ke hisaab se sahi nahi thi. `SceneAutoWirer.cs` (Window > Ludo
Tools > 5. Wire All References) mein ab ye bhi add kar diya hai:

- Scene mein `"LudoBoard"` naam ka object DhoonDta hai
- Uske saare Renderers ka **asal size** naapta hai (fixed number nahi,
  taake board chhota ho ya bara, hamesha sahi kaam kare)
- Camera ko board ke **upar aur peeche** rakh kar `LookAt()` se seedha
  board ke center par point kar deta hai (top-down board-game wala angle)

Agar scene mein `"LudoBoard"` naam ka object na mile, tool khamosh crash
nahi hota — saaf warning deta hai ke camera manually set karni paRegi.

## Update 5 — Blank Game View Ki Jaanch: Asal Board Kabhi Bana Hi Nahi

`Assets/Prefabs/LudoBoard.prefab` check kiya — usme **koi 3D mesh nahi**
(0 MeshRenderer/MeshFilter), sirf GridManager ke waypoint markers
(Cell/Home/Yard), ek nested Camera, aur ek chhota SpriteRenderer. `Art/Boards/`
mein sirf reference PNGs hain, koi 3D model nahi. **Matlab asal visual
board is project mein kabhi bana hi nahi.**

**Fix:** `PlaceholderBoardGenerator.cs` add kiya (Ludo Tools #6) — 15x15
textured Plane origin par banata hai (`ludo board 3d.png` ko texture ki
tarah use karta hai agar mile), taake test ke liye Game view khaali na
rahe. Purani `LudoBoard.prefab` ko jaan-boojh kar scene mein nahi daala
(usme chhupi hui Camera dobara wahi purana bug la sakti thi).

## Update 6 — LudoBoard Manually Daali Gayi: Do Cameras + Ghalat Zoom

User ne `LudoBoard` prefab khud Hierarchy mein drag kar di (mana kiya
tha, lekin kar diya) — jisme apni **alag Camera** aur ek chhoti
SpriteRenderer chhupi thi. Screenshot se 3 masle mile:

1. **Do cameras** scene mein (root `Main Camera` + `LudoBoard/Camera`) —
   render conflict/confusing visuals.
2. **`BoardPlaceholder` ka scale ghalat tha** (`15, 1.5, 1.5` — non-uniform,
   Inspector mein galti se type ho gaya), jis se texture bohat zyada
   stretch/distort ho gayi.
3. **Asal bug meri `FrameCameraOnBoard()` mein tha:** ye hamesha
   `"LudoBoard"` object par camera focus karti thi — lekin us mein koi
   real mesh nahi, sirf chhoti si sprite — is liye camera us **chhoti
   sprite ke bohat kareeb** zoom ho gaya, poori screen sirf uski texture
   se bhar gayi.

**Fix (`SceneAutoWirer.cs`):**
- `FrameCameraOnBoard()` ab pehle `"BoardPlaceholder"` DhoonDta hai (jahan
  asal geometry hoti hai), `"LudoBoard"` sirf fallback ke tor par.
- Nayi `DisableExtraCameras()` method — chosen camera ke ilawa **har aur
  camera ko disable** kar deti hai (delete nahi, sirf disable — Undo se
  wapas ho sakta hai).

**Fix (`PlaceholderBoardGenerator.cs`):** Ab agar `BoardPlaceholder`
pehle se maujood ho, tool usay **delete karwane ke bajaye khud reset**
kar deta hai (position/rotation/scale sab sahi kar deta hai) — is se
Inspector mein ghalti se ghalat number type hone jaisi cheezein khud
theek ho jati hain.
