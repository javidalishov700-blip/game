# Mağazaya yükleme (App Store + Google Play)

FLINCH Capacitor ile paketleniyor. **Apple App Store için Mac + ücretli Apple Developer hesabı şart.** Google Play için Windows/Mac/Linux + Play Console hesabı yeter.

Bundle id: `com.javidalishov.flinch`  
Sürüm: `1.0.0`

---

## 0. Her iki mağaza için ortak

```bash
npm install
npm test
npm run icons
npm run build
```

Gizlilik politikası dosyası: `public/privacy.html`  
App Store / Play Console bir **https URL** ister. Bu dosyayı GitHub Pages, kendi siten veya netlify’a koy. Örnek:

`https://javidalishov700-blip.github.io/game/privacy.html`

(Repo Settings → Pages → `public` klasörünü yayınla, ya da `privacy.html` içeriğini ayrı bir sayfaya yapıştır.)

---

## 1. Apple App Store (iPhone)

### Hesap
1. [Apple Developer](https://developer.apple.com/programs/) — yıllık ücretli üyelik
2. [App Store Connect](https://appstoreconnect.apple.com) → My Apps → **+** → New App
   - Platform: iOS
   - Name: `FLINCH`
   - Bundle ID: `com.javidalishov.flinch` (önce Certificates, Identifiers & Profiles’dan App ID oluştur)
   - SKU: `flinch-ios`

### Mac’te paket
Xcode yüklü bir Mac’te, proje klasöründe:

```bash
npm install
npm run icons
npm run build
npx cap add ios
npx cap sync ios
npx cap open ios
```

Xcode’da:
1. Soldan **App** target → **Signing & Capabilities** → Team’ini seç, Automatically manage signing
2. Display Name: `FLINCH`
3. Version `1.0.0`, Build `1`
4. **Info** → `ITSAppUsesNonExemptEncryption` = `NO` (özel şifreleme yok; export compliance “No”)
5. Cihazı **Any iOS Device (arm64)** seç
6. Product → **Archive**
7. Organizer → **Distribute App** → App Store Connect → Upload

Sonra App Store Connect’te build’in işlemesini bekle (10–30 dk).

### App Store Connect formu (kopyala-yapıştır)

**Name:** FLINCH

**Subtitle (30 char):** Wait. Then tap.

**Description:**
```
A spike flies at a glass core. Wait until it turns gold. Then tap.

Too early is a flinch — you live, but the combo dies.
Gold is a perfect — 2x, 3x, 4x…
Too late and the core shatters in slow motion.

One thumb. No menus. Tap to play.
```

**Keywords (100 char):** timing,reflex,arcade,one tap,slow motion,combo,minimal,reaction,skill

**Category:** Games → Arcade (Secondary: Action)

**Age rating:** 4+ (no violence against characters, no user-generated content)

**Screenshots:** iPhone 6.7" (iPhone 15 Pro Max / 16 Pro Max) en az 3 kare. Simulator’da oyunu aç, Cmd+S.
Gerekli boyutlar App Store Connect’te kırmızıyla işaretlenir; 6.7" + 6.1" yeter.

**Support URL:** gizlilik sayfan veya GitHub repo  
**Privacy Policy URL:** `privacy.html`’in https adresi

---

## 2. Google Play Store (Android)

```bash
npm install
npm run icons
npm run build
npx cap add android
npx cap sync android
npx cap open android
```

Android Studio:
1. Build → Generate Signed App Bundle
2. İlk seferde bir **keystore** oluştur, şifreyi kaybetme
3. Çıkan `.aab` dosyasını [Play Console](https://play.google.com/console) → Create app → Production / Testing’e yükle

Paket adı: `com.javidalishov.flinch`

Play listing kısa açıklama:
```
Wait until it turns gold. Then tap. 2x 3x 4x combos. Slow-mo if you're late.
```

---

## 3. İkon ve splash

Kaynaklar:
- `resources/icon.png` — 1024×1024, şeffaflık yok (App Store kuralı)
- `resources/splash.png` — 2732×2732

Mac/Android native ikonları üretmek için (native proje eklendikten sonra):

```bash
npx capacitor-assets generate
npx cap sync
```
