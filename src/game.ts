import type { AudioBus } from "./audio";
import {
  COLS,
  Color,
  applyGravity,
  dropBlock,
  duelWindow,
  fairColor,
  Grid,
  PALETTE,
  popScore,
  resolveShot,
  ROWS,
  seedTutorial,
  ShotKind,
  spawnInterval,
  stackPeak,
  unlockedColors,
} from "./board";
import { clamp, lerp, rand } from "./math";

type Mode = "title" | "play" | "duel" | "death";

type Particle = {
  x: number;
  y: number;
  vx: number;
  vy: number;
  life: number;
  max: number;
  size: number;
  r: number;
  g: number;
  b: number;
};

type Floater = { x: number; y: number; text: string; life: number; color: string };
type Pest = { x: number; y: number; vx: number; phase: number; r: number };
type ShotFx = { col: number; kind: ShotKind; color: Color; t: number };
type DropFx = { col: number; color: Color; t: number };

const STORAGE_KEY = "popdraw-best";
const MAX_PARTICLES = 180;

export class PopDrawGame {
  readonly canvas: HTMLCanvasElement;
  private readonly ctx: CanvasRenderingContext2D;
  private readonly audio: AudioBus;

  private w = 390;
  private h = 844;
  private lastT = 0;
  private time = 0;
  private mode: Mode = "title";

  private grid: Grid = seedTutorial();
  private ammo = 0;
  private next = 1;
  private special: ShotKind = "color";
  private score = 0;
  private best = 0;
  private combo = 0;
  private comboT = 0;
  private spawnT = 2;
  private pestT = 4;
  private duelT = 18;
  private pests: Pest[] = [];
  private shot: ShotFx | null = null;
  private drops: DropFx[] = [];
  private hoverCol = 2;

  private duelNeedle = 0;
  private duelDir = 1;
  private duelLo = 0.3;
  private duelHi = 0.7;

  private particles: Particle[] = [];
  private particleAt = 0;
  private floaters: Floater[] = [];
  private shake = 0;
  private flash = 0;
  private punch = 1;
  private hint = "TAP A COLUMN TO SHOOT";
  private hintT = 3;

  private cell = 48;
  private gx = 0;
  private gy = 0;
  private skyY = 0;
  private skyH = 80;
  private trayY = 0;

  constructor(canvas: HTMLCanvasElement, audio: AudioBus) {
    this.canvas = canvas;
    const ctx = canvas.getContext("2d", { alpha: false });
    if (!ctx) throw new Error("Canvas 2D is unavailable");
    this.ctx = ctx;
    this.audio = audio;
    this.best = readBest();
  }

  resize(cssW: number, cssH: number, dpr: number): void {
    this.w = cssW;
    this.h = cssH;
    this.canvas.width = Math.max(1, Math.floor(cssW * dpr));
    this.canvas.height = Math.max(1, Math.floor(cssH * dpr));
    this.ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    this.layout();
  }

  skipClock(): void {
    this.lastT = 0;
  }

  hover(x: number, y: number): void {
    const col = this.colAt(x, y);
    if (col !== null) this.hoverCol = col;
  }

  tap(x: number, y: number): void {
    this.audio.unlock();
    if (this.hitMute(x, y)) {
      this.audio.toggleMute();
      return;
    }
    if (this.mode === "title") {
      this.startRun();
      return;
    }
    if (this.mode === "death") {
      this.startRun();
      return;
    }
    if (this.mode === "duel") {
      this.resolveDuel();
      return;
    }
    if (this.hitSwap(x, y)) {
      this.swap();
      return;
    }
    if (this.hitPest(x, y)) return;
    const col = this.colAt(x, y);
    if (col !== null) this.fire(col);
  }

  swapAmmo(): void {
    if (this.mode !== "play") return;
    this.swap();
  }

