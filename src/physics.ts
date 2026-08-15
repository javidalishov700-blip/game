import {
  CORE_R,
  CORRECTION,
  FRICTION,
  GLASS_R,
  LINEAR_DAMP,
  MAX_SPEED,
  RESTITUTION,
  SLEEP_SPEED,
  SLOP,
  STONE_R,
  WALL_REST,
  WORLD_H,
  WORLD_W,
} from "./const";
import { clamp, len } from "./math";

export type BodyKind = "stone" | "glass" | "core";

export type Body = {
  id: number;
  kind: BodyKind;
  x: number;
  y: number;
  vx: number;
  vy: number;
  r: number;
  mass: number;
  color: number;
  alive: boolean;
  hint: boolean;
  spin: number;
  omega: number;
  heat: number;
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
  const r = extra?.r ?? (kind === "core" ? CORE_R : kind === "glass" ? GLASS_R : STONE_R);
  const density = kind === "stone" ? 1.8 : kind === "core" ? 0.65 : 0.9;
  return {
    id: nid(),
    kind,
    x,
    y,
    vx: 0,
    vy: 0,
    r,
    mass: Math.max(0.28, density * (r * r) * 0.0048),
    color,
    alive: true,
    hint: false,
    spin: 0,
    omega: 0,
    heat: 0,
    ...extra,
  };
}

export function invMass(b: Body): number {
  return !b.alive ? 0 : 1 / b.mass;
}

export function speed(b: Body): number {
  return len(b.vx, b.vy);
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
    b.heat = Math.max(0, b.heat - dt * 2.8);
    b.vx *= Math.pow(LINEAR_DAMP, dt * 60);
    b.vy *= Math.pow(LINEAR_DAMP, dt * 60);
    if (speed(b) < SLEEP_SPEED) {
      b.vx *= 0.84;
      b.vy *= 0.84;
    }
    b.x += b.vx * dt;
    b.y += b.vy * dt;
    b.omega *= 0.99;
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

  const e = RESTITUTION;
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
  const torque = (energy / 70) * Math.sign(velT || 1);
  a.omega += torque / a.mass;
  b.omega -= torque / b.mass;
  a.heat = Math.max(a.heat, Math.min(1, energy / 180));
  b.heat = Math.max(b.heat, Math.min(1, energy / 180));
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
  const top = well.y;
  const floor = well.y + well.h;
  for (const b of bodies) {
    if (!b.alive) continue;
    if (b.x - b.r < left) {
      b.x = left + b.r;
      if (b.vx < 0) b.vx = Math.abs(b.vx) * WALL_REST;
    }
    if (b.x + b.r > right) {
      b.x = right - b.r;
      if (b.vx > 0) b.vx = -Math.abs(b.vx) * WALL_REST;
    }
    if (b.y - b.r < top) {
      b.y = top + b.r;
      if (b.vy < 0) b.vy = Math.abs(b.vy) * WALL_REST;
    }
    if (b.y + b.r > floor) {
      b.y = floor - b.r;
      if (b.vy > 0) b.vy = -Math.abs(b.vy) * WALL_REST;
    }
  }
}

export function hitTest(bodies: Body[], x: number, y: number, pad = 12): Body | null {
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

export function anyLive(bodies: Body[], minSpeed = SLEEP_SPEED): boolean {
  for (const b of bodies) {
    if (b.alive && speed(b) >= minSpeed) return true;
  }
  return false;
}

export function livingCount(bodies: Body[]): number {
  let n = 0;
  for (const b of bodies) if (b.alive) n += 1;
  return n;
}
