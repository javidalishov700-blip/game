import type { AudioBus } from "./audio";
import { COLOR, JAM_COUNT, LIVE_SPEED, PALETTE_N, WORLD_H, WORLD_W } from "./const";
import { FxBus } from "./fx";
import { hapticClack, hapticCore, hapticDeath, hapticKick, hapticSplit } from "./haptics";
import { clamp } from "./math";
import {
  Body,
  anyLive,
  collideAll,
  hitTest,
  integrate,
  livingCount,
  resetIds,
  speed,
  walls,
  well,
} from "./physics";
import { SimEvent, comboBump, isJammed, kickBody, livingCores, resolveImpact, scoreFor } from "./rules";
import { spawnInterval, tutorialCluster, waveCluster } from "./spawn";

type Mode = "title" | "play" | "death";

const STORAGE_KEY = "clack-best";

export class ClackGame {
  readonly canvas: HTMLCanvasElement;
  private readonly ctx: CanvasRenderingContext2D;
  private readonly audio: AudioBus;
  private readonly fx = new FxBus();

  private cssW = 390;
  private cssH = 844;
  private scale = 1;
  private ox = 0;
  private oy = 0;
  private lastT = 0;
  private time = 0;
  private mode: Mode = "title";

  private bodies: Body[] = [];
  private trails = new Map<number, { x: number; y: number }[]>();

  private score = 0;
  private best = 0;
  private combo = 0;
  private comboT = 0;
  private spawnT = 7;
  private waveSeed = 1;
  private hint = "TAP A PIECE — IT FLIES THE OTHER WAY";
  private hintT = 4;
  private hover: Body | null = null;
  private clackMute = 0;

  constructor(canvas: HTMLCanvasElement, audio: AudioBus) {
    this.canvas = canvas;
    const ctx = canvas.getContext("2d", { alpha: false });
    if (!ctx) throw new Error("Canvas 2D is unavailable");
    this.ctx = ctx;
    this.audio = audio;
    this.best = readBest();
    this.loadTitle();
  }

  resize(cssW: number, cssH: number, dpr: number): void {
    this.cssW = cssW;
    this.cssH = cssH;
    this.canvas.width = Math.max(1, Math.floor(cssW * dpr));
    this.canvas.height = Math.max(1, Math.floor(cssH * dpr));
    this.ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    this.scale = Math.min(cssW / WORLD_W, cssH / WORLD_H);
    this.ox = (cssW - WORLD_W * this.scale) / 2;
    this.oy = (cssH - WORLD_H * this.scale) / 2;
  }

  skipClock(): void {
    this.lastT = 0;
  }

  hoverAt(x: number, y: number): void {
    const p = this.toWorld(x, y);
    this.hover = hitTest(this.bodies, p.x, p.y, 14);
  }

  tap(x: number, y: number): void {
    this.audio.unlock();
    const p = this.toWorld(x, y);
    if (this.hitMute(x, y)) {
      this.audio.toggleMute();
      return;
    }
    if (this.mode === "title" || this.mode === "death") {
      this.startRun();
      return;
    }
    const body = hitTest(this.bodies, p.x, p.y, 16);
    if (!body) return;
    const ev = kickBody(body, p.x, p.y);
    if (ev) this.consume([ev]);
  }

  tick(nowMs: number): void {
    if (!this.lastT) this.lastT = nowMs;
    let dt = (nowMs - this.lastT) / 1000;
    this.lastT = nowMs;
    if (dt > 0.05) dt = 0.05;
    dt *= this.fx.slow;
    this.time += dt;
    this.hintT = Math.max(0, this.hintT - dt);
    this.clackMute = Math.max(0, this.clackMute - dt);
    if (anyLive(this.bodies, LIVE_SPEED)) this.comboT = Math.max(this.comboT, 0.35);
    else this.comboT = Math.max(0, this.comboT - dt);
    if (this.comboT <= 0) this.combo = 0;

    this.fx.tick(dt);
    this.simulate(dt);
    if (this.mode === "play") this.progress(dt);
    this.draw();
  }

  private toWorld(x: number, y: number): { x: number; y: number } {
    return { x: (x - this.ox) / this.scale, y: (y - this.oy) / this.scale };
  }

  private hitMute(x: number, y: number): boolean {
    return x > this.cssW - 56 && y < 52;
  }

  private loadTitle(): void {
    resetIds();
    this.bodies = tutorialCluster();
  }

