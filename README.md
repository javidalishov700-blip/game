# CLACK

Kick one. Bank the rest. Shatter the glass.

Zero-g kinetic billiards: tap a piece on the side you want to push from and it flies the other way. Stones bank. Glass splits into two bouncing shards. Stars pop when something slams them hard. The miss that hurts is kicking the wrong piece and watching the rally die.

## Run it

```bash
npm install
npm run dev
```

| Input | Action |
| --- | --- |
| Tap a piece | Kick it away from your finger |
| M | Mute |

Glass shatters on a hard hit and the shards keep the combo alive. Stars (cores) only die from impact, not from a tap. Fill the arena with leftover shards and you **jam**.

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

App id: `com.javidalishov.clack`.

Stack: Vite + TypeScript + Canvas 2D + a fixed-step impulse physics loop + Capacitor. Chosen over React Native so collisions, neon, and haptics stay on one 60fps rAF thread.
