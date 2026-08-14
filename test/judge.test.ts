import { describe, expect, it } from "vitest";
import {
  comboMult,
  hitScore,
  judge,
  windowsFor,
} from "../src/judge";

describe("judge", () => {
  const w = { good: 0.5, perfect: 0.8 };

  it("marks flinches as early", () => {
    expect(judge(0.2, w)).toBe("early");
  });

  it("marks the wide window as good", () => {
    expect(judge(0.6, w)).toBe("good");
  });

  it("marks the last beat as perfect", () => {
    expect(judge(0.9, w)).toBe("perfect");
  });

  it("marks impact as late", () => {
    expect(judge(1, w)).toBe("late");
  });
});

describe("combo", () => {
  it("starts the multiplier at 2x on the first perfect", () => {
    expect(comboMult(1)).toBe(2);
    expect(comboMult(2)).toBe(3);
    expect(comboMult(3)).toBe(4);
  });

  it("caps at 8x", () => {
    expect(comboMult(99)).toBe(8);
  });

  it("pays 10 on a good hit and 10 * Nx on a perfect streak", () => {
    expect(hitScore("good", 0)).toBe(10);
    expect(hitScore("perfect", 1)).toBe(20);
    expect(hitScore("perfect", 3)).toBe(40);
    expect(hitScore("early", 4)).toBe(0);
  });
});

describe("windows", () => {
  it("keeps the first minutes more forgiving than the end", () => {
    const easy = windowsFor(0);
    const hard = windowsFor(1);
    expect(easy.good).toBeLessThan(hard.good);
    expect(easy.perfect).toBeLessThan(hard.perfect);
  });
});
