import { describe, expect, it } from "vitest";
import {
  arenaRadii,
  circularSpeed,
  clingSpiralRate,
  gravityAccel,
  inSafeRing,
  runDifficulty,
  stepCling,
  stepCoast,
} from "../src/math";

describe("circularSpeed", () => {
  it("is faster on inner rails", () => {
    expect(circularSpeed(1_000_000, 80)).toBeGreaterThan(
      circularSpeed(1_000_000, 160),
    );
  });
});

describe("gravityAccel", () => {
  it("pulls toward the center", () => {
    const g = gravityAccel(100, 0, 0, 0, 10_000);
    expect(g.ax).toBeLessThan(0);
    expect(g.ay).toBeCloseTo(0, 8);
    expect(g.dist).toBeCloseTo(100, 8);
  });
});

describe("stepCling", () => {
  it("keeps a circular rail with ~0 drift when spiral is 0", () => {
    const cx = 200;
    const cy = 350;
    const r0 = 180;
    const gm = 40_000_000;
    const spd = circularSpeed(gm, r0);
    let s = { px: cx + r0, py: cy, vx: 0, vy: spd };
    let minD = Infinity;
    let maxD = 0;
    const dt = 1 / 60;
    for (let i = 0; i < 60 * 12; i++) {
      s = stepCling(s, cx, cy, gm, dt, 0);
      const d = Math.hypot(s.px - cx, s.py - cy);
      minD = Math.min(minD, d);
      maxD = Math.max(maxD, d);
    }
    expect(maxD - minD).toBeLessThan(0.001);
  });

  it("spirals inward while holding", () => {
    const cx = 0;
    const cy = 0;
    const gm = 40_000_000;
    const spd = circularSpeed(gm, 200);
    let s = { px: 200, py: 0, vx: 0, vy: spd };
    const dt = 1 / 60;
    for (let i = 0; i < 60 * 3; i++) s = stepCling(s, cx, cy, gm, dt, 20);
    expect(Math.hypot(s.px, s.py)).toBeLessThan(200 - 50);
  });
});

describe("stepCoast", () => {
  it("flies tangent and applies drag", () => {
    const a = stepCoast({ px: 0, py: 0, vx: 100, vy: 0 }, 0.5, 0.04);
    expect(a.px).toBeGreaterThan(40);
    expect(a.py).toBeCloseTo(0, 8);
    expect(a.vx).toBeLessThan(100);
  });
});

describe("camping is lethal", () => {
  it("a hold-only run dies before 45s (flame/light close in)", () => {
    const minDim = 400;
    const cx = 200;
    const cy = 200;
    const start = arenaRadii(minDim, 0);
    let s = {
      px: cx + start.startR,
      py: cy,
      vx: 0,
      vy: circularSpeed(start.gm, start.startR),
    };
    const dt = 1 / 60;
    let died = false;
    let t = 0;
    for (let i = 0; i < 60 * 45; i++) {
      t += dt;
      const d = runDifficulty(t, 0);
      const arena = arenaRadii(minDim, d);
      s = stepCling(s, cx, cy, arena.gm, dt, clingSpiralRate(minDim, d));
      const dist = Math.hypot(s.px - cx, s.py - cy);
      if (!inSafeRing(dist, arena.flameR, arena.lightR)) {
        died = true;
        break;
      }
    }
    expect(died).toBe(true);
  });
});
