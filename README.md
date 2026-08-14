# POPDRAW

Shoot matching blocks. Stack like Tetris. Snipe flying pips. Win the DRAW.

A Play Store-style 2D mashup: Block Blast pops + Tetris stacking + gallery shooting + a pistol-duel timing beat. Easy for the first minute, then it layers more colors, faster drops, and tighter draws.

## Run it

```bash
npm install
npm run dev
```

| Input | Action |
| --- | --- |
| Tap a column | Shoot your current color up that stack |
| Tap NEXT ball | Swap current / next (like puzzle shooters) |
| Tap a flying pip | Bonus + bomb or rainbow shot |
| DRAW! overlay | Tap while the needle is in the green |
| 1–5 / Space | Shoot columns (desktop) |
| S | Swap |
| M | Mute |

Same color as the top of a stack = **pop the whole connected group** (Block Blast). Wrong color = **the shot stacks on top** (Tetris). If any column hits the red ceiling, you're stacked out.

Difficulty ramp: 2 colors and slow garbage at the start → 3rd color ~40 pts → pips ~10 pts → DRAW duels ~35 pts → faster drops later. Garbage colors are biased toward what's already on the board so you are not starved of matches.

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

App id: `com.javidalishov.popdraw`.
