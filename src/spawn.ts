import { CORE_R, GLASS_R, MAX_BODIES, PALETTE_N, STONE_R } from "./const";
import { Rng } from "./math";
import { Body, makeBody, well } from "./physics";

function canPlace(x: number, y: number, r: number, bodies: Body[]): boolean {
  if (x - r < well.x + 8 || x + r > well.x + well.w - 8) return false;
  if (y - r < well.y + 8 || y + r > well.y + well.h - 8) return false;
  for (const b of bodies) {
    if (!b.alive) continue;
    if (Math.hypot(b.x - x, b.y - y) < r + b.r + 6) return false;
  }
  return true;
}

export function tutorialCluster(): Body[] {
  const cx = well.x + well.w * 0.34;
  const cy = well.y + well.h * 0.52;
  const glass = makeBody("glass", cx, cy, 0, { hint: true });
  const stone = makeBody("stone", cx + STONE_R * 2.35, cy, 2);
  const coreA = makeBody("core", cx + STONE_R * 4.15, cy - CORE_R * 1.8, 1);
  const coreB = makeBody("core", cx + STONE_R * 4.15, cy + CORE_R * 1.8, 1);
  return [glass, stone, coreA, coreB];
}

export function waveCluster(score: number, seed: number, existing: Body[]): Body[] {
  const rng = new Rng(seed);
  const bodies: Body[] = [];
  const placed = existing.filter((b) => b.alive);
  const colors = score < 50 ? 2 : score < 120 ? 3 : PALETTE_N;
  const n = score < 20 ? 5 : score < 70 ? 7 : score < 150 ? 9 : 11;
  const coresWanted = score < 30 ? 2 : score < 90 ? 3 : 4;

  const tryKind = (kind: Body["kind"], color: number): Body | null => {
    const r = kind === "core" ? CORE_R : kind === "glass" ? GLASS_R : STONE_R;
    for (let k = 0; k < 24; k++) {
      const x = rng.range(well.x + 28, well.x + well.w - 28);
      const y = rng.range(well.y + 36, well.y + well.h - 36);
      if (!canPlace(x, y, r, placed.concat(bodies))) continue;
      return makeBody(kind, x, y, color);
    }
    return null;
  };

  for (let i = 0; i < coresWanted; i++) {
    if (placed.length + bodies.length >= MAX_BODIES - 2) break;
    const b = tryKind("core", rng.int(0, colors - 1));
    if (b) bodies.push(b);
  }
  for (let i = 0; i < n; i++) {
    if (placed.length + bodies.length >= MAX_BODIES - 2) break;
    const kind: Body["kind"] = rng.chance(0.55) ? "glass" : "stone";
    const b = tryKind(kind, rng.int(0, colors - 1));
    if (b) bodies.push(b);
  }
  if (!bodies.some((b) => b.kind === "core")) {
    const b = tryKind("core", 0) ?? makeBody("core", well.x + well.w * 0.5, well.y + well.h * 0.5, 0);
    bodies.push(b);
  }
  if (!bodies.some((b) => b.kind === "glass")) {
    const b = tryKind("glass", 0);
    if (b) bodies.push(b);
  }
  return bodies;
}

export function spawnInterval(score: number, cores: number): number {
  if (cores >= 4) return 8.5;
  if (score < 30) return 6.8;
  if (score < 90) return 5.6;
  return 4.6;
}