  private startRun(): void {
    resetIds();
    this.fx.reset();
    this.trails.clear();
    this.bodies = tutorialCluster();
    this.score = 0;
    this.combo = 0;
    this.comboT = 0;
    this.spawnT = 7.5;
    this.waveSeed = 2;
    this.time = 0;
    this.mode = "play";
    this.hint = "TAP THE GLOWING PIECE ON ITS LEFT SIDE";
    this.hintT = 4.4;
    this.fx.flash = 0.18;
    this.fx.punch = 1.04;
    hapticKick();
  }

  private die(): void {
    if (this.mode === "death") return;
    this.mode = "death";
    this.audio.lose();
    hapticDeath();
    this.fx.impact(1.05);
    this.fx.hitStop(0.5);
    this.fx.burst(WORLD_W * 0.5, well.y + well.h * 0.4, 44, 255, 80, 140, 260, true);
    if (this.score > this.best) {
      this.best = this.score;
      writeBest(this.best);
    }
  }

  private simulate(dt: number): void {
    const steps = dt > 0.02 ? 2 : 1;
    const h = dt / steps;
    for (let s = 0; s < steps; s++) {
      integrate(this.bodies, h);
      const contacts = collideAll(this.bodies);
      collideAll(this.bodies);
      walls(this.bodies);
      if (this.mode !== "play") continue;
      const events: SimEvent[] = [];
      for (const c of contacts) {
        const res = resolveImpact(c, this.bodies);
        events.push(...res.events);
        if (res.spawned.length) this.bodies.push(...res.spawned);
      }
      this.consume(events);
      if (isJammed(this.bodies, JAM_COUNT)) this.die();
    }
    this.updateTrails();
    this.prune();
  }

  private progress(dt: number): void {
    this.spawnT -= dt;
    const cores = livingCores(this.bodies);
    if (cores === 0) this.spawnT = Math.min(this.spawnT, 0.4);
    if (this.spawnT <= 0) {
      const extra = waveCluster(this.score, this.waveSeed++, this.bodies);
      if (extra.length) {
        this.bodies.push(...extra);
        this.audio.spawn();
        this.fx.floater(WORLD_W * 0.5, well.y + 26, "MORE IN", "#8af6ff");
      }
      this.spawnT = spawnInterval(this.score, livingCores(this.bodies));
    }
  }

  private consume(events: SimEvent[]): void {
    if (!events.length) return;
    const bump = comboBump(events);
    if (bump) {
      this.combo = this.comboT > 0 || anyLive(this.bodies, LIVE_SPEED) ? this.combo + bump : bump;
      this.comboT = 1.1;
    }
    this.score += scoreFor(events, Math.max(1, this.combo));

    for (const e of events) {
      if (e.type === "kick") {
        const c = COLOR[e.color % PALETTE_N]!;
        this.fx.burst(e.x, e.y, 12, c.rgb[0], c.rgb[1], c.rgb[2], 240, true);
        this.fx.ring(e.x, e.y, 42, c.fill, 0.26);
        this.audio.kick();
        hapticKick();
        this.fx.impact(0.18);
        this.hint = "BANK OFF STONE · SHATTER GLASS INTO THE STARS";
        this.hintT = 3;
      } else if (e.type === "clack") {
        if (this.clackMute <= 0) {
          this.audio.clack(e.energy);
          hapticClack();
          this.clackMute = 0.04;
        }
        this.fx.impact(Math.min(0.22, e.energy / 500));
      } else if (e.type === "split") {
        const c = COLOR[e.color % PALETTE_N]!;
        this.fx.burst(e.x, e.y, 22, c.rgb[0], c.rgb[1], c.rgb[2], 300, true);
        this.fx.ring(e.x, e.y, 56, c.glow, 0.32);
        this.audio.split();
        hapticSplit();
        this.fx.impact(0.38);
        this.fx.hitStop(0.18);
        this.fx.floater(e.x, e.y, comboWord(this.combo), c.glow);
      } else if (e.type === "corePop") {
        const c = COLOR[e.color % PALETTE_N]!;
        this.fx.burst(e.x, e.y, 20, 255, 255, 255, 260, true);
        this.fx.burst(e.x, e.y, 10, c.rgb[0], c.rgb[1], c.rgb[2], 180);
        this.audio.core();
        hapticCore();
        this.fx.impact(0.3);
        this.fx.floater(e.x, e.y - 8, `+${14 * Math.max(1, this.combo)}`, "#fff");
      }
    }
  }