  tick(nowMs: number): void {
    if (!this.lastT) this.lastT = nowMs;
    let dt = (nowMs - this.lastT) / 1000;
    this.lastT = nowMs;
    if (dt > 0.05) dt = 0.05;

    this.time += dt;
    this.shake = Math.max(0, this.shake - dt * 8);
    this.flash = Math.max(0, this.flash - dt * 3);
    this.punch = lerp(this.punch, 1, 1 - Math.pow(0.001, dt));
    this.hintT = Math.max(0, this.hintT - dt);
    this.comboT = Math.max(0, this.comboT - dt);
    if (this.comboT <= 0) this.combo = 0;

    this.updateShot(dt);
    this.updateDrops(dt);
    this.updatePests(dt);
    this.updateParticles(dt);
    this.updateFloaters(dt);

    if (this.mode === "play") {
      this.spawnT -= dt;
      this.pestT -= dt;
      this.duelT -= dt;
      if (this.spawnT <= 0 && !this.shot) {
        this.garbageDrop();
        this.spawnT = spawnInterval(this.score);
      }
      if (this.pestT <= 0 && this.score >= 10) {
        this.spawnPest();
        this.pestT = lerp(5.5, 3.2, clamp(this.score / 180, 0, 1));
      }
      if (this.duelT <= 0 && this.score >= 35) this.beginDuel();
    }

    if (this.mode === "duel") {
      this.duelNeedle += this.duelDir * dt * (this.score < 80 ? 0.85 : 1.15);
      if (this.duelNeedle > 1) {
        this.duelNeedle = 1;
        this.duelDir = -1;
      }
      if (this.duelNeedle < 0) {
        this.duelNeedle = 0;
        this.duelDir = 1;
      }
    }

    this.draw();
  }

  private layout(): void {
    const top = Math.max(56, this.h * 0.08);
    this.skyH = Math.max(64, this.h * 0.13);
    const tray = Math.max(96, this.h * 0.2);
    const availH = this.h - top - this.skyH - tray;
    const availW = this.w - 24;
    this.cell = Math.min(availW / COLS, availH / ROWS);
    const gridW = this.cell * COLS;
    const gridH = this.cell * ROWS;
    this.gx = (this.w - gridW) / 2;
    this.gy = top + this.skyH;
    this.skyY = top;
    this.trayY = this.gy + gridH + 8;
  }

  private startRun(): void {
    this.mode = "play";
    this.grid = seedTutorial();
    this.ammo = 0;
    this.next = 1;
    this.special = "color";
    this.score = 0;
    this.combo = 0;
    this.comboT = 0;
    this.time = 0;
    this.spawnT = 2.4;
    this.pestT = 6;
    this.duelT = 22;
    this.pests = [];
    this.shot = null;
    this.drops = [];
    this.hint = "TAP A COLUMN TO SHOOT";
    this.hintT = 3.2;
    this.flash = 0.25;
    this.punch = 1.04;
    this.buzz(10);
  }

  private die(): void {
    if (this.mode === "death") return;
    this.mode = "death";
    this.audio.lose();
    this.shake = 1;
    this.flash = 0.6;
    this.burst(this.w * 0.5, this.gy + 20, 50, 255, 90, 90, 280);
    if (this.score > this.best) {
      this.best = this.score;
      writeBest(this.best);
    }
    this.buzz(40);
  }

  private fire(col: number): void {
    if (this.shot || this.mode !== "play") return;
    this.hoverCol = col;
    this.shot = { col, kind: this.special, color: this.ammo, t: 0 };
    this.audio.shot();
    this.special = "color";
  }

  private updateShot(dt: number): void {
    if (!this.shot) return;
    this.shot.t += dt / 0.12;
    if (this.shot.t < 1) return;
    const { col, kind, color } = this.shot;
    this.shot = null;
    const res = resolveShot(this.grid, col, kind, color);
    if (res.popped.length) {
      this.combo = this.comboT > 0 ? this.combo + 1 : 1;
      this.comboT = 2.4;
      const gain = popScore(res.popped.length, this.combo, res.chains);
      this.score += gain;
      this.audio.pop(res.popped.length);
      if (res.chains) this.audio.chain();
      this.punch = 1.05 + Math.min(0.06, res.popped.length * 0.008);
      this.shake = Math.min(1, 0.16 + res.popped.length * 0.04);
      this.flash = 0.18;
      for (const p of res.popped) {
        const { x, y } = this.cellCenter(p.c, p.r);
        const rgb = rgbOf(PALETTE[(p.c + p.r) % PALETTE.length]!);
        this.burst(x, y, 10, rgb[0], rgb[1], rgb[2], 180);
      }
      const word =
        res.popped.length >= 8
          ? "UNREAL"
          : res.popped.length >= 5
            ? "CRAZY"
            : this.combo > 1
              ? `x${this.combo}`
              : "POP";
      this.addFloater(this.gx + this.cell * (col + 0.5), this.gy + 24, `+${gain}  ${word}`, "#5b3a14");
      this.buzz(12);
      if (this.score >= 8 && this.hintT > 0) {
        this.hint = "MATCH COLORS · SWAP THE NEXT BALL";
        this.hintT = 2.8;
      }
    } else if (res.placed) {
      this.combo = 0;
    }
    this.cycleAmmo();
    if (res.lose) this.die();
  }

