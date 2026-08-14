import {
  ANNIHILATE_ENERGY,
  ANNIHILATE_RADIUS,
  CONTAGION_ENERGY,
  CORE_POP_ENERGY,
  DETONATE_RADIUS,
  LAUNCH,
  RIFT_CORE_RELIEF,
  RIFT_ESCAPE,
  RIFT_INVERT,
  SHOCK_FORCE,
  WAKE_G,
  WAKE_LIFE,
} from "./const";
import { clamp } from "./math";
import { Body, Contact, Weld, dangerY, invMass, speed } from "./physics";

export type Wake = {
  x: number;
  y: number;
  r: number;
  life: number;
  mag: number;
};

export type Shockwave = {
  x: number;
  y: number;
  r: number;
  force: number;
  invert: boolean;
  color: number;
};

export type SimEvent =
  | { type: "invert"; id: number; contagion: boolean; x: number; y: number; color: number }
  | { type: "annihilate"; a: number; b: number; x: number; y: number; color: number }
  | { type: "corePop"; id: number; x: number; y: number; color: number }
  | { type: "snap"; x: number; y: number }
  | { type: "detonate"; id: number; x: number; y: number; color: number }
  | { type: "escape"; id: number };

export function invertBody(body: Body, wakes: Wake[]): SimEvent | null {
  if (!body.alive || body.kind !== "brick" || body.sign < 0) return null;
  body.sign = -1;
  body.heat = 1;
  body.incoming = false;
  body.vy = Math.min(body.vy, 0) - LAUNCH;
  body.omega += (Math.random() - 0.5) * 8;
  wakes.push({
    x: body.x,
    y: body.y,
    r: body.r * 3.4,
    life: WAKE_LIFE,
    mag: WAKE_G * Math.min(2.2, body.mass),
  });
  return { type: "invert", id: body.id, contagion: false, x: body.x, y: body.y, color: body.color };
}

export function applyWakes(wakes: Wake[], bodies: Body[], dt: number): void {
  for (let i = wakes.length - 1; i >= 0; i--) {
    const w = wakes[i]!;
    w.life -= dt;
    const u = clamp(w.life / WAKE_LIFE, 0, 1);
    for (const b of bodies) {
      if (!b.alive || b.pinned || b.sign < 0) continue;
      const d = Math.hypot(b.x - w.x, b.y - w.y);
      if (d > w.r || d < 0.001) continue;
      const falloff = 1 - d / w.r;
      b.vy += w.mag * falloff * u * dt;
    }
    if (w.life <= 0) wakes.splice(i, 1);
  }
}

export function applyShockwave(wave: Shockwave, bodies: Body[]): SimEvent[] {
  const events: SimEvent[] = [];
  for (const b of bodies) {
    if (!b.alive) continue;
    const d = Math.hypot(b.x - wave.x, b.y - wave.y);
    if (d > wave.r || d < 0.001) continue;
    const u = 1 - d / wave.r;
    const nx = (b.x - wave.x) / d;
    const ny = (b.y - wave.y) / d;
    const im = invMass(b);
    if (im > 0) {
      const kick = wave.force * u * im * b.mass;
      b.vx += nx * kick;
      b.vy += ny * kick - 40 * u;
      b.heat = Math.max(b.heat, 0.7 * u);
    }
    if (b.kind === "core") {
      b.alive = false;
      events.push({ type: "corePop", id: b.id, x: b.x, y: b.y, color: b.color });
    } else if (
      wave.invert &&
      b.kind === "brick" &&
      b.sign > 0 &&
      u > 0.45 &&
      d < wave.r * 0.72
    ) {
      const ev = invertBody(b, []);
      if (ev) {
        ev.contagion = true;
        events.push(ev);
      }
    }
  }
  return events;
}

function bodyById(bodies: Body[], id: number): Body | undefined {
  return bodies.find((b) => b.id === id);
}

export function resolveImpact(
  contact: Contact,
  bodies: Body[],
  wakes: Wake[],
): SimEvent[] {
  const a = bodyById(bodies, contact.a);
  const b = bodyById(bodies, contact.b);
  if (!a || !b) return [];
  const events: SimEvent[] = [];

  const pair: [Body, Body][] = [
    [a, b],
    [b, a],
  ];
  for (const [src, dst] of pair) {
    if (src.sign < 0 && dst.kind === "core" && contact.energy >= CORE_POP_ENERGY && dst.alive) {
      dst.alive = false;
      events.push({ type: "corePop", id: dst.id, x: dst.x, y: dst.y, color: dst.color });
    }
    if (
      src.sign < 0 &&
      dst.kind === "brick" &&
      dst.sign > 0 &&
      contact.energy >= CONTAGION_ENERGY
    ) {
      const ev = invertBody(dst, wakes);
      if (ev) {
        ev.contagion = true;
        events.push(ev);
      }
    }
  }

  if (
    a.kind === "brick" &&
    b.kind === "brick" &&
    a.sign < 0 &&
    b.sign < 0 &&
    a.color === b.color &&
    contact.energy >= ANNIHILATE_ENERGY &&
    a.alive &&
    b.alive
  ) {
    a.alive = false;
    b.alive = false;
    events.push({
      type: "annihilate",
      a: a.id,
      b: b.id,
      x: contact.x,
      y: contact.y,
      color: a.color,
    });
  }

  return events;
}