  private updateTrails(): void {
    const live = new Set<number>();
    for (const b of this.bodies) {
      if (!b.alive || speed(b) < 80) continue;
      live.add(b.id);
      let t = this.trails.get(b.id);
      if (!t) {
        t = [];
        this.trails.set(b.id, t);
      }
      t.push({ x: b.x, y: b.y });
      if (t.length > 8) t.shift();
    }
    for (const id of [...this.trails.keys()]) {
      if (!live.has(id)) this.trails.delete(id);
    }
  }

  private prune(): void {
    if (this.bodies.length < 40) return;
    this.bodies = this.bodies.filter((b) => b.alive);
  }

  private draw(): void {
    const { ctx, cssW: w, cssH: h } = this;
    ctx.save();
    if (this.fx.shake) {
      ctx.translate(
        (Math.random() - 0.5) * 12 * this.fx.shake,
        (Math.random() - 0.5) * 12 * this.fx.shake,
      );
    }
    ctx.translate(w * 0.5, h * 0.5);
    ctx.scale(this.fx.punch, this.fx.punch);
    ctx.translate(-w * 0.5, -h * 0.5);

    this.drawBackdrop();
    ctx.save();
    ctx.translate(this.ox, this.oy);
    ctx.scale(this.scale, this.scale);
    this.drawWell();
    this.drawTrails();
    this.drawBodies();
    this.fx.draw(ctx);
    ctx.restore();

    this.drawHud();
    if (this.mode === "title") this.drawTitle();
    if (this.mode === "death") this.drawDeath();
    if (this.fx.flash > 0) {
      ctx.fillStyle = `rgba(255,255,255,${this.fx.flash * 0.38})`;
      ctx.fillRect(0, 0, w, h);
    }
    if (this.fx.chroma > 0) {
      ctx.fillStyle = `rgba(0,231,255,${this.fx.chroma * 0.1})`;
      ctx.fillRect(0, 0, w * 0.12, h);
      ctx.fillStyle = `rgba(255,43,214,${this.fx.chroma * 0.1})`;
      ctx.fillRect(w * 0.88, 0, w * 0.12, h);
    }
    ctx.restore();
  }

  private drawBackdrop(): void {
    const { ctx, cssW: w, cssH: h } = this;
    const g = ctx.createRadialGradient(w * 0.5, h * 0.42, 20, w * 0.5, h * 0.5, h * 0.75);
    g.addColorStop(0, "#15122a");
    g.addColorStop(0.55, "#090814");
    g.addColorStop(1, "#05040c");
    ctx.fillStyle = g;
    ctx.fillRect(0, 0, w, h);
  }

  private drawWell(): void {
    const { ctx } = this;
    ctx.fillStyle = "rgba(12, 10, 28, 0.92)";
    roundRect(ctx, well.x, well.y, well.w, well.h, 22);
    ctx.fill();
    ctx.strokeStyle = "rgba(0, 231, 255, 0.25)";
    ctx.lineWidth = 2;
    ctx.stroke();
  }

  private drawTrails(): void {
    const { ctx } = this;
    for (const [id, pts] of this.trails) {
      if (pts.length < 2) continue;
      const body = this.bodies.find((b) => b.id === id);
      if (!body) continue;
      const c = COLOR[body.color % PALETTE_N]!;
      ctx.strokeStyle = c.fill;
      ctx.lineWidth = 3.5;
      ctx.globalAlpha = 0.4;
      ctx.beginPath();
      ctx.moveTo(pts[0]!.x, pts[0]!.y);
      for (let i = 1; i < pts.length; i++) ctx.lineTo(pts[i]!.x, pts[i]!.y);
      ctx.stroke();
      ctx.globalAlpha = 1;
    }
  }

  private drawBodies(): void {
    for (const b of this.bodies) {
      if (!b.alive) continue;
      if (b.kind === "core") this.drawCore(b);
      else if (b.kind === "glass") this.drawGlass(b);
      else this.drawStone(b);
    }
  }

