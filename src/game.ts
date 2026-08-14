import type { AudioBus } from "./audio";
import {
  COLOR,
  COMBO_WINDOW,
  PALETTE_N,
  RIFT_DECAY,
  RIFT_MAX,
  WORLD_H,
  WORLD_W,
} from "./const";
import { FxBus } from "./fx";
import {
  hapticAnnihilate,
  hapticContagion,
  hapticCore,
  hapticDeath,
  hapticInvert,
  hapticSnap,
} from "./haptics";
import { clamp } from "./math";
import {
  Body,
  Weld,
  collideAll,
  dangerY,
  hitTest,
  integrate,
  resetIds,
  solveWelds,
  walls,
  well,
} from "./physics";
import {
  Shockwave,
  SimEvent,
  Wake,
  annihilateWave,
  applyShockwave,
  applyWakes,
  collectEscapes,
  comboBump,
  detonate,
  invertBody,
  isOverflow,
  livingCores,
  markSettledIncoming,
  riftDelta,
  scoreFor,
  snapAnchor,
  resolveImpact,
} from "./rules";
import { spawnInterval, tutorialPile, wavePile } from "./spawn";

type Mode = "title" | "play" | "death";

const STORAGE_KEY = "antimass-best";

export class AntimassGame {
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
  private welds: Weld[] = [];
  private wakes: Wake[] = [];
  private waves: { wave: Shockwave; t: number }[] = [];
  private trails = new Map<number, { x: number; y: number }[]>();

  private score = 0;
  private best = 0;
  private combo = 0;
  private comboT = 0;
  private rift = 0;
  private spawnT = 7;
  private waveSeed = 1;
  private hint = "TAP THE GLOWING BRICK";
  private hintT = 4;
  private hover: Body | null = null;

  constructor(canvas: HTMLCanvasElement, audio: AudioBus) {
    this.canvas = canvas;
    const ctx = canvas.getContext("2d", { alpha: false });
    if (!ctx) throw new Error("Canvas 2D is unavailable");
    this.ctx = ctx;
    this.audio = audio;
    this.best = readBest();
    this.loadTitlePile();
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
    this.actOn(body);
  }

  tick(nowMs: number): void {
    if (!this.lastT) this.lastT = nowMs;
    let dt = (nowMs - this.lastT) / 1000;
    this.lastT = nowMs;
    if (dt > 0.05) dt = 0.05;
    dt *= this.fx.slow;
    this.time += dt;
    this.hintT = Math.max(0, this.hintT - dt);
    this.comboT = Math.max(0, this.comboT - dt);
    if (this.comboT <= 0) this.combo = 0;

    this.fx.tick(dt);
    if (this.mode === "play" || this.mode === "title") this.simulate(dt);
    if (this.mode === "play") this.progress(dt);
    this.draw();
  }

  private toWorld(x: number, y: number): { x: number; y: number } {
    return {
      x: (x - this.ox) / this.scale,
      y: (y - this.oy) / this.scale,
    };
  }

  private hitMute(x: number, y: number): boolean {
    return x > this.cssW - 56 && y < 52;
  }

  private loadTitlePile(): void {
    resetIds();
    const pile = tutorialPile();
    this.bodies = pile.bodies;
    this.welds = pile.welds;
    this.wakes = [];
  }

  private startRun(): void {
    resetIds();
    this.fx.reset();
    this.trails.clear();
    const pile = tutorialPile();
    this.bodies = pile.bodies;
    this.welds = pile.welds;
    this.wakes = [];
    this.waves = [];
    this.score = 0;
    this.combo = 0;
    this.comboT = 0;
    this.rift = 0;
    this.spawnT = 7.2;
    this.waveSeed = 2;
    this.time = 0;
    this.mode = "play";
    this.hint = "TAP THE GLOWING BRICK — INVERT MASS";
    this.hintT = 4.2;
    this.fx.flash = 0.2;
    this.fx.punch = 1.04;
    hapticInvert();
  }

  private die(): void {
    if (this.mode === "death") return;
    this.mode = "death";
    this.audio.lose();
    hapticDeath();
    this.fx.impact(1.1);
    this.fx.hitStop(0.55);
    this.fx.burst(WORLD_W * 0.5, well.y + 40, 48, 255, 70, 110, 280, true);
    if (this.score > this.best) {
      this.best = this.score;
      writeBest(this.best);
    }
  }

  private actOn(body: Body): void {
    const events: SimEvent[] = [];
    if (body.kind === "brick" && body.sign > 0) {
      const ev = invertBody(body, this.wakes);
      if (ev) events.push(ev);
    } else if (body.kind === "brick" && body.sign < 0) {
      const boom = detonate(body);
      if (boom) {
        events.push(boom.event);
        this.queueWave(boom.wave);
        events.push(...applyShockwave(boom.wave, this.bodies));
      }
    } else if (body.kind === "anchor") {
      events.push(...snapAnchor(body, this.welds, this.bodies));
    }
    this.consume(events);
  }