export function detonate(body: Body): { event: SimEvent; wave: Shockwave } | null {
  if (!body.alive || body.kind !== "brick" || body.sign > 0) return null;
  body.alive = false;
  return {
    event: { type: "detonate", id: body.id, x: body.x, y: body.y, color: body.color },
    wave: {
      x: body.x,
      y: body.y,
      r: DETONATE_RADIUS,
      force: SHOCK_FORCE * 0.82,
      invert: true,
      color: body.color,
    },
  };
}

export function annihilateWave(x: number, y: number, color: number): Shockwave {
  return {
    x,
    y,
    r: ANNIHILATE_RADIUS,
    force: SHOCK_FORCE * 1.35,
    invert: true,
    color,
  };
}

export function snapAnchor(anchor: Body, welds: Weld[], bodies: Body[]): SimEvent[] {
  if (!anchor.alive || anchor.kind !== "anchor") return [];
  const events: SimEvent[] = [];
  let snapped = false;
  for (const w of welds) {
    if (w.broken) continue;
    if (w.a !== anchor.id && w.b !== anchor.id) continue;
    w.broken = true;
    snapped = true;
    const otherId = w.a === anchor.id ? w.b : w.a;
    const other = bodies.find((b) => b.id === otherId);
    if (!other?.alive || other.pinned) continue;
    const dx = other.x - anchor.x;
    const dy = other.y - anchor.y;
    const d = Math.hypot(dx, dy) || 1;
    other.vx += (dx / d) * 380;
    other.vy += (dy / d) * 220 - 80;
    other.heat = 1;
  }
  if (snapped) events.push({ type: "snap", x: anchor.x, y: anchor.y });
  return events;
}

export function collectEscapes(bodies: Body[], top: number): SimEvent[] {
  const events: SimEvent[] = [];
  for (const b of bodies) {
    if (!b.alive || b.pinned) continue;
    if (b.sign < 0 && b.y + b.r < top) {
      b.alive = false;
      events.push({ type: "escape", id: b.id });
    }
  }
  return events;
}

export function isOverflow(bodies: Body[]): boolean {
  for (const b of bodies) {
    if (!b.alive || b.incoming || b.sign < 0 || b.kind === "core") continue;
    if (b.y < dangerY && speed(b) < 42) return true;
  }
  return false;
}

export function markSettledIncoming(bodies: Body[]): void {
  for (const b of bodies) {
    if (b.incoming && b.y > dangerY + b.r + 8) b.incoming = false;
  }
}

export function riftDelta(events: SimEvent[]): number {
  let d = 0;
  for (const e of events) {
    if (e.type === "invert" && !e.contagion) d += RIFT_INVERT;
    if (e.type === "escape") d += RIFT_ESCAPE;
    if (e.type === "corePop") d -= RIFT_CORE_RELIEF;
    if (e.type === "annihilate") d -= 6;
  }
  return d;
}

export function scoreFor(events: SimEvent[], combo: number): number {
  const m = Math.max(1, combo);
  let s = 0;
  for (const e of events) {
    if (e.type === "corePop") s += 12 * m;
    else if (e.type === "annihilate") s += 28 * m;
    else if (e.type === "invert" && e.contagion) s += 3 * m;
    else if (e.type === "detonate") s += 6 * m;
    else if (e.type === "snap") s += 4 * m;
  }
  return s;
}

export function comboBump(events: SimEvent[]): number {
  let n = 0;
  for (const e of events) {
    if (
      e.type === "corePop" ||
      e.type === "annihilate" ||
      (e.type === "invert" && e.contagion)
    ) {
      n += 1;
    }
  }
  return n;
}

export function livingCores(bodies: Body[]): number {
  let n = 0;
  for (const b of bodies) if (b.alive && b.kind === "core") n += 1;
  return n;
}

export function livingBricks(bodies: Body[]): number {
  let n = 0;
  for (const b of bodies) if (b.alive && b.kind === "brick") n += 1;
  return n;
}