  private cycleAmmo(): void {
    const n = unlockedColors(this.score);
    this.ammo = this.next;
    this.next = fairColor(this.grid, n);
  }

  private swap(): void {
    if (this.special !== "color") return;
    const a = this.ammo;
    this.ammo = this.next;
    this.next = a;
    this.audio.swap();
  }

  private garbageDrop(): void {
    const n = unlockedColors(this.score);
    const count = this.score > 90 && Math.random() < 0.35 ? 2 : 1;
    for (let i = 0; i < count; i++) {
      const col = Math.floor(rand(0, COLS));
      const color = fairColor(this.grid, n);
      this.drops.push({ col, color, t: 0 });
    }
  }

  private updateDrops(dt: number): void {
    for (let i = this.drops.length - 1; i >= 0; i--) {
      const d = this.drops[i]!;
      d.t += dt / 0.28;
      if (d.t < 1) continue;
      this.drops.splice(i, 1);
      const ok = dropBlock(this.grid, d.col, d.color);
      this.audio.drop();
      if (!ok) this.die();
    }
  }

  private spawnPest(): void {
    const fromLeft = Math.random() < 0.5;
    this.pests.push({
      x: fromLeft ? -20 : this.w + 20,
      y: this.skyY + this.skyH * rand(0.3, 0.75),
      vx: (fromLeft ? 1 : -1) * lerp(70, 130, clamp(this.score / 200, 0, 1)),
      phase: rand(0, 6),
      r: 18,
    });
  }

  private updatePests(dt: number): void {
    for (let i = this.pests.length - 1; i >= 0; i--) {
      const p = this.pests[i]!;
      p.x += p.vx * dt;
      p.phase += dt * 8;
      p.y += Math.sin(p.phase) * 12 * dt;
      if (p.x < -40 || p.x > this.w + 40) this.pests.splice(i, 1);
    }
  }

  private hitPest(x: number, y: number): boolean {
    for (let i = this.pests.length - 1; i >= 0; i--) {
      const p = this.pests[i]!;
      if (Math.hypot(p.x - x, p.y - y) < p.r + 14) {
        this.pests.splice(i, 1);
        this.score += 6;
        this.audio.pest();
        this.burst(p.x, p.y, 16, 255, 210, 80, 160);
        this.addFloater(p.x, p.y, "+6  PIP", "#8a5a00");
        this.special = Math.random() < 0.5 ? "rainbow" : "bomb";
        this.hint = this.special === "bomb" ? "BOMB SHOT READY" : "RAINBOW SHOT READY";
        this.hintT = 2;
        this.buzz(10);
        return true;
      }
    }
    return false;
  }

  private beginDuel(): void {
    this.mode = "duel";
    this.duelNeedle = 0;
    this.duelDir = 1;
    const w = duelWindow(this.score);
    this.duelLo = w.lo;
    this.duelHi = w.hi;
    this.duelT = lerp(20, 13, clamp(this.score / 220, 0, 1));
    this.audio.duelStart();
    this.hint = "TAP IN THE GREEN — DRAW!";
    this.hintT = 2;
  }

  private resolveDuel(): void {
    const hit = this.duelNeedle >= this.duelLo && this.duelNeedle <= this.duelHi;
    this.mode = "play";
    if (hit) {
      this.audio.duelWin();
      this.score += 20;
      this.punch = 1.08;
      this.shake = 0.5;
      this.flash = 0.3;
      this.addFloater(this.w * 0.5, this.skyY + 28, "+20  QUICK DRAW", "#5b3a14");
      for (let c = 0; c < COLS; c++) {
        const peak = stackPeak(this.grid, c);
        if (peak < ROWS) this.grid[c]![peak] = null;
      }
      applyGravity(this.grid);
      this.burst(this.w * 0.5, this.gy, 28, 255, 200, 60, 220);
      this.buzz(18);
    } else {
      this.audio.duelLose();
      this.shake = 0.35;
      const n = unlockedColors(this.score);
      dropBlock(this.grid, Math.floor(rand(0, COLS)), fairColor(this.grid, n));
      dropBlock(this.grid, Math.floor(rand(0, COLS)), fairColor(this.grid, n));
      this.addFloater(this.w * 0.5, this.skyY + 28, "TOO SLOW", "#8a2038");
    }
  }