  private queueWave(wave: Shockwave): void {
    this.waves.push({ wave, t: 0 });
    const c = COLOR[wave.color % PALETTE_N]!;
    this.fx.ring(wave.x, wave.y, wave.r, c.glow, 0.5);
  }

  private simulate(dt: number): void {
    const steps = dt > 0.02 ? 2 : 1;
    const h = dt / steps;
    for (let s = 0; s < steps; s++) {
      applyWakes(this.wakes, this.bodies, h);
      integrate(this.bodies, h);
      const contacts = collideAll(this.bodies);
      const snaps = solveWelds(this.welds, this.bodies);
      walls(this.bodies);
      if (this.mode !== "play") continue;
      const events: SimEvent[] = [];
      for (const c of contacts) events.push(...resolveImpact(c, this.bodies, this.wakes));
      for (const snap of snaps) events.push({ type: "snap", x: snap.x, y: snap.y });
      const extra: SimEvent[] = [];
      for (const e of events) {
        if (e.type !== "annihilate") continue;
        const wave = annihilateWave(e.x, e.y, e.color);
        this.queueWave(wave);
        extra.push(...applyShockwave(wave, this.bodies));
      }
      events.push(...extra);
      events.push(...collectEscapes(this.bodies, well.y));
      markSettledIncoming(this.bodies);
      this.consume(events);
      if (isOverflow(this.bodies) || this.rift >= RIFT_MAX) this.die();
    }
    this.updateTrails();
    this.prune();
  }

  private progress(dt: number): void {
    this.rift = clamp(this.rift - RIFT_DECAY * dt, 0, RIFT_MAX);
    this.spawnT -= dt;
    const cores = livingCores(this.bodies);
    if (cores === 0) this.spawnT = Math.min(this.spawnT, 0.45);
    if (this.spawnT <= 0) {
      this.dropWave();
      this.spawnT = spawnInterval(this.score, livingCores(this.bodies));
    }
  }

  private dropWave(): void {
    const pile = wavePile(this.score, this.waveSeed++, this.bodies);
    if (!pile.bodies.length) return;
    this.bodies.push(...pile.bodies);
    this.welds.push(...pile.welds);
    this.audio.spawn();
    this.fx.floater(WORLD_W * 0.5, well.y + 28, "PAYLOAD", "#8af6ff");
  }

  private consume(events: SimEvent[]): void {
    if (!events.length) return;
    const bump = comboBump(events);
    if (bump) {
      this.combo = this.comboT > 0 ? this.combo + bump : bump;
      this.comboT = COMBO_WINDOW;
    }
    const gain = scoreFor(events, Math.max(1, this.combo));
    this.score += gain;
    this.rift = clamp(this.rift + riftDelta(events), 0, RIFT_MAX);

    for (const e of events) {
      if (e.type === "invert") {
        const c = COLOR[e.color % PALETTE_N]!;
        this.fx.burst(e.x, e.y, e.contagion ? 14 : 10, c.rgb[0], c.rgb[1], c.rgb[2], 220, true);
        this.fx.ring(e.x, e.y, 46, c.fill, 0.28);
        if (e.contagion) {
          this.audio.contagion();
          hapticContagion();
          this.fx.impact(0.22);
        } else {
          this.audio.invert();
          hapticInvert();
          this.fx.impact(0.16);
          this.hint = "IMPACTS SPREAD INVERSION · SAME COLOR ANNIHILATES";
          this.hintT = 3.2;
        }
      } else if (e.type === "annihilate") {
        const c = COLOR[e.color % PALETTE_N]!;
        this.fx.burst(e.x, e.y, 36, c.rgb[0], c.rgb[1], c.rgb[2], 360, true);
        this.fx.impact(0.7);
        this.fx.hitStop(0.42);
        this.audio.annihilate();
        hapticAnnihilate();
        this.fx.floater(e.x, e.y, comboWord(this.combo), c.glow, 1.25);
      } else if (e.type === "corePop") {
        const c = COLOR[e.color % PALETTE_N]!;
        this.fx.burst(e.x, e.y, 18, 255, 255, 255, 240, true);
        this.fx.burst(e.x, e.y, 10, c.rgb[0], c.rgb[1], c.rgb[2], 180);
        this.audio.core();
        hapticCore();
        this.fx.impact(0.28);
        this.fx.floater(e.x, e.y - 8, `+${12 * Math.max(1, this.combo)}`, "#fff");
        this.hint = "ANCHORS SNAP CABLES · TAP AN INVERTED BRICK TO DETONATE";
        this.hintT = Math.max(this.hintT, 2.6);
      } else if (e.type === "snap") {
        this.audio.snap();
        hapticSnap();
        this.fx.burst(e.x, e.y, 12, 255, 210, 80, 160, true);
        this.fx.impact(0.2);
      } else if (e.type === "detonate") {
        this.audio.detonate();
        this.fx.impact(0.4);
        this.fx.hitStop(0.22);
      } else if (e.type === "escape") {
        this.audio.rift();
        this.fx.impact(0.12);
      }
    }
    if (gain > 0 && events.some((e) => e.type === "annihilate" || e.type === "corePop")) {
      this.fx.floater(WORLD_W * 0.5, 58, this.combo > 1 ? `x${this.combo}` : `+${gain}`, "#ffe97a");
    }
  }

