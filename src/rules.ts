import { comboMult, hitScore, type Verdict } from "./judge";

export const LANES = 6;
export const LIVES = 3;

export type SpikeKind = "normal" | "gold" | "ghost";

export type Spawn = {
  lane: number;
  kind: SpikeKind;
  gap: number;
  /** Mid-flight lane swap. */
  feintTo?: number;
  /** Progress [0,1] when the swap finishes. */
  feintAt?: number;
};

export function laneFromAngle(ang: number): number {
  const tau = Math.PI * 2;
  const a = ((ang % tau) + tau) % tau;
  return Math.round(a / (tau / LANES)) % LANES;
}

export function laneFromPoint(
  x: number,
  y: number,
  cx: number,
  cy: number,
): number {
  return laneFromAngle(Math.atan2(y - cy, x - cx));
}

export function laneAngle(lane: number): number {
  return (lane * Math.PI * 2) / LANES;
}

export function liveLane(spawn: Pick<Spawn, "lane" | "feintTo" | "feintAt">, progress: number): number {
  if (spawn.feintTo != null && spawn.feintAt != null && progress >= spawn.feintAt) {
    return spawn.feintTo;
  }
  return spawn.lane;
}

export function shortestArc(from: number, to: number): number {
  let d = to - from;
  const tau = Math.PI * 2;
  while (d > Math.PI) d -= tau;
  while (d < -Math.PI) d += tau;
  return d;
}

export function waveTitle(wave: number): string {
  if (wave <= 1) return "WAIT";
  if (wave === 2) return "GOLD";
  if (wave === 3) return "GHOST";
  if (wave === 4) return "PAIRS";
  if (wave === 5) return "FEINT";
  return "STORM";
}

export function waveHint(wave: number): string {
  if (wave <= 1) return "TAP THE LANE WHEN GOLD";
  if (wave === 2) return "GOLD PAYS DOUBLE";
  if (wave === 3) return "LET GHOSTS PASS";
  if (wave === 4) return "TWO AT ONCE";
  if (wave === 5) return "IT SWITCHES LANES";
  return "DON'T BLINK";
}

export function buildWave(wave: number): Spawn[] {
  const n = Math.min(16, 5 + wave);
  const out: Spawn[] = [];
  let last = -1;
  for (let i = 0; i < n; i++) {
    let lane = Math.floor(Math.random() * LANES);
    if (lane === last) lane = (lane + 1 + Math.floor(Math.random() * 4)) % LANES;
    last = lane;
    let kind: SpikeKind = "normal";
    if (wave >= 2 && Math.random() < 0.2) kind = "gold";
    if (wave >= 3 && Math.random() < 0.18) kind = "ghost";
    const gap = Math.max(0.2, 0.56 - wave * 0.036);
    const spawn: Spawn = { lane, kind, gap };
    if (wave >= 5 && kind === "normal" && Math.random() < 0.32) {
      spawn.feintTo = (lane + (Math.random() < 0.5 ? 1 : LANES - 1)) % LANES;
      spawn.feintAt = 0.4 + Math.random() * 0.12;
    }
    out.push(spawn);
  }
  if (wave >= 4 && out.length >= 3) {
    const i = 1 + Math.floor(Math.random() * (out.length - 2));
    const prev = out[i - 1]!;
    const cur = out[i]!;
    cur.gap = 0.07;
    cur.lane = (prev.lane + 2) % LANES;
    cur.feintTo = undefined;
    cur.feintAt = undefined;
  }
  if (wave >= 7 && out.length >= 5) {
    const j = 2 + Math.floor(Math.random() * (out.length - 3));
    out[j]!.gap = 0.06;
  }
  return out;
}

export function award(
  verdict: Verdict,
  kind: SpikeKind,
  streakAfter: number,
  wave: number,
): number {
  const w = Math.max(1, wave);
  if (kind === "ghost" && verdict === "late") {
    return 15 * comboMult(Math.max(1, streakAfter)) * w;
  }
  const base = hitScore(verdict, streakAfter) * w;
  if (kind === "gold") return base * 2;
  return base;
}

export function waveBonus(wave: number): number {
  return 40 * wave;
}