  private colAt(x: number, y: number): number | null {
    if (x < this.gx || x > this.gx + this.cell * COLS) return null;
    if (y < this.skyY) return null;
    const col = Math.floor((x - this.gx) / this.cell);
    if (col < 0 || col >= COLS) return null;
    return col;
  }

  private cellCenter(c: number, r: number): { x: number; y: number } {
    return {
      x: this.gx + (c + 0.5) * this.cell,
      y: this.gy + (r + 0.5) * this.cell,
    };
  }

  private hitSwap(x: number, y: number): boolean {
    const nx = this.w * 0.5 + 48;
    const ny = this.trayY + 36;
    return Math.hypot(x - nx, y - ny) < 28;
  }

  private hitMute(x: number, y: number): boolean {
    return x > this.w - 52 && y < 52;
  }

  private updateParticles(dt: number): void {
    for (const p of this.particles) {
      if (p.life <= 0) continue;
      p.life -= dt;
      p.vx *= 0.98;
      p.vy += 420 * dt;
      p.x += p.vx * dt;
      p.y += p.vy * dt;
    }
  }

  private updateFloaters(dt: number): void {
    for (let i = this.floaters.length - 1; i >= 0; i--) {
      const f = this.floaters[i]!;
      f.life -= dt;
      f.y -= 26 * dt;
      if (f.life <= 0) this.floaters.splice(i, 1);
    }
  }

  private burst(
    x: number,
    y: number,
    n: number,
    r: number,
    g: number,
    b: number,
    speed: number,
  ): void {
    for (let i = 0; i < n; i++) {
      const a = rand(0, Math.PI * 2);
      const s = rand(0.25, 1) * speed;
      const next: Particle = {
        x,
        y,
        vx: Math.cos(a) * s,
        vy: Math.sin(a) * s - 40,
        life: rand(0.25, 0.6),
        max: 0.6,
        size: rand(3, 7),
        r,
        g,
        b,
      };
      next.max = next.life;
      if (this.particles.length < MAX_PARTICLES) this.particles.push(next);
      else this.particles[this.particleAt] = next;
      this.particleAt = (this.particleAt + 1) % MAX_PARTICLES;
    }
  }

  private addFloater(x: number, y: number, text: string, color: string): void {
    this.floaters.push({ x, y, text, life: 0.8, color });
    if (this.floaters.length > 10) this.floaters.shift();
  }

  private buzz(ms: number): void {
    try {
      navigator.vibrate?.(ms);
    } catch {
      /* ignore */
    }
  }

  private draw(): void {
    const { ctx, w, h } = this;
    ctx.save();
    if (this.shake) {
      ctx.translate((Math.random() - 0.5) * 12 * this.shake, (Math.random() - 0.5) * 12 * this.shake);
    }
    ctx.translate(w * 0.5, h * 0.5);
    ctx.scale(this.punch, this.punch);
    ctx.translate(-w * 0.5, -h * 0.5);

    this.drawBackdrop();
    this.drawSky();
    this.drawGrid();
    this.drawShot();
    this.drawDrops();
    this.drawTray();
    this.drawPests();
    this.drawParticles();
    this.drawFloaters();
    this.drawHud();
    if (this.mode === "duel") this.drawDuel();
    if (this.mode === "title") this.drawTitle();
    if (this.mode === "death") this.drawDeath();
    if (this.flash > 0) {
      ctx.fillStyle = `rgba(255,255,255,${this.flash * 0.45})`;
      ctx.fillRect(0, 0, w, h);
    }
    ctx.restore();
  }

  private drawBackdrop(): void {
    const { ctx, w, h } = this;
    const g = ctx.createLinearGradient(0, 0, 0, h);
    g.addColorStop(0, "#f7e9d2");
    g.addColorStop(0.45, "#f3d5e4");
    g.addColorStop(1, "#c9d8ff");
    ctx.fillStyle = g;
    ctx.fillRect(0, 0, w, h);
  }

