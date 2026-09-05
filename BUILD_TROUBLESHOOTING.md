# Chapter 22 — Android Build Troubleshooting

`ANDROID_BUILD_GUIDE.md` mein diye steps follow karte waqt agar koi error
aaye, yahan uska hal dekh lein.

## "SDK not found" ya "Android SDK/NDK/JDK missing"
Unity Hub kholein → apni Unity version ke saamne gear/settings icon →
**Add Modules** → "Android Build Support" ke andar **SDK**, **NDK**, aur
**OpenJDK** teeno tick karke install karein (agar pehle se nahi hain).

## "Keystore was tampered with, or password was incorrect"
Password ghalat likha gaya hai, ya keystore file corrupt ho gayi hai.
Password dobara check karein. Agar keystore hi kho gayi ho, tou naya
keystore banana paRega — lekin agar app pehle Play Store par publish ho
chuki hai, purani keystore ke baghair update nahi ho sakti (isi liye keystore
mehfooz rakhna zaroori hai).

## "Gradle build failed"
1. Sabse pehle poora error message (Console window mein neeche scroll karein)
   copy kar ke mujhe bhejein — ye error har baar mukhtalif wajah se aata hai.
2. Aksar wajah: internet slow hona (Gradle pehli baar files download karta
   hai) — dobara try karein.

## "Failed to resolve" ya package/dependency errors
- Internet connection check karein
- **Edit → Preferences → External Tools** mein Gradle/JDK paths sahi hain ya nahi dekhein

## App phone par install nahi ho rahi ("App not installed")
- Phone mein pehle se koi purana test version installed hai jo **alag
  keystore** se signed tha — usay pehle uninstall karein, phir dobara try karein
- Phone ki Settings mein "Install from Unknown Sources" allow karein (agar
  Play Store se nahi, seedha APK install kar rahe hain)

## Build ban gayi lekin app khulte hi crash ho jati hai
- Unity mein **File → Build Settings → Development Build** tick kar ke
  dobara build karein, phir phone connect kar ke **Console** window mein
  error dekhein (Window → Analysis → Console, ya `adb logcat` use karein)
- Wo error message mujhe bhej dein, main uski wajah bata dunga

## Build bohat time le rahi hai
Pehli baar 10-20 minute lagna normal hai (Gradle sab kuch download/cache
karta hai). Agli baar tez hogi.

---
Koi bhi error aaye jo yahan na ho, uska **poora error text ya screenshot**
mujhe bhej dein — main uski wajah dhoond kar hal bata dunga.
