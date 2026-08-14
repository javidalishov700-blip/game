import { describe, expect, it } from "vitest";
import {
  LANES,
  LIVES,
  award,
  buildWave,
  laneAngle,
  laneFromAngle,
  liveLane,
  shortestArc,
  waveBonus,
  waveTitle,
} from "../src/rules";

describe("lanes", () => {
  it("maps a lane angle back to the same lane", () => {
    for (let i = 0; i < LANES; i++) {
      expect(laneFromAngle(laneAngle(i))).toBe(i);
    }
  });

  it("wraps past a full turn", () => {
    expect(laneFromAngle(laneAngle(0) + Math.PI * 2)).toBe(0);
  });

  it("uses the swapped lane after a feint", () => {
    expect(liveLane({ lane: 1, feintTo: 2, feintAt: 0.4 }, 0.2)).toBe(1);
    expect(liveLane({ lane: 1, feintTo: 2, feintAt: 0.4 }, 0.4)).toBe(2);
  });

  it("takes the short way around the ring", () => {
    expect(shortestArc(0, Math.PI * 2 - 0.2)).toBeCloseTo(-0.2, 5);
  });
});

describe("waves", () => {
  it("starts with three lives and named waves", () => {
    expect(LIVES).toBe(3);
    expect(waveTitle(1)).toBe("WAIT");
    expect(waveTitle(3)).toBe("GHOST");
    expect(waveTitle(5)).toBe("FEINT");
  });

  it("keeps wave 1 as single normal spikes", () => {
    const wave = buildWave(1);
    expect(wave.length).toBe(5);
    expect(wave.every((s) => s.kind === "normal" && s.feintTo == null)).toBe(true);
  });

  it("pays gold double and ghosts on a pass", () => {
    expect(award("perfect", "normal", 1, 1)).toBe(20);
    expect(award("perfect", "gold", 1, 1)).toBe(40);
    expect(award("late", "ghost", 1, 1)).toBe(30);
    expect(waveBonus(3)).toBe(120);
  });
});
