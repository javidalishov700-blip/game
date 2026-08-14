import { describe, expect, it } from "vitest";
import {
  applyGravity,
  columnHeight,
  dropBlock,
  emptyGrid,
  flood,
  popGroups,
  popScore,
  resolveShot,
  ROWS,
  seedTutorial,
  stackPeak,
  unlockedColors,
} from "../src/board";

describe("gravity", () => {
  it("compacts blocks to the floor", () => {
    const grid = emptyGrid();
    grid[0]![2] = 1;
    grid[0]![5] = 1;
    applyGravity(grid);
    expect(grid[0]![ROWS - 1]).toBe(1);
    expect(grid[0]![ROWS - 2]).toBe(1);
    expect(grid[0]![2]).toBeNull();
  });
});

describe("flood", () => {
  it("collects 4-connected matches", () => {
    const grid = emptyGrid();
    grid[0]![7] = 0;
    grid[1]![7] = 0;
    grid[1]![6] = 0;
    grid[2]![7] = 1;
    expect(flood(grid, 0, 7)).toHaveLength(3);
  });
});

describe("resolveShot", () => {
  it("pops a matching peak and does not place", () => {
    const grid = emptyGrid();
    grid[2]![7] = 0;
    grid[2]![6] = 0;
    const res = resolveShot(grid, 2, "color", 0);
    expect(res.placed).toBe(false);
    expect(res.popped.length).toBeGreaterThanOrEqual(2);
    expect(columnHeight(grid, 2)).toBe(0);
  });

  it("stacks a mismatch onto the peak", () => {
    const grid = emptyGrid();
    grid[1]![7] = 1;
    grid[1]![6] = 2;
    const res = resolveShot(grid, 1, "color", 0);
    expect(res.placed).toBe(true);
    expect(res.lose).toBe(false);
    expect(stackPeak(grid, 1)).toBe(5);
    expect(grid[1]![5]).toBe(0);
  });

  it("bomb clears a neighborhood", () => {
    const grid = emptyGrid();
    grid[1]![7] = 0;
    grid[2]![7] = 1;
    grid[3]![7] = 2;
    const res = resolveShot(grid, 2, "bomb", 0);
    expect(res.popped.length).toBeGreaterThanOrEqual(3);
  });
});

describe("dropBlock", () => {
  it("fills from the bottom", () => {
    const grid = emptyGrid();
    expect(dropBlock(grid, 0, 1)).toBe(true);
    expect(grid[0]![ROWS - 1]).toBe(1);
    expect(dropBlock(grid, 0, 2)).toBe(true);
    expect(grid[0]![ROWS - 2]).toBe(2);
  });
});

describe("popGroups", () => {
  it("clears clusters of min size", () => {
    const grid = emptyGrid();
    grid[0]![7] = 1;
    grid[1]![7] = 1;
    grid[2]![7] = 1;
    const popped = popGroups(grid, 3);
    expect(popped).toHaveLength(3);
  });
});

describe("helpers", () => {
  it("keeps early game at 2 colors", () => {
    expect(unlockedColors(0)).toBe(2);
    expect(unlockedColors(39)).toBe(2);
    expect(unlockedColors(40)).toBe(3);
  });

  it("scores bigger pops higher", () => {
    expect(popScore(5, 1, 0)).toBeGreaterThan(popScore(2, 1, 0));
  });

  it("tutorial board is match-ready", () => {
    const grid = seedTutorial();
    expect(flood(grid, 0, ROWS - 1).length).toBeGreaterThanOrEqual(3);
  });
});
