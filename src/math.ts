export const clamp = (v: number, min: number, max: number): number =>
  Math.max(min, Math.min(max, v));

export const lerp = (a: number, b: number, t: number): number => a + (b - a) * t;

export const len = (x: number, y: number): number => Math.hypot(x, y);

export const rand = (a = 0, b = 1): number => a + Math.random() * (b - a);

export const randInt = (a: number, b: number): number =>
  Math.floor(rand(a, b + 1));

export const pick = <T>(xs: T[]): T => xs[Math.floor(Math.random() * xs.length)]!;
