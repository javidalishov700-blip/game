# Submitting Slice & Blast to the App Store

Everything App Store Connect will ask for, already written. Work top to bottom; each step is
either a link to paste or a box to fill.

## 0. Prerequisites already handled by the build

You do not need to configure any of these by hand — `SliceBlastBuild.PrepareProject()` and
`IosPostProcess` apply them to every build:

| requirement | value | where it comes from |
|---|---|---|
| Bundle identifier | `com.javidalishov.sliceblast` | `SliceBlastBuild` |
| Orientation | portrait only, `UIRequiresFullScreen` | `SliceBlastBuild`, `IosPostProcess` |
| Device family | iPhone only | `SliceBlastBuild` |
| App icon | 1024×1024, opaque, no alpha, in the asset catalog | `IosPostProcess` |
| `CFBundleIconName` | `AppIcon` | `IosPostProcess` |
| Export compliance | `ITSAppUsesNonExemptEncryption = false` | `IosPostProcess` |
| Build number | minutes since 2024-01-01, always climbing | `IosPostProcess` + `codemagic.yaml` |
| Unity splash | disabled | `SliceBlastBuild.ApplySplashSettings()` |

Getting a build to TestFlight is a separate document: [SHIPPING.md](SHIPPING.md).

## 1. The legal pages are already live

Apple requires a reachable **Privacy Policy URL** and a **Support URL** before the app can be
submitted. They're served by real GitHub Pages, riding the deployment already switched on for
the `javidalishov700-blip/steady-site` repository, under their own path:

| page | URL |
|---|---|
| Support | `https://javidalishov700-blip.github.io/steady-site/sliceblast/legal/support.html` |
| Privacy Policy | `https://javidalishov700-blip.github.io/steady-site/sliceblast/legal/privacy.html` |
| Terms of Use | `https://javidalishov700-blip.github.io/steady-site/sliceblast/legal/terms.html` |

These are the exact URLs hard-coded in `GameHud.PrivacyUrl` / `GameHud.TermsUrl`, and the ones
to paste into App Store Connect in section 3 below. The source files live in the `steady-site`
repository at `public/sliceblast/legal/`, not in this one — edit them there if the copy ever
needs to change; a push to `main` redeploys automatically via that repo's `pages.yml`.

The copies of the same three pages under this repository's own `docs/` folder are unused by the
app for now. Kept in case this repository ever gets its own GitHub Pages switched on — at that
point, update the two constants in `GameHud.cs` and the two URLs in section 3 below and nothing
else changes.

## 2. Create the app record

App Store Connect → **My Apps → + → New App**.

| field | value |
|---|---|
| Platform | iOS |
| Name | `Slice & Blast` (if taken: `Slice & Blast: Tower Stack`) |
| Primary language | English (U.S.) |
| Bundle ID | `com.javidalishov.sliceblast` |
| SKU | `sliceblast-ios-001` |
| User Access | Full Access |

## 3. Metadata to paste

**Subtitle** (30 characters max)

```
Stack, slice, detonate
```

**Promotional text** (170 max — editable without a new build)

```
One thumb, one tower, one perfect drop after another. Chain three and blow the top off it.
```

**Description**

```
Tap. Drop. Land it dead centre.

Slice & Blast is a one-thumb stacking game about precision. Every block swings across the top
of your tower and waits for one tap. Land it perfectly and you keep your full width. Miss, and
the overhang is sliced off and gone for good — and the tower gets that little bit harder.

Chain three perfect drops and the top of the tower detonates, handing your width back and
turning a careful run into a fast one.

RARE BLOCKS, REAL REWARDS
Every so often something else slides in, and each one behaves like what it looks like:

• NEON — a different colour every time. Land it and it charges the three layers beneath it,
  then takes them with it.
• ELECTRIC — arcs circling its shell. Land it and a current runs the whole height of the
  tower, doubling every point for fifteen seconds.
• GLASS — a shield that absorbs one mistake completely.
• STEEL — heavy, fast, and it widens the tower back out.

Miss one and it simply shatters. Specials never damage your tower — they are pure upside, and
they are worth waiting for.

BUILT TO BE PICKED UP
• One tap, no tutorial, no menus in the way
• Sixty frames a second, every frame
• Completely offline
• No ads, no purchases, no accounts, no data collected

Free, finished, and the same game every time you open it.
```

**Keywords** (100 characters max, comma separated, no spaces after commas)

```
stack,tower,block,slice,arcade,casual,one tap,offline,no ads,reflex,precision,combo,neon,blast
```

**Support URL** → `https://javidalishov700-blip.github.io/steady-site/sliceblast/legal/support.html`
**Marketing URL** → *(leave blank, or the same URL)*
**Privacy Policy URL** → `https://javidalishov700-blip.github.io/steady-site/sliceblast/legal/privacy.html`

**Category** → Primary: **Games → Arcade**. Secondary: **Games → Puzzle**.

**Copyright** → `2026 Javid Alishov`

**What's New in This Version** (first release)

```
The first release of Slice & Blast.
```

## 4. App Privacy

App Store Connect → your app → **App Privacy** → *Get Started*.

> **Do you or your third-party partners collect data from this app?** → **No**

