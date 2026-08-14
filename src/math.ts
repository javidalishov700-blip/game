export const clamp = (v: number, min: number, max: number): number =>
  Math.max(min, Math.min(max, v));

export const lerp = (a: number, b: number, t: number): number => a + (b - a) * t;

export const len = (x: number, y: number): number => Math.hypot(x, y);

export const rand = (a = 0, b = 1): number => a + Math.random() * (b - a);

/** Softened inverse-square gravity toward (cx, cy). */
export function gravityAccel(
  px: number,
  py: number,
  cx: number,
  cy: number,
  gm: number,
  soften = 900,
): { ax: number; ay: number; dist: number } {
  const dx = cx - px;
  const dy = cy - py;
  const dist = Math.hypot(dx, dy);
  const d2 = dx * dx + dy * dy + soften;
  const inv = 1 / Math.sqrt(d2);
  const a = gm / d2;
  return { ax: dx * inv * a, ay: dy * inv * a, dist };
}

/** Circular-orbit speed at distance `r` for a given GM (Kepler: inner = faster). */
export const circularSpeed = (gm: number, r: number): number =>
  Math.sqrt(gm / Math.max(24, r));

export function runDifficulty(time: number, score: number): number {
  return 1 - Math.exp(-(time * 0.048 + score * 0.02));
}

export function arenaRadii(
  minDim: number,
  d: number,
): { flameR: number; lightR: number; gm: number; startR: number } {
  const flameR = minDim * lerp(0.068, 0.22, d);
  const lightR = minDim * lerp(0.46, 0.248, d);
  const startR = minDim * 0.52 * (0.068 + 0.46);
  const mid = (flameR + lightR) * 0.52;
  const period = lerp(2.25, 1.5, d);
  const omega = (Math.PI * 2) / period;
  const gm = mid * mid * mid * omega * omega;
  return { flameR, lightR, gm, startR };
}

export function inSafeRing(
  dist: number,
  flameR: number,
  lightR: number,
): boolean {
  return dist >= flameR + 7 && dist <= lightR - 5;
}

export function clingSpiralRate(minDim: number, d: number): number {
  return minDim * lerp(0.01, 0.05, d);
}

export type SparkState = {
  px: number;
  py: number;
  vx: number;
  vy: number;
};

/** Polar rail + hungry inward spiral. Radius never increases while clinging. */
export function stepCling(
  s: SparkState,
  cx: number,
  cy: number,
  gm: number,
  dt: number,
  spiralPx: number,
  minDist = 12,
): SparkState {
  const dx = s.px - cx;
  const dy = s.py - cy;
  let dist = Math.max(minDist, Math.hypot(dx, dy));
  dist = Math.max(minDist, dist - spiralPx * dt);
  let ang = Math.atan2(dy, dx);
  const tx = -Math.sin(ang);
  const ty = Math.cos(ang);
  const sign = s.vx * tx + s.vy * ty >= 0 ? 1 : -1;
  const spd = circularSpeed(gm, dist);
  ang += sign * (spd / dist) * dt;
  return {
    px: cx + Math.cos(ang) * dist,
    py: cy + Math.sin(ang) * dist,
    vx: -Math.sin(ang) * spd * sign,
    vy: Math.cos(ang) * spd * sign,
  };
}

export function stepCoast(
  s: SparkState,
  dt: number,
  drag = 0.04,
  cap = Infinity,
): SparkState {
  let vx = s.vx * (1 - drag * dt);
  let vy = s.vy * (1 - drag * dt);
  const spd = Math.hypot(vx, vy);
  if (spd > cap) {
    vx = (vx / spd) * cap;
    vy = (vy / spd) * cap;
  }
  return { px: s.px + vx * dt, py: s.py + vy * dt, vx, vy };
}
