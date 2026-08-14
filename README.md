# ANTIMASS

Invert one brick. Weaponize the collapse.

A physics demolition well: you do not snipe targets. You flip a brick's mass sign so gravity yanks it the wrong way, then ride the chain reaction — impact contagion, snapped support cables, same-color antimass annihilation, and cores crushed in the wreckage.

The miss that hurts is not a missed shot. It is inverting the wrong keystone and watching a 12x annihilation fly into the ceiling.

## Run it

```bash
npm install
npm run dev
```

| Input | Action |
| --- | --- |
| Tap a brick | Invert its mass (it falls *up*) and slam a gravity wake into neighbors |
| Tap an inverted brick | Detonate it into a shockwave |
| Tap a hex **anchor** | Snap every cable welded to it |
| M | Mute |

Same-color antimass collisions **annihilate** and invert everything in the blast. Cores (the star orbs) only die when crushed by inverted mass or a shockwave. Spill too much antimass through the roof and the **rift** ruptures; let the pile rest above the red line and you **overflow**.

```bash
npm test
npm run build
```

### Play Store export (Android)

```bash
npm run build
npx cap add android
npx cap sync android
npx cap open android
```

App id: `com.javidalishov.antimass`.

Stack: Vite + TypeScript + Canvas 2D + a fixed-step impulse physics loop + Capacitor. Chosen over React Native so the sim and neon pass stay on one 60fps rAF thread with no bridge.