  private drawStone(b: Body): void {
    const { ctx } = this;
    const c = COLOR[b.color % PALETTE_N]!;
    const hover = this.hover?.id === b.id && this.mode === "play";
    ctx.save();
    ctx.translate(b.x, b.y);
    ctx.rotate(b.spin * 0.12);
    if (b.heat > 0.15) {
      ctx.shadowColor = c.fill;
      ctx.shadowBlur = 14 + b.heat * 12;
    }
    const g = ctx.createRadialGradient(-b.r * 0.3, -b.r * 0.3, 2, 0, 0, b.r);
    g.addColorStop(0, c.glow);
    g.addColorStop(0.55, c.fill);
    g.addColorStop(1, c.dim);
    ctx.fillStyle = g;
    roundRect(ctx, -b.r, -b.r, b.r * 2, b.r * 2, b.r * 0.34);
    ctx.fill();
    ctx.shadowBlur = 0;
    ctx.fillStyle = "rgba(255,255,255,0.22)";
    roundRect(ctx, -b.r * 0.5, -b.r * 0.58, b.r * 0.85, b.r * 0.34, b.r * 0.16);
    ctx.fill();
    this.drawHint(b, hover);
    ctx.restore();
  }

  private drawGlass(b: Body): void {
    const { ctx } = this;
    const c = COLOR[b.color % PALETTE_N]!;
    const hover = this.hover?.id === b.id && this.mode === "play";
    ctx.save();
    ctx.translate(b.x, b.y);
    ctx.rotate(b.spin * 0.2 + Math.PI / 4);
    ctx.shadowColor = c.fill;
    ctx.shadowBlur = 12 + b.heat * 16;
    ctx.fillStyle = c.fill;
    ctx.globalAlpha = 0.92;
    roundRect(ctx, -b.r * 0.78, -b.r * 0.78, b.r * 1.56, b.r * 1.56, b.r * 0.18);
    ctx.fill();
    ctx.globalAlpha = 0.35;
    ctx.fillStyle = "#fff";
    roundRect(ctx, -b.r * 0.4, -b.r * 0.55, b.r * 0.55, b.r * 0.28, 6);
    ctx.fill();
    ctx.globalAlpha = 1;
    ctx.shadowBlur = 0;
    ctx.restore();
    ctx.save();
    ctx.translate(b.x, b.y);
    this.drawHint(b, hover);
    if (b.hint && this.mode === "play") {
      ctx.fillStyle = `rgba(255,255,255,${0.45 + 0.4 * Math.sin(this.time * 6)})`;
      ctx.beginPath();
      ctx.moveTo(b.r + 10, 0);
      ctx.lineTo(b.r + 2, -7);
      ctx.lineTo(b.r + 2, 7);
      ctx.closePath();
      ctx.fill();
    }
    ctx.restore();
  }

  private drawCore(b: Body): void {
    const { ctx } = this;
    const c = COLOR[b.color % PALETTE_N]!;
    const pulse = 1 + 0.08 * Math.sin(this.time * 8 + b.id);
    ctx.save();
    ctx.translate(b.x, b.y);
    ctx.shadowColor = c.fill;
    ctx.shadowBlur = 16;
    ctx.fillStyle = "#fff";
    ctx.beginPath();
    ctx.arc(0, 0, b.r * 0.42 * pulse, 0, Math.PI * 2);
    ctx.fill();
    ctx.shadowBlur = 0;
    ctx.strokeStyle = c.glow;
    ctx.lineWidth = 2.4;
    ctx.beginPath();
    ctx.arc(0, 0, b.r * pulse, 0, Math.PI * 2);
    ctx.stroke();
    ctx.fillStyle = c.fill;
    ctx.globalAlpha = 0.85;
    ctx.beginPath();
    for (let i = 0; i < 8; i++) {
      const a = (i / 8) * Math.PI * 2 + this.time * 1.5;
      const r = i % 2 === 0 ? b.r * 0.95 : b.r * 0.4;
      const x = Math.cos(a) * r;
      const y = Math.sin(a) * r;
      if (i === 0) ctx.moveTo(x, y);
      else ctx.lineTo(x, y);
    }
    ctx.closePath();
    ctx.fill();
    ctx.globalAlpha = 1;
    ctx.restore();
  }

  private drawHint(b: Body, hover: boolean): void {
    const { ctx } = this;
    if (b.hint && this.mode !== "death") {
      ctx.strokeStyle = `rgba(255,255,255,${0.4 + 0.4 * Math.sin(this.time * 6)})`;
      ctx.lineWidth = 2;
      ctx.beginPath();
      ctx.arc(0, 0, b.r + 6, 0, Math.PI * 2);
      ctx.stroke();
    }
    if (hover) {
      ctx.strokeStyle = "rgba(255,255,255,0.7)";
      ctx.lineWidth = 2;
      ctx.beginPath();
      ctx.arc(0, 0, b.r + 4, 0, Math.PI * 2);
      ctx.stroke();
    }
  }

