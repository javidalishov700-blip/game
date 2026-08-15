import { describe, expect, it } from "vitest";
import { CORE_POP_ENERGY, JAM_COUNT, KICK, MIN_SPLIT_R, SPLIT_ENERGY } from "../src/const";
import { collidePair, livingCount, makeBody, resetIds, speed, walls, well } from "../src/physics";
import {
  comboBump,
  isJammed,
  kickBody,
  livingCores,
  resolveImpact,
  scoreFor,
} from "../src/rules";
import { spawnInterval, tutorialCluster, waveCluster } from "../src/spawn";

describe("kick", () => {
  it("sends the piece away from the tap, not toward it", () => {
    resetIds();
    const b = makeBody("glass", 100, 200, 0);
    kickBody(b, 70, 200);
    expect(b.vx).toBeGreaterThan(0);
    expect(Math.abs(b.vx)).toBeGreaterThan(Math.abs(b.vy));
    expect(speed(b)).toBeCloseTo(KICK, 0);
  });

  it("center taps still launch", () => {
    resetIds();
    const b = makeBody("stone", 50, 50, 0);
    const ev = kickBody(b, 50, 50);
    expect(ev?.type).toBe("kick");
    expect(speed(b)).toBeGreaterThan(100);
  });
});

describe("momentum collisions", () => {
  it("a moving piece transfers velocity into a sleeper", () => {
    resetIds();
    const a = makeBody("glass", 100, 100, 0);
    const b = makeBody("glass", 135, 100, 0);
    a.vx = 400;
    const hit = collidePair(a, b);
    expect(hit).not.toBeNull();
    expect(hit!.energy).toBeGreaterThan(0);
    expect(b.vx).toBeGreaterThan(0);
  });

  it("walls bounce instead of swallowing velocity", () => {
    resetIds();
    const b = makeBody("stone", well.x + 10, well.y + 80, 0);
    b.vx = -200;
    walls([b]);
    expect(b.vx).toBeGreaterThan(0);
    expect(b.x).toBeGreaterThanOrEqual(well.x + b.r - 0.01);
  });
});

describe("shatter and pop", () => {
  it("hard hits split glass into two smaller pieces", () => {
    resetIds();
    const a = makeBody("glass", 100, 200, 1);
    const b = makeBody("stone", 140, 200, 2);
    const res = resolveImpact(
      { a: a.id, b: b.id, energy: SPLIT_ENERGY + 10, nx: 1, ny: 0, x: 120, y: 200 },
      [a, b],
    );
    expect(a.alive).toBe(false);
    expect(res.spawned).toHaveLength(2);
    expect(res.spawned.every((p) => p.kind === "glass")).toBe(true);
    expect(res.events.some((e) => e.type === "split")).toBe(true);
  });

  it("tiny glass does not split again", () => {
    resetIds();
    const a = makeBody("glass", 100, 200, 1, { r: MIN_SPLIT_R - 2 });
    const b = makeBody("stone", 140, 200, 2);
    const res = resolveImpact(
      { a: a.id, b: b.id, energy: SPLIT_ENERGY + 40, nx: 1, ny: 0, x: 120, y: 200 },
      [a, b],
    );
    expect(a.alive).toBe(true);
    expect(res.spawned).toHaveLength(0);
  });

  it("cores pop only when the hit is hard enough", () => {
    resetIds();
    const a = makeBody("glass", 100, 200, 0);
    const core = makeBody("core", 130, 200, 0);
    const weak = resolveImpact(
      { a: a.id, b: core.id, energy: CORE_POP_ENERGY - 10, nx: 1, ny: 0, x: 115, y: 200 },
      [a, core],
    );
    expect(core.alive).toBe(true);
    expect(weak.events.some((e) => e.type === "corePop")).toBe(false);
    const hard = resolveImpact(
      { a: a.id, b: core.id, energy: CORE_POP_ENERGY + 10, nx: 1, ny: 0, x: 115, y: 200 },
      [a, core],
    );
    expect(core.alive).toBe(false);
    expect(hard.events.some((e) => e.type === "corePop")).toBe(true);
  });
});

describe("score and jam", () => {
  it("pays more for pops than splits, and scales with combo", () => {
    expect(scoreFor([{ type: "corePop", id: 1, x: 0, y: 0, color: 0 }], 2)).toBe(28);
    expect(scoreFor([{ type: "split", id: 1, x: 0, y: 0, color: 0 }], 1)).toBe(5);
    expect(
      comboBump([
        { type: "split", id: 1, x: 0, y: 0, color: 0 },
        { type: "corePop", id: 2, x: 0, y: 0, color: 0 },
      ]),
    ).toBe(2);
  });

  it("jams when the arena is overcrowded", () => {
    resetIds();
    const bodies = Array.from({ length: JAM_COUNT }, (_, i) => makeBody("stone", i, 0, 0));
    expect(isJammed(bodies, JAM_COUNT)).toBe(true);
    expect(isJammed(bodies.slice(0, 3), JAM_COUNT)).toBe(false);
    expect(livingCount(bodies)).toBe(JAM_COUNT);
  });
});

describe("spawn", () => {
  it("tutorial is a readable bank-shot: hinted glass, a bumper, cores", () => {
    resetIds();
    const pile = tutorialCluster();
    expect(pile.some((b) => b.hint && b.kind === "glass")).toBe(true);
    expect(pile.some((b) => b.kind === "stone")).toBe(true);
    expect(livingCores(pile)).toBeGreaterThanOrEqual(2);
  });

  it("waves always add at least one core and stay in the well", () => {
    resetIds();
    const pile = waveCluster(40, 9, []);
    expect(livingCores(pile)).toBeGreaterThanOrEqual(1);
    for (const b of pile) {
      expect(b.x).toBeGreaterThan(well.x);
      expect(b.x).toBeLessThan(well.x + well.w);
    }
  });

  it("spawns faster as score climbs", () => {
    expect(spawnInterval(120, 1)).toBeLessThan(spawnInterval(10, 1));
  });
});
