# Testing Guide — Android Ludo 3D (Sab Kuch Ek Jagah)

Is project mein **4 alag systems** hain, har ek ki apni test scene hai.
Yahan har ek ko test karne ka mukammal, step-by-step tareeqa hai.

---

## Shuru Karne Se Pehle (Ek Dafa Ka Kaam)

1. **Unity Hub** se project kholein (Unity 6000.5.4f1)
2. Khulte hi thora intezar karein (scripts compile hongi)
3. **Window → General → Console** kholein (agar khuli nahi)
4. Console mein koi **laal (red) error** tou nahi — agar ho, screenshot/text bhej dein, main foran theek kar dunga

---

## System 1: Main Ludo Game 🎲

**Scene:** `Assets/Scenes/SampleScene.unity`

### Kaise Test Karein
1. Project window mein `Assets/Scenes/SampleScene.unity` par **double-click** karein (khul jayegi)
2. Upar **▶ Play** button dabayein
3. Ye sab check karein:

| Kya Karna Hai | Kya Hona Chahiye |
|---|---|
| Roll button dabayein | Dice ghume aur 1-6 ka number dikhaye |
| Dice par 6 na aaye | Token move na ho, agla player ki baari aaye |
| Dice par 6 aaye | Yard se token bahar nikle, dobara roll ka mauka mile |
| Token par tap karein | Sahi token move ho (agar 1 se zyada movable hon tou aap chunein) |
| Apna token dushman ke upar le jayein | Dushman ka token yard wapas chala jaye (capture) |
| Star cell par khaRe hon | Capture na ho |
| Token poora chakkar laga le | Apne rang ke home-stretch mein muRe |
| Sab 4 tokens finish hon | "Wins!" screen aaye |

**Poori checklist:** `PLAYTEST_CHECKLIST.md` file dekhein (isi folder mein).

---

## System 2: Gulli Danda 🏏

**Scene:** `Assets/Scenes/GulliDandaMilestone_Test.unity`

### Kaise Test Karein
1. Ye scene kholein, **▶ Play** dabayein
2. **Tap karein** (screen par kahin bhi) → Gulli upar uchlegi
3. Thori der baad **swipe karein** (ungli/mouse ghasit kar chhoRein) → Gulli ko power-meter ke hisaab se strike karegi
4. Dekhein:
   - Gulli udd kar door jati hai (physics ke sath)
   - **Bhaju** (neela character) khud chal kar Gulli pakadne ki koshish karta hai
   - Agar pakad le → "Caught" (out, 0 coins)
   - Agar zameen par gir jaye → Bhaju uthata hai aur **Kotha** (safed marker) ki taraf wapas fenkta hai
   - Kotha lag jaye → out (lekin distance ke coins mil chuke hote hain)
   - Na lage → agla turn shuru
5. UI mein **Distance, Chances Left, Coins** dikhne chahiye

---

## System 3: Ludo Empire (Land & Resources) 🌲

**Scene:** `Assets/Scenes/EmpireMilestone1_Test.unity`

### Kaise Test Karein
1. Ye scene kholein, **▶ Play** dabayein
2. **Biome chunein** (Forest ya Snow button)
3. **Darakhton (trees) par tap karein** — 10 baar kaatein
4. Har chop par **Wood** inventory mein barhna chahiye
5. **"Sell Wood"** button dabayein → Silver Coins milne chahiye
6. **Tool Shop** se koi tool (Hammer/Chisel/Cutter) khareedein
7. **"Build Foundry"** button dabayein → Factory ban jaye

Ye poora loop hai: **Biome → Chop 10 Trees → Sell Wood → Buy Tool → Build Foundry.**

---

## System 4: Killa Bandar 🥋

**Scene:** `Assets/Scenes/KillaBandarMilestone_Test.unity`

### Kaise Test Karein
1. Ye scene kholein, **▶ Play** dabayein
2. Neeche-baayen taraf **joystick** dikhega — usay ghumayein, **Red player** (jo shuru mein Protector/"Killa Bandar" hai) chalega
3. Dekhein:
   - **Attackers** (Green/Yellow/Blue) khud-b-khud Killa stake ki taraf chalte hain (AI)
   - Agar aap (Protector) kisi Attacker ko chhoo lein (aur shoes bachi hon) → **role swap** ho jaye, wo naya Protector ban jaye
   - Agar Attacker shoe chura le (stake ke paas jaye) → shoe count kam ho
   - Sab shoes khatam ho jayein → "Attacker Strike" state, **Sprint button** dikhna chahiye
   - Sprint button dabayein → Protector Milestone (peeli marker) ki taraf bhaage
   - Wahan pahunch jaye → shoes reset, wahi Protector rahe
   - Rasta mein pakda jaye → role swap

---

## Agar Kahin Bhi Masla Aaye

1. **Console window** (Window → General → Console) check karein
2. Jo bhi laal error ho, uska **poora text copy** kar ke mujhe bhej dein
3. Ya screenshot bhej dein agar visual masla ho (kuch dikh nahi raha, galat jagah hai, waghera)

## Compiler Khud Check Karwana Ho

Agar aap Unity **band** kar dein aur mujhe bata dein "compiler chala len", main khud command-line se Unity chala kar **poore project ka compile-check** (0 errors/0 warnings) kar sakta hoon — bina aapko kuch karne ki zaroorat.

---

## Sab Files Ka Reference

| File | Kya Hai |
|---|---|
| `PROJECT_STATUS.md` | Poori progress, kya bana kya baqi |
| `PLAYTEST_CHECKLIST.md` | Sirf Main Ludo ki detailed checklist |
| `ANDROID_BUILD_GUIDE.md` | Android build banane ka tareeqa |
| `BUILD_TROUBLESHOOTING.md` | Build errors ka hal |
