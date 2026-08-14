import { describe, expect, it } from "vitest";
import { ANNIHILATE_ENERGY, CONTAGION_ENERGY, CORE_POP_ENERGY } from "../src/const";
import { Body, collidePair, makeBody, resetIds, well } from "../src/physics";
import {
  annihilateWave,
  applyShockwave,
  applyWakes,
  collectEscapes,
  comboBump,
  detonate,
  invertBody,
  isOverflow,
  livingCores,
  riftDelta,
  scoreFor,
  snapAnchor,
  resolveImpact,
} from "../src/rules";
import { spawnInterval, tutorialPile, wavePile } from "../src/spawn";

function brick(x: number, y: number, color = 0, extra?: Partial<Body>): Body {
  return makeBody("brick", x, y, color, extra);
}

describe("invertBody", () => {
  it("flips mass sign and launches against gravity", () => {
    resetIds();
    const b = brick(100, 400);
    const wakes: { x: number; y: number; r: number; life: number; mag: number }[] = [];
    const ev = invertBody(b, wakes);
    expect(ev?.type).toBe("invert");
    expect(b.sign).toBe(-1);
    expect(b.vy).toBeLessThan(0);
    expect(wakes).toHaveLength(1);
  });

  it("refuses cores, anchors, and already inverted bricks", () => {
    resetIds();
    const core = makeBody("core", 0, 0, 0);
    const anchor = makeBody("anchor", 0, 0, 0);
    const flipped = brick(0, 0, 0, { sign: -1 });
    expect(invertBody(core, [])).toBeNull();
    expect(invertBody(anchor, [])).toBeNull();
    expect(invertBody(flipped, [])).toBeNull();
  });
});

describe("wakes", () => {
  it("slams nearby positive-mass bricks downward", () => {
    resetIds();
    const neighbor = brick(120, 400);
    const vy = neighbor.vy;
    applyWakes([{ x: 120, y: 400, r: 80, life: 0.32, mag: 4000 }], [neighbor], 0.05);
    expect(neighbor.vy).toBeGreaterThan(vy);
  });
});

describe("impact cascade", () => {
  it("contagion-inverts a brick on a hard antimass hit", () => {
    resetIds();
    const a = brick(100, 200, 0, { sign: -1 });
    const b = brick(140, 200, 1);
    const events = resolveImpact(
      { a: a.id, b: b.id, energy: CONTAGION_ENERGY + 10, nx: 1, ny: 0, x: 120, y: 200 },
      [a, b],
      [],
    );
    expect(b.sign).toBe(-1);
    expect(events.some((e) => e.type === "invert" && e.contagion)).toBe(true);
  });

  it("annihilates same-color antimass on contact", () => {
    resetIds();
    const a = brick(100, 200, 2, { sign: -1 });
    const b = brick(140, 200, 2, { sign: -1 });
    const events = resolveImpact(
      { a: a.id, b: b.id, energy: ANNIHILATE_ENERGY + 5, nx: 1, ny: 0, x: 120, y: 200 },
      [a, b],
      [],
    );
    expect(a.alive).toBe(false);
    expect(b.alive).toBe(false);
    expect(events.some((e) => e.type === "annihilate")).toBe(true);
  });

  it("pops a core crushed by inverted mass", () => {
    resetIds();
    const a = brick(100, 200, 0, { sign: -1 });
    const core = makeBody("core", 130, 200, 0);
    const events = resolveImpact(
      { a: a.id, b: core.id, energy: CORE_POP_ENERGY + 5, nx: 1, ny: 0, x: 115, y: 200 },
      [a, core],
      [],
    );
    expect(core.alive).toBe(false);
    expect(events.some((e) => e.type === "corePop")).toBe(true);
  });
});

describe("shockwave", () => {
  it("pops cores inside the blast and can invert nearby bricks", () => {
    resetIds();
    const core = makeBody("core", 200, 200, 1);
    const near = brick(230, 200, 0);
    const far = brick(500, 200, 0);
    const events = applyShockwave(annihilateWave(200, 200, 1), [core, near, far]);
    expect(core.alive).toBe(false);
    expect(events.some((e) => e.type === "corePop")).toBe(true);
    expect(far.sign).toBe(1);
  });
});