  private drawHud(): void {
    const { ctx, cssW: w } = this;
    ctx.fillStyle = "rgba(220, 230, 255, 0.55)";
    ctx.font = "700 13px system-ui, sans-serif";
    ctx.textAlign = "left";
    ctx.fillText("BEST  " + this.best, 16, 28);
    ctx.textAlign = "right";
    ctx.fillText(this.audio.isMuted ? "MUTE" : "SOUND", w - 16, 28);
    ctx.textAlign = "center";
    ctx.font = "800 34px system-ui, sans-serif";
    ctx.fillStyle = "#f4f7ff";
    const title = this.mode === "play" || this.mode === "death" ? String(this.score) : "CLACK";
    ctx.fillText(title, w * 0.5, 36);

    const jam = livingCount(this.bodies) / JAM_COUNT;
    const barW = Math.min(220, w * 0.5);
    const bx = (w - barW) / 2;
    const by = 48;
    ctx.fillStyle = "rgba(255,255,255,0.08)";
    roundRect(ctx, bx, by, barW, 7, 4);
    ctx.fill();
    ctx.fillStyle = jam > 0.72 ? "#ff4d7a" : "#00e7ff";
    roundRect(ctx, bx, by, Math.max(2, barW * clamp(jam, 0, 1)), 7, 4);
    ctx.fill();
    ctx.fillStyle = "rgba(220,230,255,0.4)";
    ctx.font = "700 9px system-ui, sans-serif";
    ctx.fillText("JAM", w * 0.5, by + 18);

    if (this.hintT > 0 && this.mode === "play") {
      ctx.globalAlpha = clamp(this.hintT, 0, 1);
      ctx.font = "700 13px system-ui, sans-serif";
      ctx.fillStyle = "#c8e8ff";
      ctx.fillText(this.hint, w * 0.5, this.cssH - 22);
      ctx.globalAlpha = 1;
    }
    if (this.combo > 1 && this.mode === "play") {
      ctx.font = "800 18px system-ui, sans-serif";
      ctx.fillStyle = "#ffe97a";
      ctx.fillText(`COMBO x${this.combo}`, w * 0.5, 78);
    }
  }

  private drawTitle(): void {
    const { ctx, cssW: w, cssH: h } = this;
    ctx.textAlign = "center";
    ctx.fillStyle = "rgba(8,6,16,0.28)";
    ctx.fillRect(0, h * 0.72, w, h * 0.28);
    ctx.font = "600 13px system-ui, sans-serif";
    ctx.fillStyle = "#9fdfff";
    ctx.fillText("Kick one. Bank the rest. Shatter the glass.", w * 0.5, h - 58);
    ctx.font = "800 18px system-ui, sans-serif";
    ctx.fillStyle = "#00e7ff";
    ctx.globalAlpha = 0.55 + 0.45 * Math.sin(this.time * 4);
    ctx.fillText("TAP TO PLAY", w * 0.5, h - 28);
    ctx.globalAlpha = 1;
  }

  private drawDeath(): void {
    const { ctx, cssW: w, cssH: h } = this;
    ctx.fillStyle = "rgba(8, 4, 14, 0.46)";
    ctx.fillRect(0, 0, w, h);
    ctx.textAlign = "center";
    ctx.fillStyle = "#ff6b9a";
    ctx.font = "800 26px system-ui, sans-serif";
    ctx.fillText("JAMMED", w * 0.5, h * 0.24);
    ctx.fillStyle = "#f4f7ff";
    ctx.font = "800 58px system-ui, sans-serif";
    ctx.fillText(String(this.score), w * 0.5, h * 0.24 + 70);
    ctx.font = "700 16px system-ui, sans-serif";
    ctx.fillStyle = this.score >= this.best && this.score > 0 ? "#ffe97a" : "#9fdfff";
    ctx.fillText(
      this.score >= this.best && this.score > 0 ? "NEW BEST" : "BEST  " + this.best,
      w * 0.5,
      h * 0.24 + 98,
    );
    ctx.fillStyle = "#00e7ff";
    ctx.fillText("TAP TO RETRY", w * 0.5, h * 0.86);
  }
}

function comboWord(combo: number): string {
  if (combo >= 10) return "BANKRUPT";
  if (combo >= 6) return "SHATTER";
  if (combo >= 3) return "CLACK";
  return "SPLIT";
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
