import { ANCHOR_R, BRICK_R, CORE_R, MAX_BODIES, PALETTE_N } from "./const";
import { Rng } from "./math";
import { Body, Weld, makeBody, well } from "./physics";

export type Structure = { bodies: Body[]; welds: Weld[] };

function hexCell(col: number, row: number, cx: number, baseY: number, r: number) {
  const dx = r * 2 * 0.94;
  const dy = r * Math.sqrt(3);
  const ox = row % 2 === 0 ? 0 : dx * 0.5;
  return {
    x: cx + ox + col * dx,
    y: baseY - row * dy,
  };
}

export function tutorialPile(): Structure {
  const cx = well.x + well.w * 0.5;
  const floor = well.y + well.h - BRICK_R - 2;
  const bodies: Body[] = [];
  const welds: Weld[] = [];

  const key = makeBody("brick", cx, floor, 0, { hint: true });
  const l = makeBody("brick", cx - BRICK_R * 1.85, floor, 2);
  const r = makeBody("brick", cx + BRICK_R * 1.85, floor, 2);
  const mid = makeBody("brick", cx, floor - BRICK_R * 1.72, 0);
  const core = makeBody("core", cx, floor - BRICK_R * 3.35, 0);
  const aL = makeBody("anchor", well.x + 28, floor - BRICK_R * 1.6, 3);
  const aR = makeBody("anchor", well.x + well.w - 28, floor - BRICK_R * 1.6, 3);
  const topL = makeBody("brick", cx - BRICK_R * 1.1, floor - BRICK_R * 3.2, 1);
  const topR = makeBody("brick", cx + BRICK_R * 1.1, floor - BRICK_R * 3.2, 1);
  bodies.push(key, l, r, mid, core, aL, aR, topL, topR);

  welds.push(
    { a: aL.id, b: l.id, rest: Math.hypot(aL.x - l.x, aL.y - l.y), broken: false },
    { a: aR.id, b: r.id, rest: Math.hypot(aR.x - r.x, aR.y - r.y), broken: false },
  );
  return { bodies, welds };
}

function canPlace(x: number, y: number, r: number, bodies: Body[]): boolean {
  if (x - r < well.x + 4 || x + r > well.x + well.w - 4) return false;
  if (y + r > well.y + well.h - 2) return false;
  for (const b of bodies) {
    if (!b.alive) continue;
    if (Math.hypot(b.x - x, b.y - y) < r + b.r - 1.2) return false;
  }
  return true;
}

export function wavePile(score: number, seed: number, existing: Body[]): Structure {
  const rng = new Rng(seed);
  const bodies: Body[] = [];
  const welds: Weld[] = [];
  const colors = score < 40 ? 2 : score < 110 ? 3 : PALETTE_N;
  const floors = score < 25 ? 3 : score < 80 ? 4 : score < 160 ? 5 : 6;
  const cols = score < 60 ? 4 : 5;
  const cx = well.x + well.w * 0.5 - ((cols - 1) * BRICK_R * 0.94);
  const floor = well.y + well.h - BRICK_R - 2;
  const incoming = existing.some((b) => b.alive);
  const baseY = incoming ? well.y + 36 : floor;

  const placed: Body[] = existing.filter((b) => b.alive);

  for (let row = 0; row < floors; row++) {
    const n = row % 2 === 0 ? cols : cols - 1;
    for (let col = 0; col < n; col++) {
      if (placed.length + bodies.length >= MAX_BODIES - 4) break;
      const p = hexCell(col, row, cx, baseY, BRICK_R);
      const y = incoming ? well.y + 28 + row * BRICK_R * 1.72 : p.y;
      const x = incoming ? well.x + well.w * 0.5 + (col - (n - 1) / 2) * BRICK_R * 1.9 : p.x;
      const isCore = row >= 1 && rng.chance(score < 20 ? 0.16 : 0.22);
      const isAnchor =
        !isCore && (col === 0 || col === n - 1) && row === 1 && rng.chance(0.55);
      const kind = isCore ? "core" : isAnchor ? "anchor" : "brick";
      const r = kind === "core" ? CORE_R : kind === "anchor" ? ANCHOR_R : BRICK_R;
      if (!canPlace(x, y, r, placed.concat(bodies))) continue;
      const color = rng.int(0, colors - 1);
      const body = makeBody(kind, x, y, color, { incoming });
      bodies.push(body);
    }
  }

  if (!bodies.some((b) => b.kind === "core")) {
    const host = bodies.find((b) => b.kind === "brick");
    if (host && canPlace(host.x, host.y - BRICK_R * 1.6, CORE_R, placed.concat(bodies))) {
      bodies.push(
        makeBody("core", host.x, host.y - BRICK_R * 1.6, host.color, { incoming }),
      );
    }
  }

  const anchors = bodies.filter((b) => b.kind === "anchor");
  const bricks = bodies.filter((b) => b.kind === "brick");
  for (const a of anchors) {
    let best: Body | null = null;
    let bestD = 1e9;
    for (const b of bricks) {
      const d = Math.hypot(a.x - b.x, a.y - b.y);
      if (d < bestD && d < BRICK_R * 3.2) {
        best = b;
        bestD = d;
      }
    }
    if (best) {
      welds.push({ a: a.id, b: best.id, rest: bestD, broken: false });
    }
  }

  const extra = bricks.length >= 2 ? Math.min(2, Math.floor(score / 90)) : 0;
  for (let i = 0; i < extra; i++) {
    const a = rng.pick(bricks);
    const b = rng.pick(bricks);
    if (a.id === b.id) continue;
    const d = Math.hypot(a.x - b.x, a.y - b.y);
    if (d > BRICK_R * 1.2 && d < BRICK_R * 3.4) {
      welds.push({ a: a.id, b: b.id, rest: d, broken: false });
    }
  }

  return { bodies, welds };
}

export function spawnInterval(score: number, cores: number): number {
  if (cores >= 5) return 7.5;
  if (score < 30) return 6.4;
  if (score < 80) return 5.4;
  if (score < 160) return 4.6;
  return 3.9;
}