describe("detonate and anchors", () => {
  it("only inverted bricks detonate", () => {
    resetIds();
    expect(detonate(brick(0, 0))).toBeNull();
    const boom = detonate(brick(10, 10, 1, { sign: -1 }));
    expect(boom?.event.type).toBe("detonate");
    expect(boom?.wave.r).toBeGreaterThan(20);
  });

  it("snaps welds off an anchor and kicks the attached brick", () => {
    resetIds();
    const anchor = makeBody("anchor", 40, 400, 3);
    const hung = brick(90, 400, 0);
    const vx = hung.vx;
    const welds = [{ a: anchor.id, b: hung.id, rest: 50, broken: false }];
    const events = snapAnchor(anchor, welds, [anchor, hung]);
    expect(welds[0]!.broken).toBe(true);
    expect(events[0]?.type).toBe("snap");
    expect(hung.vx).not.toBe(vx);
  });
});

describe("fail states and scoring", () => {
  it("detects overflow when a resting brick sits above the danger line", () => {
    resetIds();
    const piled = brick(well.x + 80, well.y + 20, 0, { incoming: false, vy: 0, vx: 0 });
    expect(isOverflow([piled])).toBe(true);
    piled.incoming = true;
    expect(isOverflow([piled])).toBe(false);
  });

  it("collects inverted bricks that leave the well roof", () => {
    resetIds();
    const gone = brick(100, well.y - 40, 0, { sign: -1 });
    const events = collectEscapes([gone], well.y);
    expect(gone.alive).toBe(false);
    expect(events[0]?.type).toBe("escape");
  });

  it("scores annihilations and cores with combo, and charges rift for sloppy inversions", () => {
    expect(scoreFor([{ type: "corePop", id: 1, x: 0, y: 0, color: 0 }], 2)).toBe(24);
    expect(scoreFor([{ type: "annihilate", a: 1, b: 2, x: 0, y: 0, color: 0 }], 1)).toBe(28);
    expect(
      comboBump([
        { type: "invert", id: 1, contagion: true, x: 0, y: 0, color: 0 },
        { type: "corePop", id: 2, x: 0, y: 0, color: 0 },
      ]),
    ).toBe(2);
    expect(riftDelta([{ type: "escape", id: 1 }])).toBeGreaterThan(
      riftDelta([{ type: "corePop", id: 1, x: 0, y: 0, color: 0 }]),
    );
  });
});

describe("spawn", () => {
  it("tutorial is a playable keystone: hinted brick plus a core", () => {
    resetIds();
    const pile = tutorialPile();
    expect(pile.bodies.some((b) => b.hint && b.kind === "brick")).toBe(true);
    expect(livingCores(pile.bodies)).toBeGreaterThanOrEqual(1);
    expect(pile.welds.length).toBeGreaterThan(0);
  });

  it("wave piles always include a core and stay inside the well", () => {
    resetIds();
    const pile = wavePile(50, 7, []);
    expect(livingCores(pile.bodies)).toBeGreaterThanOrEqual(1);
    for (const b of pile.bodies) {
      expect(b.x).toBeGreaterThan(well.x);
      expect(b.x).toBeLessThan(well.x + well.w);
    }
  });

  it("spawns faster as score climbs", () => {
    expect(spawnInterval(200, 1)).toBeLessThan(spawnInterval(10, 1));
  });
});

describe("physics contact", () => {
  it("separates overlapping bricks and reports impact energy", () => {
    resetIds();
    const a = brick(100, 100);
    const b = brick(110, 100);
    b.vx = -80;
    a.vx = 80;
    const hit = collidePair(a, b);
    expect(hit).not.toBeNull();
    for (let i = 0; i < 6; i++) collidePair(a, b);
    expect(Math.hypot(b.x - a.x, b.y - a.y)).toBeGreaterThanOrEqual(a.r + b.r - 1.5);
  });
});
