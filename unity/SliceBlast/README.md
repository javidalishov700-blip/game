# Slice & Blast

Hyper-casual blok yığma oyununun çekirdek döngüsü. Tek dokunuş, ego-boost snap, dilimleme ve blast.

## 1. Unity'de açmak (yerel)

1. Unity Hub → **Add** → `unity/SliceBlast` klasörünü seç. **Unity 6 LTS (6000.3.x)** hedefleniyor;
   Unity 2021.3 / 2022.3 LTS de çalışır. Farklı bir sürümle açarsan Unity projeyi sessizce yükseltir —
   projede yalnızca script ve JSON olduğu için bu risksizdir.
2. Proje açılınca menüden **Slice & Blast → Create Playable Scene** çalıştır.
   Bu, `Assets/Scenes/Main.unity` sahnesini üretir ve içine tek bir `SliceBlastBootstrap` objesi koyar.
3. **Play**'e bas. Kamera, ışık, havuzlar, blok ve debris prefab'ları çalışma anında kurulur —
   elle sahne kurulumu gerekmiyor.

Kontrol: ekrana dokunmak / sol tık / boşluk tuşu bloğu bırakır.

## 2. Codemagic ile iPhone'a almak

Repo kökündeki `codemagic.yaml` iki workflow tanımlar: `slice-blast-ios` (TestFlight) ve
`slice-blast-android` (APK).

### Gerekenler

| Ne | Neden |
| --- | --- |
| Apple Developer Program üyeliği (yıllık $99) | iPhone'a kurulum/TestFlight imzası için zorunlu |
| App Store Connect API key | Codemagic'in imzalama ve TestFlight yüklemesi için |
| Unity hesabı + lisans | CI makinesinde editörü aktive etmek için |
| Codemagic hesabı (macOS M2 instance) | Unity + Xcode kurulu build makinesi |

### Adımlar

1. Codemagic'te repoyu bağla (**Add application → GitHub → javidalishov700-blip/game**).
2. **Teams → Integrations → App Store Connect**'te API key ekle ve `codemagic.yaml` içindeki
   `app_store_connect: SliceBlast ASC Key` satırını kendi key adınla değiştir.
3. **Environment variables** bölümünde `unity` adlı bir grup oluştur:
   - `UNITY_HOME` → `/Applications/Unity/Hub/Editor/<sürüm>/Unity.app`
   - `UNITY_EMAIL`, `UNITY_PASSWORD`, `UNITY_SERIAL` (hepsi secure)
4. App Store Connect'te `com.sliceblast.game` bundle ID'si ile uygulama kaydı aç
   (başka bir ID kullanacaksan `codemagic.yaml` içindeki `BUNDLE_ID` ve `ios_signing.bundle_identifier`
   alanlarını da değiştir).
5. `claude/slice-blast-core-sihw5b` dalına push at veya Codemagic'ten **Start new build** de.
6. Build bitince TestFlight'tan iPhone'una kur.

### Unity Personal lisansı kullanıyorsan

`UNITY_SERIAL` yalnızca Plus/Pro içindir. Personal'da Unity'nin manuel aktivasyon akışı gerekir:
yerel makinede `Unity -batchmode -createManualActivationFile` ile `.alf` üret, `license.unity3d.com`
üzerinden `.ulf` dosyasını al, içeriğini Codemagic'e `UNITY_LICENSE` secure değişkeni olarak koy ve
"Activate Unity licence" adımını şununla değiştir:

```bash
echo "$UNITY_LICENSE" > /tmp/unity.ulf
"$UNITY_BIN" -batchmode -quit -nographics -logFile - -manualLicenseFile /tmp/unity.ulf || true
```

### Apple Developer üyeliği olmadan

Ad-hoc veya TestFlight imzası mümkün değil. O durumda `slice-blast-android` workflow'u ile APK alıp
Android'de test etmek en hızlı yol; iPhone için tek alternatif kendi Mac'inde Xcode'un ücretsiz
7 günlük provisioning'i ile kurmak.


## Blok tipleri

| Blok | Hareket | Tam isabet | Iska |
|---|---|---|---|
| **Standard** | normal | istiflenir, seri artar | taşan kısım kesilir ve düşer |
| **Neon** | normal | üstteki 3 katmanı patlatır, tabanı genişletir | yok olur, kule sağlam |
| **Electric** | %10 hızlı | 15 saniye boyunca tüm puanlar ×2 (üst üste gelirse süre yenilenir) | yok olur, kule sağlam |
| **Glass** | %5 yavaş | kalkan verir: sıradaki Standard ıskası kuleye zarar vermez | yok olur, kule sağlam |
| **Steel** | **1.5× hızlı** | platformu %15 genişletir | yok olur, kule sağlam |
| **Glitch** | %15 hızlı, yolun yarısında **ileri zıplar** | jackpot puan | yok olur, kule sağlam |

Özel blok ıskası kuleyi asla kesmez: blok havuza döner, combo sıfırlanır ve **sonraki blok
zorunlu olarak Standard** gelir — ritim böyle sıfırlanır. Özel bloklar arka arkaya gelmez,
ilk 5 blok her zaman Standard'dır.

## Mimari

- `GameEvents` — statik sinyal veriyolu; oyun mantığı yayınlar, sunum katmanı dinler
- `PoolManager` / `BlockPool` — blok, moloz, kıvılcım ve şok dalgası havuzları; oyun sırasında sıfır tahsis
- `BlockSpawner` — tip kurası, ağırlıklar, zorunlu Standard kuralı, konumlandırma
- `GameFlowManager` — skor, combo, 15 saniyelik çarpan, kalkan, blast, oyun sonu
- `BlockSlicer` — girdi, mıknatıs doğrulaması, dilimleme matematiği, özel blok yok oluşu

## 3. Ayarlama

Tüm dengeleme değerleri `GameFlowManager` ve `BlockSlicer` inspector alanlarında:

- **Invisible Tutorial** — ilk 3 blok 0.5x hızda, sonra yumuşak rampa.
- **Dynamic Speed** — kule yükseldikçe hızlanır; kombo kırılınca yavaşlar, zamanla toparlar.
- **Ego Boost** — `perfectThreshold` 0.05 birim; seri ve hızla birlikte görünmez şekilde genişler.
- **Blast Flow State** — 3 perfect üst üste → o katmanlar patlar (Block Blast'taki satır
  temizleme hissi): her katman için büyük puan bonusu, platform genişler, skor çarpanı bir
  kademe artar (×2 → ×5) ve blok hızı çarpanla birlikte yükselir. Taban platform asla yok olmaz.
- **Sunum** — sesler çalışma anında sentezleniyor (`ProceduralSfx`), perfect serisi pentatonik
  bir merdiven çalar; HUD kod içinde kurulur (`GameHud`), en yüksek skor `PlayerPrefs`'te tutulur,
  uygulama ikonu build sırasında çizilir (`SliceBlastAssets`).
