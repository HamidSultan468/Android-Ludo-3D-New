# Android Build Guide (Unity Editor ke Manual Steps)

Ye kaam code se nahi hota — Unity Editor ke settings menu mein khud karna
parta hai. Yahan step-by-step tareeqa hai.

## 1. Platform Android par Switch Karein
1. Unity mein: **File → Build Settings**
2. List mein **Android** chunein
3. **"Switch Platform"** button dabayein (thora time lagega)

## 2. Player Settings Set Karein
**File → Build Settings → Player Settings** (ya Edit → Project Settings → Player)

- **Company Name** aur **Product Name** apni marzi se likhein
- **Package Name** set karein, jaise: `com.hamidsultan.ludo3d`
  (format hamesha `com.company.appname` jaisa hona chahiye)
- **Minimum API Level**: Android 7.0 (API 24) ya usse upar rakhein
- **Target API Level**: "Automatic (highest installed)" chhoR dein
- **Default Orientation**: Landscape ya Portrait — jo aapke game ki design ho
- **Icon** aur **Splash Screen** apni tasveerein daal dein

## 3. Scenes In Build Check Karein
**File → Build Settings → Scenes In Build**
- Yahan MainMenu scene sab se pehle number par honi chahiye (index 0)
- Uske baad gameplay scene

## 4. Keystore Banayein (Google Play par publish karne ke liye zaroori)
**Player Settings → Publishing Settings**
1. "Create a new keystore" chunein
2. Password set karein aur **kahin mehfooz save kar lein** — agar ye kho gaya
   tou app update karna mushkil ho jayega
3. Key alias banayein

⚠️ **Zaroori:** Keystore file (`.keystore`) aur uska password **kabhi GitHub
par push na karein** — ye secret hai. Hum isay `.gitignore` mein already
excluded rakhte hain.

## 5. Build Karein
- **Testing ke liye:** File → Build Settings → "Build And Run" (phone USB se
  connect hona chahiye, USB Debugging on honi chahiye)
- **Play Store ke liye:** "Build" dabayein, format **Android App Bundle
  (.aab)** chunein

## 6. Phone Par Test Karna
1. Phone ki **Settings → About Phone** mein "Build Number" par 7 baar tap
   karein — Developer Options khul jayengi
2. Developer Options mein **USB Debugging** ON karein
3. Phone ko USB se PC se jorein
4. Unity mein **Build And Run** dabayein

---
Jab ye sab steps kar lein, mujhe bata dein — agar koi error aaye tou uska
screenshot ya error message share kar dein, main uska hal bata dunga.