That single answer is the whole section, and it is accurate: the app makes no network requests,
embeds no analytics, advertising or attribution SDK, and stores only the best score and the two
audio/haptics switches in local app preferences. See `docs/privacy-policy.html`.

## 5. Age rating

**Age Rating → Edit**, answer **None** to every content question. The result is **4+**.

There is no unrestricted web access (the two title-screen links open specific fixed pages in
Safari, which is not "web browsing" for rating purposes), no gambling, no user-generated
content and no contests.

## 6. Screenshots

Apple requires at least one iPhone screenshot set, and the exact pixel size it asks for has
moved around over time — check the numbers printed on **App Store Connect → your app →
Distribution → App Store → [version] → Previews and Screenshots → iPhone** (the "View All
Sizes in Media Manager" link on that row) rather than trusting a fixed number here. As of this
writing that row asks for the **6.5-inch** size: **1242 × 2688** or **1284 × 2778** portrait,
PNG or JPEG, no alpha channel. Only the first 3 are shown on the App Store install sheet, but
uploading up to 10 is fine; three to five is the sensible number.

The easiest way to produce them at exactly the right size, from Windows:

1. Unity → **Game** view → resolution dropdown → **+** → Type *Fixed Resolution*,
   Width `1242`, Height `2688` (match whatever the Media Manager page currently shows if it
   differs from this).
2. Press Play and get to the moment you want.
3. **Slice & Blast → Capture App Store Screenshot** (or `Ctrl+Shift+S`). Files land in
   `unity/SliceBlast/Screenshots/`. The tool accepts either 6.5" or 6.9" sizes and warns in
   the Console if the Game view is set to neither.

Worth capturing: the title screen, a tall tower mid-run, a blast going off, an electric block
with the current running, and the run-over screen with a good score.

## 7. Build, upload, submit

1. Follow [SHIPPING.md](SHIPPING.md) to get a build into TestFlight.
2. Install it from TestFlight and play it once on the device — this is also your last check
   that the title-screen legal links open.
3. App Store Connect → your app → **Distribution** → pick the build.
4. **App Review Information**: no sign-in required, so leave the account fields empty. Notes:

```
Slice & Blast is a single-player offline arcade game. No account, no network access, no
purchases, no ads. Tap anywhere on screen to drop the moving block. The Privacy Policy and
Terms of Use links on the title screen open pages in Safari.
```

5. **Version Release** → *Automatically release this version*.
6. **Add for Review → Submit**.

## 8. If review comes back

The three rejections this app's shape usually attracts, and where each is already answered:

- *Guideline 5.1.1 — privacy policy* → the URL in section 1 and the "No" in section 4 must
  agree with each other. They do.
- *Guideline 2.1 — app completeness* → the reviewer could not get past a screen. Check the
  build you submitted actually starts a run from the title screen tap.
- *Missing icon / Info.plist value* → an iOS build made while the Unity editor's active build
  target was still Windows. Switch the platform, let scripts recompile, then build again —
  `SliceBlastBuild.Run()` refuses the first attempt on purpose for exactly this reason.

## 9. Turning on ads (a later version, not this one)

`AdsManager.cs` (interstitial after every 3rd run, plus an optional rewarded "continue" on
the run-over screen) is already written, but sits inert behind the `SLICEBLAST_ADS_ENABLED`
scripting define until three real, one-time steps happen — none of them possible without a
human at a keyboard, and none of them belong in the version already submitted above:

1. **Import the plugin.** Download the Google Mobile Ads Unity Plugin `.unitypackage` from
   the releases page of `github.com/googleads/googleads-mobile-unity` and import it via
   Unity → Assets → Import Package → Custom Package.
2. **Get real IDs.** Create an app at admob.google.com, take its App ID and the interstitial
   and rewarded ad unit IDs. Put the App ID in `IosPostProcess.AdMobAppId`, and the two ad
   unit IDs in `AdsManager`'s `InterstitialAdUnitId`/`RewardedAdUnitId` — both files say
   exactly where. Until then the code runs on Google's public test IDs, which always serve a
   test ad and never pay out.
3. **Flip the switch.** Unity → Project Settings → Player → iOS tab → Scripting Define
   Symbols → add `SLICEBLAST_ADS_ENABLED`. Nothing above does anything until this is set.

Then, before submitting that build:

- **App Privacy** answer changes from "No" to declaring data collected — Identifiers
  (Advertising Data / IDFA) and Usage Data, "used for third-party advertising", collection
  linked to the user, no data used to track across other companies' apps beyond what AdMob
  itself does. Google publishes the exact answers to give at
  `support.google.com/admob/answer/9760862` — copy them in rather than guessing.
- **Privacy Policy** text needs a paragraph naming Google AdMob, the advertising identifier,
  and a link to Google's own privacy policy. The live pages are in the `steady-site`
  repository at `public/sliceblast/legal/privacy.html` (see section 1) — update that file,
  not `docs/privacy-policy.html` in this repository, which is the unused spare copy.
- Ship this as its **own version** (e.g. 1.1) once 1.0 has cleared review — an app already in
  Apple's review queue cannot have its behaviour or its App Privacy answers changed under it.
