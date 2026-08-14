# WICK

Hold to orbit the flame. Release to fly. Don't burn. Don't fade.

A brand-new 2D hyper-casual game for Google Play: one button, 60fps juice, and a failure that looks so preventable viewers will install it to prove they could have lasted one more orbit.

## Step 1 — Tech stack

**TypeScript + HTML5 Canvas + Vite + Capacitor.**

Cursor iterates fastest on a typed Canvas game loop (no scene-graph ceremony, instant HMR), and a well-written 2D Canvas renderer holds 60fps on phones. Capacitor wraps the same build into a Play Store AAB without rewriting the game in a native engine.

## Step 2 — Pitch

**WICK** is a one-thumb Icarus toy. You are a spark circling a living flame: **hold anywhere to cling to its gravity and orbit**, **release to coast in a straight line** and snatch motes in the dark. Stay too close and you **burn**; drift past the lantern light and you **fade**. The flame grows and the safe ring shrinks, so every extra point is greed — the exact clip that makes someone watching a fail video think "I would have let go." Instant hold-to-retry closes the loop.

## Step 3 — Run it

```bash
npm install
npm run dev
```

Open the local URL (Vite prints it), then **hold click / space / finger** to begin.

| Input | Action |
| --- | --- |
| Hold (finger, mouse, Space, Enter) | Cling — orbit the flame, slowly spiral in |
| Release | Coast tangent (ghost dots show the path) |
| Hold after death | Retry immediately |
| M / speaker icon | Mute |

```bash
npm test
npm run build
```

### Play Store export (Android)

```bash
npm install
npm run build
npx cap add android
npx cap sync android
npx cap open android
```

Then in Android Studio: generate a signed **AAB** (`Build > Generate Signed App Bundle`) and upload it to Google Play Console. `capacitor.config.ts` already sets `appId` to `com.javidalishov.wick` and `webDir` to `dist`.

## Core loop (what to extend next)

- `src/game.ts` — cling rails, coast physics, motes, deaths, juice, HUD
- `src/audio.ts` — Web Audio synth (no sound files)
- `src/math.ts` — Kepler speed, cling/coast steppers, difficulty radii
- `src/main.ts` — canvas fit, input, rAF loop
- `test/math.test.ts` — orbit stability + camping-is-lethal regression

Difficulty is endless: the flame grows, the light shrinks, and **holding slowly spirals you inward** so camping is death. Hot motes on the inner/outer rims are worth 2×. Ash motes snuff you out. Near-missing the flame while coasting awards **SEARED**. Combos chain if you collect within 1.15s. Release is the only way to climb to a higher rail.

## License

Private prototype. Change `appId` before shipping.