  private drawSky(): void {
    const { ctx } = this;
    ctx.fillStyle = "rgba(255,255,255,0.28)";
    roundRect(ctx, this.gx, this.skyY, this.cell * COLS, this.skyH - 8, 16);
    ctx.fill();
    ctx.fillStyle = "rgba(90, 50, 30, 0.45)";
    ctx.font = "700 12px system-ui, sans-serif";
    ctx.textAlign = "center";
    ctx.fillText("TAP THE PIPS  ·  DRAW WHEN THEY APPEAR", this.w * 0.5, this.skyY + 16);
  }

  private drawGrid(): void {
    const { ctx } = this;
    ctx.fillStyle = "rgba(255,255,255,0.7)";
    roundRect(ctx, this.gx - 8, this.gy - 8, this.cell * COLS + 16, this.cell * ROWS + 16, 18);
    ctx.fill();

    for (let c = 0; c < COLS; c++) {
      if (c === this.hoverCol && this.mode === "play") {
        ctx.fillStyle = "rgba(255, 180, 70, 0.16)";
        ctx.fillRect(this.gx + c * this.cell, this.gy, this.cell, this.cell * ROWS);
      }
      for (let r = 0; r < ROWS; r++) {
        const x = this.gx + c * this.cell;
        const y = this.gy + r * this.cell;
        ctx.strokeStyle = "rgba(80, 40, 20, 0.06)";
        ctx.strokeRect(x + 0.5, y + 0.5, this.cell, this.cell);
        const color = this.grid[c]![r];
        if (color === null) continue;
        this.drawBlock(x, y, this.cell, color, 1);
      }
    }

    ctx.fillStyle = "rgba(255, 70, 90, 0.55)";
    ctx.fillRect(this.gx, this.gy + 3, this.cell * COLS, 3);
    ctx.font = "800 10px system-ui, sans-serif";
    ctx.fillStyle = "rgba(180, 40, 60, 0.7)";
    ctx.textAlign = "left";
    ctx.fillText("CEILING", this.gx + 6, this.gy - 10);
  }

  private drawBlock(x: number, y: number, cell: number, color: Color, scale: number): void {
    const { ctx } = this;
    const m = cell * 0.08;
    const s = (cell - m * 2) * scale;
    const bx = x + (cell - s) / 2;
    const by = y + (cell - s) / 2;
    const hex = PALETTE[color] ?? PALETTE[0]!;
    ctx.save();
    ctx.fillStyle = hex;
    ctx.shadowColor = "rgba(80, 40, 20, 0.18)";
    ctx.shadowBlur = 8;
    ctx.shadowOffsetY = 3;
    roundRect(ctx, bx, by, s, s, s * 0.28);
    ctx.fill();
    ctx.shadowBlur = 0;
    ctx.fillStyle = "rgba(255,255,255,0.38)";
    roundRect(ctx, bx + s * 0.12, by + s * 0.1, s * 0.5, s * 0.28, s * 0.2);
    ctx.fill();
    ctx.restore();
  }

  private drawShot(): void {
    if (!this.shot) return;
    const peak = stackPeak(this.grid, this.shot.col);
    const targetR = peak === ROWS ? ROWS - 1 : peak;
    const startY = this.trayY;
    const endY = this.gy + (targetR + 0.5) * this.cell;
    const y = lerp(startY, endY, clamp(this.shot.t, 0, 1));
    const x = this.gx + (this.shot.col + 0.5) * this.cell;
    if (this.shot.kind === "bomb") this.drawSpecialOrb(x, y, "#3a3a3a", "B");
    else if (this.shot.kind === "rainbow") this.drawSpecialOrb(x, y, "#fff", "R");
    else this.drawBlock(x - this.cell / 2, y - this.cell / 2, this.cell * 0.85, this.shot.color, 1);
  }

  private drawDrops(): void {
    for (const d of this.drops) {
      const x = this.gx + (d.col + 0.5) * this.cell;
      const y = lerp(this.skyY, this.gy + this.cell * 0.5, clamp(d.t, 0, 1));
      this.drawBlock(x - this.cell / 2, y - this.cell / 2, this.cell, d.color, 0.9);
    }
  }