  private updateTrails(): void {
    const live = new Set<number>();
    for (const b of this.bodies) {
      if (!b.alive || b.sign > 0) continue;
      live.add(b.id);
      let t = this.trails.get(b.id);
      if (!t) {
        t = [];
        this.trails.set(b.id, t);
      }
      t.push({ x: b.x, y: b.y });
      if (t.length > 9) t.shift();
    }
    for (const id of [...this.trails.keys()]) {
      if (!live.has(id)) this.trails.delete(id);
    }
  }

  private prune(): void {
    if (this.bodies.length < 50) return;
    this.bodies = this.bodies.filter((b) => b.alive);
    this.welds = this.welds.filter((w) => !w.broken);
  }

  private draw(): void {
    const { ctx, cssW: w, cssH: h } = this;
    ctx.save();
    if (this.fx.shake) {
      ctx.translate(
        (Math.random() - 0.5) * 14 * this.fx.shake,
        (Math.random() - 0.5) * 14 * this.fx.shake,
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
    this.drawWelds();
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
    ctx.strokeStyle = "rgba(0, 231, 255, 0.22)";
    ctx.lineWidth = 2;
    ctx.stroke();

    ctx.save();
    ctx.beginPath();
    roundRect(ctx, well.x, well.y, well.w, well.h, 22);
    ctx.clip();
    ctx.strokeStyle = "rgba(255,255,255,0.035)";
    ctx.lineWidth = 1;
    for (let y = well.y + 24; y < well.y + well.h; y += 28) {
      ctx.beginPath();
      ctx.moveTo(well.x, y);
      ctx.lineTo(well.x + well.w, y);
      ctx.stroke();
    }
    ctx.strokeStyle = "rgba(255, 70, 110, 0.7)";
    ctx.setLineDash([6, 7]);
    ctx.beginPath();
    ctx.moveTo(well.x + 8, dangerY);
    ctx.lineTo(well.x + well.w - 8, dangerY);
    ctx.stroke();
    ctx.setLineDash([]);
    ctx.fillStyle = "rgba(255, 70, 110, 0.72)";
    ctx.font = "800 10px system-ui, sans-serif";
    ctx.textAlign = "left";
    ctx.fillText("OVERFLOW", well.x + 10, dangerY - 6);
    ctx.restore();
  }

  private drawWelds(): void {
    const { ctx } = this;
    const map = new Map(this.bodies.map((b) => [b.id, b]));
    ctx.lineCap = "round";
    for (const w of this.welds) {
      if (w.broken) continue;
      const a = map.get(w.a);
      const b = map.get(w.b);
      if (!a?.alive || !b?.alive) continue;
      const pulse = 0.45 + 0.25 * Math.sin(this.time * 6 + a.id);
      ctx.strokeStyle = `rgba(255, 210, 80, ${pulse})`;
      ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.moveTo(a.x, a.y);
      ctx.lineTo(b.x, b.y);
      ctx.stroke();
    }
  }

  private drawTrails(): void {
    const { ctx } = this;
    for (const [id, pts] of this.trails) {
      if (pts.length < 2) continue;
      const body = this.bodies.find((b) => b.id === id);
      if (!body) continue;
      const c = COLOR[body.color % PALETTE_N]!;
      ctx.strokeStyle = c.fill;
      ctx.lineWidth = 4;
      ctx.globalAlpha = 0.35;
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
      else if (b.kind === "anchor") this.drawAnchor(b);
      else this.drawBrick(b);
    }
  }

  private drawBrick(b: Body): void {
    const { ctx } = this;
    const c = COLOR[b.color % PALETTE_N]!;
    const hover = this.hover?.id === b.id && this.mode === "play";
    ctx.save();
    ctx.translate(b.x, b.y);
    ctx.rotate(b.spin * 0.15);
    if (b.sign < 0 || b.heat > 0.2) {
      ctx.shadowColor = c.fill;
      ctx.shadowBlur = 16 + b.heat * 18;
    }
    const g = ctx.createRadialGradient(-b.r * 0.3, -b.r * 0.35, 2, 0, 0, b.r);
    if (b.sign < 0) {
      g.addColorStop(0, "#fff");
      g.addColorStop(0.22, c.glow);
      g.addColorStop(0.7, c.fill);
      g.addColorStop(1, "#05040c");
    } else {
      g.addColorStop(0, c.glow);
      g.addColorStop(0.55, c.fill);
      g.addColorStop(1, c.dim);
    }
    ctx.fillStyle = g;
    roundRect(ctx, -b.r, -b.r, b.r * 2, b.r * 2, b.r * 0.42);
    ctx.fill();
    ctx.shadowBlur = 0;
    if (b.sign > 0) {
      ctx.fillStyle = "rgba(255,255,255,0.28)";
      roundRect(ctx, -b.r * 0.55, -b.r * 0.62, b.r * 0.9, b.r * 0.38, b.r * 0.2);
      ctx.fill();
    } else {
      ctx.fillStyle = "#05040c";
      ctx.beginPath();
      ctx.arc(0, 0, b.r * 0.34, 0, Math.PI * 2);
      ctx.fill();
      ctx.fillStyle = c.glow;
      ctx.beginPath();
      ctx.moveTo(0, -b.r * 0.72);
      ctx.lineTo(b.r * 0.22, -b.r * 0.28);
      ctx.lineTo(-b.r * 0.22, -b.r * 0.28);
      ctx.closePath();
      ctx.fill();
    }
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
      roundRect(ctx, -b.r - 3, -b.r - 3, b.r * 2 + 6, b.r * 2 + 6, b.r * 0.48);
      ctx.stroke();
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
    ctx.shadowBlur = 18;
    ctx.fillStyle = "#fff";
    ctx.beginPath();
    ctx.arc(0, 0, b.r * 0.45 * pulse, 0, Math.PI * 2);
    ctx.fill();
    ctx.shadowBlur = 0;
    ctx.strokeStyle = c.glow;
    ctx.lineWidth = 2.5;
    ctx.beginPath();
    ctx.arc(0, 0, b.r * pulse, 0, Math.PI * 2);
    ctx.stroke();
    const spikes = 6;
    ctx.fillStyle = c.fill;
    ctx.globalAlpha = 0.85;
    ctx.beginPath();
    for (let i = 0; i < spikes; i++) {
      const a = (i / spikes) * Math.PI * 2 + this.time * 1.4;
      const r = i % 2 === 0 ? b.r * 0.95 : b.r * 0.42;
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

  private drawAnchor(b: Body): void {
    const { ctx } = this;
    const hover = this.hover?.id === b.id && this.mode === "play";
    ctx.save();
    ctx.translate(b.x, b.y);
    ctx.fillStyle = "#2a2438";
    ctx.strokeStyle = hover ? "#ffe97a" : "#ffd21a";
    ctx.lineWidth = 2.5;
    hex(ctx, b.r);
    ctx.fill();
    ctx.stroke();
    ctx.fillStyle = "#ffd21a";
    ctx.beginPath();
    ctx.arc(0, 0, 4, 0, Math.PI * 2);
    ctx.fill();
    ctx.restore();
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
    const title =
      this.mode === "play" || this.mode === "death" ? String(this.score) : "ANTIMASS";
    ctx.fillText(title, w * 0.5, 36);

    const barW = Math.min(220, w * 0.5);
    const bx = (w - barW) / 2;
    const by = 48;
    ctx.fillStyle = "rgba(255,255,255,0.08)";
    roundRect(ctx, bx, by, barW, 7, 4);
    ctx.fill();
    const riftU = this.rift / RIFT_MAX;
    ctx.fillStyle = riftU > 0.72 ? "#ff4d7a" : "#00e7ff";
    roundRect(ctx, bx, by, Math.max(2, barW * riftU), 7, 4);
    ctx.fill();
    ctx.fillStyle = "rgba(220,230,255,0.4)";
    ctx.font = "700 9px system-ui, sans-serif";
    ctx.fillText("RIFT", w * 0.5, by + 18);

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
    ctx.fillText("Invert mass. Weaponize the collapse.", w * 0.5, h - 58);
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
    ctx.fillText(this.rift >= RIFT_MAX ? "WELL RUPTURED" : "OVERFLOW", w * 0.5, h * 0.24);
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
  if (combo >= 12) return "GODFLIP";
  if (combo >= 8) return "WELLBREAK";
  if (combo >= 5) return "ANNIHILATE";
  if (combo >= 3) return "CHAIN";
  return "RIFT";
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

function hex(ctx: CanvasRenderingContext2D, r: number): void {
  ctx.beginPath();
  for (let i = 0; i < 6; i++) {
    const a = (Math.PI / 3) * i - Math.PI / 6;
    const x = Math.cos(a) * r;
    const y = Math.sin(a) * r;
    if (i === 0) ctx.moveTo(x, y);
    else ctx.lineTo(x, y);
  }
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
