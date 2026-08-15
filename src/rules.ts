import { CORE_POP_ENERGY, KICK, MIN_SPLIT_R, SPLIT_ENERGY, SPLIT_SPREAD } from "./const";
import { Body, Contact, makeBody } from "./physics";

export type SimEvent =
  | { type: "kick"; id: number; x: number; y: number; color: number }
  | { type: "clack"; x: number; y: number; energy: number }
  | { type: "split"; id: number; x: number; y: number; color: number }
  | { type: "corePop"; id: number; x: number; y: number; color: number };

export type ImpactResult = { events: SimEvent[]; spawned: Body[] };

export function kickBody(body: Body, tapX: number, tapY: number): SimEvent | null {
  if (!body.alive) return null;
  let dx = body.x - tapX;
  let dy = body.y - tapY;
  let d = Math.hypot(dx, dy);
  if (d < 3) {
    dx = 0;
    dy = -1;
    d = 1;
  }
  const nx = dx / d;
  const ny = dy / d;
  body.vx += nx * KICK;
  body.vy += ny * KICK;
  body.heat = 1;
  body.omega += (tapX < body.x ? 1 : -1) * 6;
  return { type: "kick", id: body.id, x: body.x, y: body.y, color: body.color };
}

function bodyById(bodies: Body[], id: number): Body | undefined {
  return bodies.find((b) => b.id === id);
}

function splitGlass(body: Body, nx: number, ny: number): Body[] {
  body.alive = false;
  const r = Math.max(MIN_SPLIT_R * 0.92, body.r * 0.62);
  const px = -ny;
  const py = nx;
  const gap = r * 0.85;
  const left = makeBody("glass", body.x + px * gap, body.y + py * gap, body.color, { r });
  const right = makeBody("glass", body.x - px * gap, body.y - py * gap, body.color, { r });
  left.vx = body.vx + px * SPLIT_SPREAD;
  left.vy = body.vy + py * SPLIT_SPREAD;
  right.vx = body.vx - px * SPLIT_SPREAD;
  right.vy = body.vy - py * SPLIT_SPREAD;
  left.heat = 1;
  right.heat = 1;
  return [left, right];
}

export function resolveImpact(contact: Contact, bodies: Body[]): ImpactResult {
  const a = bodyById(bodies, contact.a);
  const b = bodyById(bodies, contact.b);
  if (!a || !b) return { events: [], spawned: [] };
  const events: SimEvent[] = [];
  const spawned: Body[] = [];

  if (contact.energy > 18) {
    events.push({ type: "clack", x: contact.x, y: contact.y, energy: contact.energy });
  }

  for (const dst of [a, b]) {
    if (!dst.alive) continue;
    if (dst.kind === "core" && contact.energy >= CORE_POP_ENERGY) {
      dst.alive = false;
      events.push({ type: "corePop", id: dst.id, x: dst.x, y: dst.y, color: dst.color });
    } else if (
      dst.kind === "glass" &&
      dst.r >= MIN_SPLIT_R &&
      contact.energy >= SPLIT_ENERGY
    ) {
      const kids = splitGlass(dst, contact.nx, contact.ny);
      spawned.push(...kids);
      events.push({ type: "split", id: dst.id, x: dst.x, y: dst.y, color: dst.color });
    }
  }

  return { events, spawned };
}

export function scoreFor(events: SimEvent[], combo: number): number {
  const m = Math.max(1, combo);
  let s = 0;
  for (const e of events) {
    if (e.type === "corePop") s += 14 * m;
    else if (e.type === "split") s += 5 * m;
  }
  return s;
}

export function comboBump(events: SimEvent[]): number {
  let n = 0;
  for (const e of events) {
    if (e.type === "corePop" || e.type === "split") n += 1;
  }
  return n;
}

export function livingCores(bodies: Body[]): number {
  let n = 0;
  for (const b of bodies) if (b.alive && b.kind === "core") n += 1;
  return n;
}

export function isJammed(bodies: Body[], limit: number): boolean {
  let n = 0;
  for (const b of bodies) {
    if (b.alive) n += 1;
    if (n >= limit) return true;
  }
  return false;
}
