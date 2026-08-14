export type Verdict = "early" | "good" | "perfect" | "late";

export type Windows = {
  /** Progress [0,1] where surviving parries begin. */
  good: number;
  /** Progress [0,1] where perfect (combo) parries begin. */
  perfect: number;
};

/** Difficulty 0..1. Early game is wide and readable. */
export function windowsFor(difficulty: number): Windows {
  const d = Math.max(0, Math.min(1, difficulty));
  return {
    good: 0.38 + d * 0.22,
    perfect: 0.68 + d * 0.16,
  };
}

export function approachTime(difficulty: number): number {
  const d = Math.max(0, Math.min(1, difficulty));
  return 1.4 - d * 0.68;
}

export function gapTime(difficulty: number): number {
  const d = Math.max(0, Math.min(1, difficulty));
  return 0.42 - d * 0.18;
}

export function runDifficulty(cleared: number, score: number): number {
  return 1 - Math.exp(-(cleared * 0.045 + score * 0.0015));
}

/**
 * progress: 0 at spawn, 1 at impact.
 * No tap by impact = late (caller handles).
 */
export function judge(progress: number, w: Windows): Verdict {
  if (progress >= 1) return "late";
  if (progress >= w.perfect) return "perfect";
  if (progress >= w.good) return "good";
  return "early";
}

/** Consecutive perfects: 1 → 2x, 2 → 3x, ... capped at 8x. */
export function comboMult(perfectStreak: number): number {
  if (perfectStreak <= 0) return 1;
  return Math.min(1 + perfectStreak, 8);
}

export function hitScore(verdict: Verdict, perfectStreakAfter: number): number {
  const base = 10;
  if (verdict === "perfect") return base * comboMult(perfectStreakAfter);
  if (verdict === "good") return base;
  return 0;
}
