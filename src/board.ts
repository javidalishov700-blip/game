import { pick, rand } from "./math";

export const COLS = 5;
export const ROWS = 8;

/** 0..4 play colors. -1 rainbow, -2 bomb (shots only). */
export type Color = number;
export type Grid = (Color | null)[][];
export type CellPos = { c: number; r: number };

export const PALETTE = ["#ff5a7a", "#2fd39a", "#ffc43a", "#4d9fff", "#d07cff"];

export function emptyGrid(): Grid {
  return Array.from({ length: COLS }, () =>
    Array.from({ length: ROWS }, () => null),
  );
}

export function cloneGrid(grid: Grid): Grid {
  return grid.map((col) => col.slice());
}

export function unlockedColors(score: number): number {
  if (score < 40) return 2;
  if (score < 100) return 3;
  if (score < 200) return 4;
  return 5;
}

export function spawnInterval(score: number): number {
  if (score < 25) return 2.5;
  if (score < 60) return 2.15;
  if (score < 120) return 1.8;
  if (score < 200) return 1.45;
  return 1.15;
}

/** Smallest row index with a block (peak). ROWS if the column is empty. */
export function stackPeak(grid: Grid, col: number): number {
  for (let r = 0; r < ROWS; r++) {
    if (grid[col]![r] !== null) return r;
  }
  return ROWS;
}

export function columnHeight(grid: Grid, col: number): number {
  return ROWS - stackPeak(grid, col);
}

export function isOverflow(grid: Grid): boolean {
  for (let c = 0; c < COLS; c++) {
    if (grid[c]![0] !== null) return true;
  }
  return false;
}

export function flood(grid: Grid, col: number, row: number): CellPos[] {
  const color = grid[col]![row];
  if (color === null) return [];
  const seen = new Set<string>();
  const out: CellPos[] = [];
  const stack: CellPos[] = [{ c: col, r: row }];
  while (stack.length) {
    const p = stack.pop()!;
    const key = `${p.c},${p.r}`;
    if (seen.has(key)) continue;
    if (p.c < 0 || p.c >= COLS || p.r < 0 || p.r >= ROWS) continue;
    if (grid[p.c]![p.r] !== color) continue;
    seen.add(key);
    out.push(p);
    stack.push(
      { c: p.c + 1, r: p.r },
      { c: p.c - 1, r: p.r },
      { c: p.c, r: p.r + 1 },
      { c: p.c, r: p.r - 1 },
    );
  }
  return out;
}

export function applyGravity(grid: Grid): void {
  for (let c = 0; c < COLS; c++) {
    const blocks: Color[] = [];
    for (let r = ROWS - 1; r >= 0; r--) {
      const v = grid[c]![r];
      if (v !== null) blocks.push(v);
    }
    for (let r = 0; r < ROWS; r++) grid[c]![r] = null;
    for (let i = 0; i < blocks.length; i++) {
      grid[c]![ROWS - 1 - i] = blocks[i]!;
    }
  }
}

export function popGroups(grid: Grid, minSize: number): CellPos[] {
  const seen = new Set<string>();
  const popped: CellPos[] = [];
  for (let c = 0; c < COLS; c++) {
    for (let r = 0; r < ROWS; r++) {
      const key = `${c},${r}`;
      if (seen.has(key) || grid[c]![r] === null) continue;
      const group = flood(grid, c, r);
      for (const p of group) seen.add(`${p.c},${p.r}`);
      if (group.length >= minSize) {
        for (const p of group) {
          grid[p.c]![p.r] = null;
          popped.push(p);
        }
      }
    }
  }
  return popped;
}

export type ShotKind = "color" | "rainbow" | "bomb";

export type ShotResult = {
  popped: CellPos[];
  placed: boolean;
  lose: boolean;
  chains: number;
};

export function resolveShot(
  grid: Grid,
  col: number,
  kind: ShotKind,
  color: Color,
): ShotResult {
  const popped: CellPos[] = [];
  let placed = false;
  let lose = false;
  const peak = stackPeak(grid, col);

  if (kind === "bomb") {
    const impact = peak === ROWS ? ROWS - 1 : peak;
    for (let c = col - 1; c <= col + 1; c++) {
      for (let r = impact - 1; r <= impact + 2; r++) {
        if (c < 0 || c >= COLS || r < 0 || r >= ROWS) continue;
        if (grid[c]![r] !== null) {
          popped.push({ c, r });
          grid[c]![r] = null;
        }
      }
    }
  } else if (peak === ROWS) {
    const placedColor = kind === "rainbow" ? color : color;
    grid[col]![ROWS - 1] = placedColor;
    placed = true;
  } else {
    const hit = grid[col]![peak]!;
    const match = kind === "rainbow" || hit === color;
    if (match) {
      for (const p of flood(grid, col, peak)) {
        popped.push(p);
        grid[p.c]![p.r] = null;
      }
    } else {
      const row = peak - 1;
      if (row < 0) lose = true;
      else {
        grid[col]![row] = color;
        placed = true;
      }
    }
  }

  applyGravity(grid);
  let chains = 0;
  for (let i = 0; i < 6; i++) {
    const extra = popGroups(grid, 3);
    if (!extra.length) break;
    popped.push(...extra);
    applyGravity(grid);
    chains += 1;
  }

  return { popped, placed, lose: lose || isOverflow(grid), chains };
}

export function dropBlock(grid: Grid, col: number, color: Color): boolean {
  const peak = stackPeak(grid, col);
  const row = peak === ROWS ? ROWS - 1 : peak - 1;
  if (row < 0) {
    grid[col]![0] = color;
    return false;
  }
  grid[col]![row] = color;
  return true;
}

export function fairColor(grid: Grid, unlocked: number): Color {
  const tops: Color[] = [];
  for (let c = 0; c < COLS; c++) {
    const peak = stackPeak(grid, c);
    if (peak < ROWS) tops.push(grid[c]![peak]!);
  }
  if (tops.length && rand() < 0.62) return pick(tops);
  return Math.floor(rand(0, unlocked));
}

export function seedTutorial(): Grid {
  const grid = emptyGrid();
  const bottom = ROWS - 1;
  const pattern = [
    [0, 0, 1, 1, 0],
    [0, 0, 1, 1, 1],
  ];
  for (let i = 0; i < pattern.length; i++) {
    for (let c = 0; c < COLS; c++) {
      grid[c]![bottom - i] = pattern[i]![c]!;
    }
  }
  return grid;
}

export function popScore(n: number, combo: number, chains: number): number {
  if (n <= 0) return 0;
  return n * (n + 1) * Math.max(1, combo) + chains * 8;
}

export function duelWindow(score: number): { lo: number; hi: number } {
  const width = score < 80 ? 0.42 : score < 160 ? 0.3 : 0.22;
  const mid = 0.52;
  return { lo: mid - width / 2, hi: mid + width / 2 };
}