  private drawTray(): void {
    const { ctx, w } = this;
    const y = this.trayY + 8;
    ctx.fillStyle = "rgba(255,255,255,0.55)";
    roundRect(ctx, this.gx - 8, y, this.cell * COLS + 16, 78, 20);
    ctx.fill();

    ctx.textAlign = "center";
    ctx.fillStyle = "rgba(90,50,30,0.5)";
    ctx.font = "700 11px system-ui, sans-serif";
    ctx.fillText("NOW", w * 0.5 - 48, y + 16);
    ctx.fillText("NEXT  TAP TO SWAP", w * 0.5 + 48, y + 16);

    this.drawBlock(w * 0.5 - 48 - this.cell * 0.42, y + 22, this.cell * 0.84, this.ammo, 1);
    this.drawBlock(w * 0.5 + 48 - this.cell * 0.32, y + 28, this.cell * 0.64, this.next, 1);

    if (this.special === "bomb") {
      ctx.fillStyle = "#3a3a3a";
      ctx.font = "700 12px system-ui, sans-serif";
      ctx.fillText("BOMB", w * 0.5 - 48, y + 74);
    } else if (this.special === "rainbow") {
      ctx.fillStyle = "#7a40c8";
      ctx.font = "700 12px system-ui, sans-serif";
      ctx.fillText("RAINBOW", w * 0.5 - 48, y + 74);
    }
  }

  private drawSpecialOrb(x: number, y: number, fill: string, glyph: string): void {
    const { ctx } = this;
    ctx.fillStyle = fill;
    ctx.beginPath();
    ctx.arc(x, y, this.cell * 0.28, 0, Math.PI * 2);
    ctx.fill();
    ctx.fillStyle = fill === "#fff" ? "#7a40c8" : "#fff";
    ctx.font = "700 16px system-ui, sans-serif";
    ctx.textAlign = "center";
    ctx.textBaseline = "middle";
    ctx.fillText(glyph, x, y + 1);
    ctx.textBaseline = "alphabetic";
  }

  private drawPests(): void {
    const { ctx } = this;
    for (const p of this.pests) {
      ctx.save();
      ctx.translate(p.x, p.y);
      ctx.fillStyle = "#6b4b2a";
      ctx.beginPath();
      ctx.ellipse(0, 0, p.r, p.r * 0.72, 0, 0, Math.PI * 2);
      ctx.fill();
      ctx.fillStyle = "#ffd56a";
      ctx.beginPath();
      ctx.ellipse(-4, -2, 5, 4, 0, 0, Math.PI * 2);
      ctx.ellipse(6, -2, 5, 4, 0, 0, Math.PI * 2);
      ctx.fill();
      ctx.fillStyle = "#1a120c";
      ctx.beginPath();
      ctx.arc(-3, -2, 1.5, 0, Math.PI * 2);
      ctx.arc(7, -2, 1.5, 0, Math.PI * 2);
      ctx.fill();
      ctx.fillStyle = "#ff8a3a";
      ctx.beginPath();
      ctx.moveTo(2, 2);
      ctx.lineTo(12, 5);
      ctx.lineTo(2, 7);
      ctx.closePath();
      ctx.fill();
      ctx.restore();
    }
  }

  private drawParticles(): void {
    const { ctx } = this;
    for (const p of this.particles) {
      if (p.life <= 0) continue;
      const u = p.life / p.max;
      ctx.fillStyle = `rgba(${p.r|0},${p.g|0},${p.b|0},${u})`;
      ctx.beginPath();
      ctx.arc(p.x, p.y, p.size * u, 0, Math.PI * 2);
      ctx.fill();
    }
  }

  private drawFloaters(): void {
    const { ctx } = this;
    ctx.textAlign = "center";
    ctx.font = "800 16px system-ui, sans-serif";
    for (const f of this.floaters) {
      ctx.globalAlpha = clamp(f.life * 1.6, 0, 1);
      ctx.fillStyle = f.color;
      ctx.fillText(f.text, f.x, f.y);
    }
    ctx.globalAlpha = 1;
  }

  private drawHud(): void {
    const { ctx, w } = this;
    ctx.fillStyle = "rgba(70, 35, 20, 0.72)";
    ctx.font = "700 13px system-ui, sans-serif";
    ctx.textAlign = "left";
    ctx.fillText("BEST  " + this.best, 18, 28);
    ctx.textAlign = "right";
    ctx.fillText(this.audio.isMuted ? "MUTE" : "SOUND", w - 18, 28);
    ctx.textAlign = "center";
    ctx.font = "800 34px system-ui, sans-serif";
    ctx.fillStyle = "#4a2712";
    ctx.fillText(this.mode === "play" || this.mode === "duel" ? String(this.score) : "POPDRAW", w * 0.5, 36);
    if (this.hintT > 0 && this.mode === "play") {
      ctx.globalAlpha = clamp(this.hintT, 0, 1);
      ctx.font = "700 14px system-ui, sans-serif";
      ctx.fillStyle = "#6a3a18";
      ctx.fillText(this.hint, w * 0.5, this.h - 18);
      ctx.globalAlpha = 1;
    }
  }

