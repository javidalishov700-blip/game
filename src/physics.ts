import {
  ANCHOR_R,
  BRICK_R,
  CORE_R,
  CORRECTION,
  FRICTION,
  G,
  LINEAR_DAMP,
  MAX_SPEED,
  RESTITUTION,
  SLEEP_SPEED,
  SLOP,
  WORLD_H,
  WORLD_W,
} from "./const";
import { clamp, len } from "./math";

export type BodyKind = "brick" | "core" | "anchor";

export type Body = {
  id: number;
  kind: BodyKind;
  x: number;
  y: number;
  vx: number;
  vy: number;
  r: number;
  mass: number;
  sign: 1 | -1;
  color: number;
  pinned: boolean;
  alive: boolean;
  incoming: boolean;
  hint: boolean;
  spin: number;
  omega: number;
  heat: number;
};

export type Weld = {
  a: number;
  b: number;
  rest: number;
  broken: boolean;
};

export type Contact = {
  a: number;
  b: number;
  energy: number;
  nx: number;
  ny: number;
  x: number;
  y: number;
};

export type Well = { x: number; y: number; w: number; h: number };

export const well: Well = { x: 18, y: 72, w: WORLD_W - 36, h: WORLD_H - 118 };

export const dangerY = well.y + 86;

let nextId = 1;

export function resetIds(n = 1): void {
  nextId = n;
}

export function nid(): number {
  return nextId++;
}

export function makeBody(
  kind: BodyKind,
  x: number,
  y: number,
  color: number,
  extra?: Partial<Body>,
): Body {
  const r = kind === "core" ? CORE_R : kind === "anchor" ? ANCHOR_R : BRICK_R;
  const density = kind === "anchor" ? 3.2 : kind === "core" ? 0.7 : 1;
  return {
    id: nid(),
    kind,
    x,
    y,
    vx: 0,
    vy: 0,
    r,
    mass: Math.max(0.35, density * (r * r) * 0.0048),
    sign: 1,
    color,
    pinned: kind === "anchor",
    alive: true,
    incoming: false,
    hint: false,
    spin: 0,
    omega: 0,
    heat: 0,
    ...extra,
  };
}

export function invMass(b: Body): number {
  return b.pinned || !b.alive ? 0 : 1 / b.mass;
}

export function speed(b: Body): number {
  return len(b.vx, b.vy);
}

export function byId(bodies: Body[]): Map<number, Body> {
  const m = new Map<number, Body>();
  for (const b of bodies) m.set(b.id, b);
  return m;
}

function clampVel(b: Body): void {
  const s = speed(b);
  if (s > MAX_SPEED) {
    const k = MAX_SPEED / s;
    b.vx *= k;
    b.vy *= k;
  }
}

export function integrate(bodies: Body[], dt: number): void {
  for (const b of bodies) {
    if (!b.alive) continue;
    b.heat = Math.max(0, b.heat - dt * 3.2);
    if (b.pinned) {
      b.vx = 0;
      b.vy = 0;
      b.omega = 0;
      continue;
    }
    b.vy += G * b.sign * dt;
    b.vx *= Math.pow(LINEAR_DAMP, dt * 60);
    b.vy *= Math.pow(LINEAR_DAMP, dt * 60);
    if (b.sign > 0 && speed(b) < SLEEP_SPEED) {
      b.vx *= 0.86;
      b.vy *= 0.86;
    }
    b.x += b.vx * dt;
    b.y += b.vy * dt;
    b.omega *= 0.992;
    b.spin += b.omega * dt;
    clampVel(b);
  }
}

