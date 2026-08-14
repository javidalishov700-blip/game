export const clamp = (v: number, min: number, max: number): number =>
  Math.max(min, Math.min(max, v));

export const lerp = (a: number, b: number, t: number): number => a + (b - a) * t;

export const len = (x: number, y: number): number => Math.hypot(x, y);

export const rand = (a = 0, b = 1): number => a + Math.random() * (b - a);

export const randInt = (a: number, b: number): number =>
  Math.floor(rand(a, b + 1));

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