  private drawTitle(): void {
    const { ctx, w, h } = this;
    ctx.textAlign = "center";
    ctx.font = "600 14px system-ui, sans-serif";
    ctx.fillStyle = "#6a3a18";
    ctx.fillText("Match  ·  Stack  ·  Snipe  ·  Draw", w * 0.5, h - 44);
    ctx.font = "800 18px system-ui, sans-serif";
    ctx.fillStyle = "#c44b1a";
    ctx.globalAlpha = 0.55 + 0.45 * Math.sin(this.time * 4);
    ctx.fillText("TAP TO PLAY", w * 0.5, h - 20);
    ctx.globalAlpha = 1;
  }

  private drawDeath(): void {
    const { ctx, w, h } = this;
    ctx.fillStyle = "rgba(40, 16, 20, 0.28)";
    ctx.fillRect(0, 0, w, h);
    ctx.fillStyle = "#4a2712";
    ctx.textAlign = "center";
    ctx.font = "800 28px system-ui, sans-serif";
    ctx.fillText("STACKED OUT", w * 0.5, h * 0.22);
    ctx.font = "800 56px system-ui, sans-serif";
    ctx.fillText(String(this.score), w * 0.5, h * 0.22 + 64);
    ctx.font = "700 16px system-ui, sans-serif";
    ctx.fillStyle = this.score >= this.best && this.score > 0 ? "#c44b1a" : "#6a3a18";
    ctx.fillText(
      this.score >= this.best && this.score > 0 ? "NEW BEST" : "BEST  " + this.best,
      w * 0.5,
      h * 0.22 + 92,
    );
    ctx.fillStyle = "#c44b1a";
    ctx.fillText("TAP TO RETRY", w * 0.5, h * 0.88);
  }

  private drawDuel(): void {
    const { ctx, w, h } = this;
    ctx.fillStyle = "rgba(40, 12, 20, 0.35)";
    ctx.fillRect(0, 0, w, h);
    ctx.fillStyle = "#fff6e8";
    ctx.textAlign = "center";
    ctx.font = "800 42px system-ui, sans-serif";
    ctx.fillText("DRAW!", w * 0.5, h * 0.28);
    ctx.font = "700 14px system-ui, sans-serif";
    ctx.fillText("Tap when the needle is in the green", w * 0.5, h * 0.28 + 28);

    const barW = Math.min(280, w - 64);
    const barX = (w - barW) / 2;
    const barY = h * 0.42;
    ctx.fillStyle = "#2a1820";
    roundRect(ctx, barX, barY, barW, 28, 14);
    ctx.fill();
    ctx.fillStyle = "#3ddc84";
    const gx = barX + this.duelLo * barW;
    const gw = (this.duelHi - this.duelLo) * barW;
    roundRect(ctx, gx, barY, gw, 28, 14);
    ctx.fill();
    const nx = barX + this.duelNeedle * barW;
    ctx.fillStyle = "#fff";
    ctx.fillRect(nx - 3, barY - 6, 6, 40);
  }
}

function roundRect(
  ctx: CanvasRenderingContext2D,
  x: number,
  y: number,
  w: number,
  h: number,
  r: number,
): void {
  const rr = Math.min(r, w / 2, h / 2);
  ctx.beginPath();
  ctx.moveTo(x + rr, y);
  ctx.arcTo(x + w, y, x + w, y + h, rr);
  ctx.arcTo(x + w, y + h, x, y + h, rr);
  ctx.arcTo(x, y + h, x, y, rr);
  ctx.arcTo(x, y, x + w, y, rr);
  ctx.closePath();
}

function rgbOf(hex: string): [number, number, number] {
  const n = parseInt(hex.slice(1), 16);
  return [(n >> 16) & 255, (n >> 8) & 255, n & 255];
}

function readBest(): number {
  try {
    return Number(localStorage.getItem(STORAGE_KEY) || 0) || 0;
  } catch {
    return 0;
  }
}

function writeBest(n: number): void {
  try {
    localStorage.setItem(STORAGE_KEY, String(n));
  } catch {
    /* ignore */
  }
}
