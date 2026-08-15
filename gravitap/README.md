# GravitAP — The Gravity Manipulation Hyper-Casual Game

> **Tap the void. Bend gravity. Hit the portal.**

---

## Oyun Konsepti (Game Concept)

**GravitAP** piyasada daha önce hiç görülmemiş bir mekanik üzerine kurulmuştur:

Oyuncu ekrana **dokunarak** geçici **yerçekimi kuyuları** (gravity wells) oluşturur. Sürekli hareket halindeki bir neon top, bu kuyulara çekilerek **yörüngesi değiştirilir**. Amaç: topu altıgen neon portallara yönlendirmek.

### Neden Benzersiz?
| Özellik | GravitAP | Diğerleri |
|---------|----------|-----------|
| Kontrol | Fizik kuyusu yerleştirme (dolaylı) | Direkt çekme / fırlatma |
| Yörünge tahmini | 0.4s anlık önizleme yayı | Yok |
| Combo motoru | 3.2s zaman pencereli zincir | Basit ardışık vuruş |
| Zorluk katmanı | Well ömrü + portal hareketi + ömür süresi | Sadece hız |

### 3 Saniyelik Core Loop
1. **OKU** — Portal belirir, topun hızını ve yönünü kestir
2. **DOKUN** — Tahmin noktasına kuyu yerleştir → 0.4s yörünge yayı flaşlar
3. **NOVA** — Top portale girer → patlama + ekran sarsıntısı + `×4 COMBO` uçar

### Rage-Bait Mekanizması
"Portal'ın yanına dokun zaten!" — ama top saniyede 900px hızla gidiyor, kuyu 0.2 saniye şarj süresi istiyor, tahmin yayı kaybolmadan karar vermek gerekiyor...

---

## Teknoloji

**Native Android Kotlin + SurfaceView** — Motor overhead'i sıfır, APK ~2MB, 60fps garantili, tam haptic API erişimi.

- **Min SDK:** 24 (Android 7.0 — cihazların %98'i)
- **Target SDK:** 34 (Android 14)
- **Dil:** Kotlin 1.9
- **Build:** Gradle 8.0 + AGP 8.1.2

---

## Kurulum ve Derleme

### Gereksinimler
- **Android Studio Hedgehog** (2023.1.1) veya üzeri
- **JDK 17** (Android Studio ile birlikte gelir)
- **Android SDK 34** (Android Studio SDK Manager'dan yükleyin)

### Adımlar

```bash
# 1. Klonla
git clone <repo-url>
cd gravitap

# 2. Android Studio'da aç
# File → Open → gravitap klasörünü seç
# Gradle sync otomatik başlar

# 3. APK derle (Android Studio'da)
# Build → Build Bundle(s) / APK(s) → Build APK(s)

# 4. Cihaza yükle
# Run → Run 'app' (USB ile bağlı cihaz gerekli)
```

### Komut Satırından (Gradle yüklüyse)

```bash
cd gravitap
gradle wrapper          # Gradle wrapper jar'ı oluştur
./gradlew assembleDebug # Debug APK derle
./gradlew assembleRelease --stacktrace  # Release APK
```

### Google Play Store için İmzalama

```bash
# Keystore oluştur
keytool -genkey -v -keystore gravitap-release.jks \
  -alias gravitap -keyalg RSA -keysize 2048 -validity 10000

# app/build.gradle içindeki signingConfigs.release'i doldur
# veya ortam değişkenleri kullan:
# KEYSTORE_PATH, KEY_ALIAS, KEY_PASSWORD, STORE_PASSWORD
```

---

## Proje Yapısı

```
gravitap/
├── app/src/main/
│   ├── java/com/gravitap/
│   │   ├── MainActivity.kt          — Fullscreen activity, lifecycle yönetimi
│   │   ├── GameView.kt              — SurfaceView, oyun döngüsü, input, render pipeline
│   │   ├── entities/
│   │   │   ├── Constants.kt         — Tüm oyun sabitleri ve renk paleti
│   │   │   ├── Ball.kt              — Neon top: fizik, trail efekti, glow rendering
│   │   │   ├── Portal.kt            — Altıgen portal: animasyon, hitbox, ömür zamanlayıcısı
│   │   │   └── GravityWell.kt       — Yerçekimi kuyusu: 3 aşamalı yaşam döngüsü, ripple ring
│   │   ├── systems/
│   │   │   ├── ParticleSystem.kt    — Patlama partikülleri + yüzen kombo metinleri
│   │   │   └── ScreenShake.kt       — Ekran sarsıntısı (decay tabanlı)
│   │   └── ui/
│   │       └── HUD.kt               — Skor, can, level, combo çubuğu, menü/game-over ekranları
│   └── res/
│       ├── values/themes.xml        — Fullscreen dark theme
│       └── drawable/                — Vektör launcher icon
```

---

## Oyun Mekaniği Detayları

### Yerçekimi Fiziği
Softened Newtonian gravity (singülarite önleme ile):
```
force = G × strength / (distance² + softening²)
```
Bu gerçek parçacık simülasyonlarında kullanılan formüldür — sonsuz kuvvet sorununu çözer.

### Yörünge Tahmin Yayı
Her kuyu yerleştirildiğinde aynı fizik motoru ile 70 adım × 0.024s = ~1.68 saniye simüle edilir.
Sonuçlar 0.4 saniye boyunca kesikli noktalar olarak gösterilir, sonra solar.

### Combo Sistemi
- 3.2 saniyelik pencere içinde ardışık portal vuruşları combo'yu artırır
- Maksimum ×10 combo
- Her seviyede: `Puan = 100 × level × combo + (koleksiyon / 5) × 25`

### Zorluk Eğrisi
| Seviye | Yeni Özellik |
|--------|-------------|
| 1-2    | Sabit portallar |
| 3-4    | Portallar hareket etmeye başlar |
| 5+     | Portallara ömür süresi eklenir |
| 6+     | Aynı anda 2 portal |
| 11+    | Aynı anda 3 portal |

### Görsel Efektler
- **Neon top trail:** 26 noktalık dairesel tampon, en yakın segment en kalın
- **Portal animasyonu:** Dönen çift altıgen, sin dalga titreşimi
- **Patlama:** 32+ parçacık + 8 beyaz kıvılcım, hava direnci ve yerçekimi ile
- **Kombo metni:** Yüzen, scale-in animasyonlu, renkli gölgeli

---

## Bağımlılık Listesi

```gradle
implementation 'androidx.core:core-ktx:1.12.0'
implementation 'androidx.appcompat:appcompat:1.6.1'
```
Sadece 2 bağımlılık. Oyun motoru tamamen özel yazılmıştır.
