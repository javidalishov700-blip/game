# Mağazaya yükleme (App Store + Google Play)

FLINCH Capacitor ile paketleniyor.

- **Google Play:** Windows yeter. Android Studio + Play Console.
- **Apple App Store:** iPhone paketi (IPA) yalnızca **macOS + Xcode** ile üretilir. Kendi Mac’in yoksa Windows’tan **Codemagic** (bulut Mac) kullan. Ücretli **Apple Developer** hesabı her durumda şart (~$99/yıl). Windows’ta Xcode çalışmaz; IPA’yı evde derleyemezsin.

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

Gizlilik: `public/privacy.html`  
Şartlar: `public/terms.html`  
Hakkında: `public/about.html`  
App Store / Play Console bir **https URL** ister. Bu dosyaları GitHub Pages, kendi siten veya netlify’a koy. Örnek:

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

### Windows’tan yükleme (Mac yok)

Kendi bilgisayarında iOS derlemesi yok. Sıra:

1. **Apple Developer** al: [developer.apple.com/programs](https://developer.apple.com/programs/) — Windows tarayıcı yeter.
2. [App Store Connect](https://appstoreconnect.apple.com) → Users and Access → Integrations → **App Store Connect API** → Generate API Key.
   - Access: **App Manager**
   - `.p8` dosyasını, **Issuer ID** ve **Key ID**’yi indir / kopyala. `.p8` bir daha inmez, sakla.
3. Certificates, Identifiers & Profiles → Identifiers → **+** → App IDs → `com.javidalishov.flinch`
4. App Store Connect → My Apps → **+** → New App (iOS, name `FLINCH`, bundle `com.javidalishov.flinch`, SKU `flinch-ios`)
5. Listing’i Windows’tan doldur (aşağıdaki kopyala-yapıştır). Privacy Policy için `privacy.html`’in **https** adresi şart.
6. **Codemagic** ile IPA üret ve yükle:
   1. [codemagic.io](https://codemagic.io) → GitHub ile giriş → bu repo’yu ekle
   2. Teams → Integrations → **Developer Portal** → Apple API key (`.p8` + Issuer ID + Key ID). Integration adı: `codemagic`
   3. Codemagic, `codemagic.yaml` dosyasını görür. Workflow: **iOS App Store (Windows)**
   4. İlk seferde `APP_STORE_APP_ID` değişkenini App Store Connect → App → App Information → **Apple ID** (sayı) ile doldur
   5. Start new build. Bitince TestFlight’a düşer (10–30 dk işlem).
   6. App Store Connect → TestFlight’ta build yeşil olunca → App Store sekmesi → sürümü seç → **Submit for Review**

#### `No matching profiles found for bundle identifier 'com.javidalishov.flinch'`

Codemagic imza profili bulamadı. Apple tarafında App ID yok demektir. Windows tarayıcıda:

1. [developer.apple.com/account](https://developer.apple.com/account) — üyelik **Active** olmalı (ödeme geçmeden Identifier oluşturamazsın).
2. Certificates, Identifiers & Profiles → **Identifiers** → **+**
   - App IDs → App
   - Description: `FLINCH`
   - Bundle ID: **Explicit** → `com.javidalishov.flinch`
   - Register
3. [appstoreconnect.apple.com](https://appstoreconnect.apple.com) → Apps → **+** → New App
   - Platform: iOS
   - Name: `FLINCH`
   - Primary language: English
   - Bundle ID listesinde `com.javidalishov.flinch` görünmeli — görünmüyorsa adım 2 bitmemiş
   - SKU: `flinch-ios`
4. Codemagic → Team settings → Integrations → Apple Developer Portal bağlı mı kontrol et (API key **App Manager** veya **Admin**).
5. Bu branch’i tekrar **Start new build**.

Profili artık build sırasında Codemagic oluşturur (`--create`). App ID’yi senin Apple hesabında bir kez kaydetmen şart.

Ekran görüntüsü (Windows): Chrome’da oyunu aç (`npm run dev`), F12 → cihaz toolbar → iPhone 14 Pro Max, 3–5 kare al. App Store 6.7" ister (ör. 1290×2796). Küçükse bir görsel editörde o boyuta büyüt.

Mac kirala (MacinCloud vb.) da olur; Codemagic daha ucuz ve bu repo için hazır.

### Mac’te paket (elden)
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

**Subtitle (30 char):** Six lanes. Don't flinch.

**Description:**
```
Six spikes. Six lanes. A glass core.

Tap the right wedge when it turns gold. Too early is a flinch — you live, the combo dies. Too late costs a life.

Ghosts must be left alone. Gold pays double. Later waves overlap, and some spikes switch lanes mid-flight.

3 lives. Wave bonuses. 2x 3x 4x combos. Slow-mo shatter.

Settings: sound, BGM, vibration. Share your score.
```

**Keywords (100 char):** timing,reflex,arcade,lanes,combo,slow motion,reaction,skill,one thumb

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
Six lanes. Wait for gold. Let ghosts pass. 2x 3x 4x. Don't flinch.
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