export function collidePair(a: Body, b: Body): Contact | null {
  if (!a.alive || !b.alive || a.id === b.id) return null;
  const dx = b.x - a.x;
  const dy = b.y - a.y;
  const dist = Math.hypot(dx, dy);
  const min = a.r + b.r;
  if (dist >= min || dist < 1e-6) return null;

  const nx = dx / dist;
  const ny = dy / dist;
  const overlap = min - dist;
  const imA = invMass(a);
  const imB = invMass(b);
  const im = imA + imB;
  if (im > 0) {
    const corr = (Math.max(overlap - SLOP, 0) * CORRECTION) / im;
    a.x -= nx * corr * imA;
    a.y -= ny * corr * imA;
    b.x += nx * corr * imB;
    b.y += ny * corr * imB;
  }

  const rvx = b.vx - a.vx;
  const rvy = b.vy - a.vy;
  const velN = rvx * nx + rvy * ny;
  const cx = (a.x * b.r + b.x * a.r) / min;
  const cy = (a.y * b.r + b.y * a.r) / min;
  if (velN > 0) {
    return { a: a.id, b: b.id, energy: 0, nx, ny, x: cx, y: cy };
  }

  const e = a.kind === "core" || b.kind === "core" ? 0.55 : RESTITUTION;
  const j = im > 0 ? (-(1 + e) * velN) / im : 0;
  a.vx -= j * nx * imA;
  a.vy -= j * ny * imA;
  b.vx += j * nx * imB;
  b.vy += j * ny * imB;

  const tx = -ny;
  const ty = nx;
  const velT = rvx * tx + rvy * ty;
  const jt = im > 0 ? clamp(-velT / im, -Math.abs(j) * FRICTION, Math.abs(j) * FRICTION) : 0;
  a.vx -= jt * tx * imA;
  a.vy -= jt * ty * imA;
  b.vx += jt * tx * imB;
  b.vy += jt * ty * imB;

  const reduced = (a.mass * b.mass) / Math.max(0.001, a.mass + b.mass);
  const energy = Math.max(0, -velN) * reduced;
  const torque = (energy / 80) * Math.sign(velT || 1);
  if (!a.pinned) a.omega += torque / a.mass;
  if (!b.pinned) b.omega -= torque / b.mass;
  clampVel(a);
  clampVel(b);
  return { a: a.id, b: b.id, energy, nx, ny, x: cx, y: cy };
}

export function collideAll(bodies: Body[]): Contact[] {
  const hits: Contact[] = [];
  const n = bodies.length;
  for (let i = 0; i < n; i++) {
    const a = bodies[i]!;
    if (!a.alive) continue;
    for (let j = i + 1; j < n; j++) {
      const b = bodies[j]!;
      const hit = collidePair(a, b);
      if (hit && hit.energy > 4) hits.push(hit);
    }
  }
  return hits;
}

export function walls(bodies: Body[]): void {
  const left = well.x;
  const right = well.x + well.w;
  const floor = well.y + well.h;
  for (const b of bodies) {
    if (!b.alive || b.pinned) continue;
    if (b.x - b.r < left) {
      b.x = left + b.r;
      if (b.vx < 0) b.vx = -b.vx * 0.38;
    }
    if (b.x + b.r > right) {
      b.x = right - b.r;
      if (b.vx > 0) b.vx = -b.vx * 0.38;
    }
    if (b.sign > 0 && b.y + b.r > floor) {
      b.y = floor - b.r;
      if (b.vy > 0) b.vy = -b.vy * 0.22;
      b.vx *= 0.82;
      if (Math.abs(b.vy) < 18) b.vy = 0;
    }
  }
}

export function solveWelds(welds: Weld[], bodies: Body[]): Contact[] {
  const map = byId(bodies);
  const snaps: Contact[] = [];
  for (const w of welds) {
    if (w.broken) continue;
    const a = map.get(w.a);
    const b = map.get(w.b);
    if (!a?.alive || !b?.alive) {
      w.broken = true;
      continue;
    }
    if (a.sign !== b.sign) {
      w.broken = true;
      const dx = b.x - a.x;
      const dy = b.y - a.y;
      const d = Math.hypot(dx, dy) || 1;
      const nx = dx / d;
      const ny = dy / d;
      const kick = 240;
      if (!a.pinned) {
        a.vx -= nx * kick;
        a.vy -= ny * kick;
      }
      if (!b.pinned) {
        b.vx += nx * kick;
        b.vy += ny * kick;
      }
      snaps.push({
        a: a.id,
        b: b.id,
        energy: 200,
        nx,
        ny,
        x: (a.x + b.x) * 0.5,
        y: (a.y + b.y) * 0.5,
      });
      continue;
    }
    const dx = b.x - a.x;
    const dy = b.y - a.y;
    const d = Math.hypot(dx, dy) || 1;
    const nx = dx / d;
    const ny = dy / d;
    const diff = d - w.rest;
    const imA = invMass(a);
    const imB = invMass(b);
    const im = imA + imB;
    if (im <= 0) continue;
    const corr = (diff * 0.55) / im;
    a.x += nx * corr * imA;
    a.y += ny * corr * imA;
    b.x -= nx * corr * imB;
    b.y -= ny * corr * imB;
    const rel = (b.vx - a.vx) * nx + (b.vy - a.vy) * ny;
    const j = (rel * 0.4) / im;
    a.vx += j * nx * imA;
    a.vy += j * ny * imA;
    b.vx -= j * nx * imB;
    b.vy -= j * ny * imB;
  }
  return snaps;
}

export function hitTest(bodies: Body[], x: number, y: number, pad = 10): Body | null {
  let best: Body | null = null;
  let bestD = 1e9;
  for (const b of bodies) {
    if (!b.alive) continue;
    const d = Math.hypot(b.x - x, b.y - y);
    if (d <= b.r + pad && d < bestD) {
      best = b;
      bestD = d;
    }
  }
  return best;
}
