import type { AudioBus } from "./audio";
import { circularSpeed, clamp, len, lerp, rand } from "./math";

type Mode = "title" | "play" | "death";
type DeathKind = "burn" | "fade";

type Mote = {
  x: number;
  y: number;
  phase: number;
  worth: number;
  hot: boolean;
};

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
  drag: number;
};

type Floater = {
  x: number;
  y: number;
  text: string;
  life: number;
  color: string;
};

type Trail = { x: number; y: number; a: number };

const MAX_PARTICLES = 160;
const STORAGE_KEY = "wick-best";
const COLLECT_NOTES_RESET = 1.15;

export class WickGame {
  readonly canvas: HTMLCanvasElement;
  private readonly ctx: CanvasRenderingContext2D;
  private readonly audio: AudioBus;

  private w = 390;
  private h = 844;
  private time = 0;
  private lastT = 0;
  private mode: Mode = "title";
  private holding = false;
  private wasHolding = false;
  private allowRestart = false;
  private deathKind: DeathKind = "burn";
  private deathAge = 0;
  private hitStop = 0;

  private cx = 0;
  private cy = 0;
  private px = 0;
  private py = 0;
  private vx = 0;
  private vy = 0;
  private flameR = 40;
  private lightR = 180;
  private gm = 1;
  private flameLeanX = 0;
  private flameLeanY = 0;

  private score = 0;
  private best = 0;
  private combo = 0;
  private comboTimer = 0;
  private motes: Mote[] = [];
  private spawnTimer = 0;
  private collected = 0;
  private hint = "HOLD TO ORBIT";
  private hintLife = 2.8;
  private taughtRelease = false;
  private seared = false;

  private particles: Particle[] = [];
  private particleAt = 0;
  private floaters: Floater[] = [];
  private trail: Trail[] = [];
  private stars: { x: number; y: number; a: number; s: number }[] = [];

  private shake = 0;
  private flash = 0;
  private zoomPunch = 1;
  private ambientT = 0;
  private lastRecapture = 0;

  constructor(canvas: HTMLCanvasElement, audio: AudioBus) {
    this.canvas = canvas;
    const ctx = canvas.getContext("2d", { alpha: false });
    if (!ctx) throw new Error("Canvas 2D is unavailable");
    this.ctx = ctx;
    this.audio = audio;
    this.best = Number(localStorage.getItem(STORAGE_KEY) || 0) || 0;
    this.seedStars();
    this.resetSpark(true);
  }

  resize(cssW: number, cssH: number, dpr: number): void {
    this.w = cssW;
    this.h = cssH;
    this.canvas.width = Math.max(1, Math.floor(cssW * dpr));
    this.canvas.height = Math.max(1, Math.floor(cssH * dpr));
    this.ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    this.cx = this.w * 0.5;
    this.cy = this.h * 0.48;
    this.seedStars();
    if (this.mode === "title") this.resetSpark(true);
  }

  setHolding(down: boolean): void {
    this.holding = down;
    if (down) this.audio.unlock();
    this.audio.setHolding(down && this.mode === "play");

    if (this.mode === "title" && down) {
      this.startRun();
      return;
    }
    if (this.mode === "death" && down && this.allowRestart) {
      this.startRun();
    }
  }

  skipClock(): void {
    this.lastT = 0;
  }

  tick(nowMs: number): void {
    if (!this.lastT) this.lastT = nowMs;
    let dt = (nowMs - this.lastT) / 1000;
    this.lastT = nowMs;
    if (dt > 0.05) dt = 0.05;
    const rawDt = dt;

    if (this.mode === "death") {
      this.deathAge += rawDt;
      if (!this.holding && this.deathAge > 0.4) this.allowRestart = true;
      dt *= this.deathAge < 0.55 ? 0.22 : 0;
    }
    if (this.hitStop > 0) {
      this.hitStop -= rawDt;
      dt *= 0.12;
    }

    this.time += dt;
    this.ambientT += dt;
    this.shake = Math.max(0, this.shake - dt * 7);
    this.flash = Math.max(0, this.flash - dt * 3.2);
    this.zoomPunch = lerp(this.zoomPunch, 1, 1 - Math.pow(0.001, dt));
    this.hintLife = Math.max(0, this.hintLife - dt);
    this.comboTimer = Math.max(0, this.comboTimer - dt);
    if (this.comboTimer <= 0) this.combo = 0;

    this.updateRadii();
    this.updatePhysics(dt);
    this.updateMotes(dt);
    this.updateParticles(dt);
    this.updateFloaters(dt);
    this.draw();
  }

  private get difficulty(): number {
    return 1 - Math.exp(-(this.time * 0.035 + this.score * 0.018));
  }

  private minDim(): number {
    return Math.min(this.w, this.h);
  }

  private updateRadii(): void {
    const m = this.minDim();
    const d = this.mode === "play" ? this.difficulty : 0;
    this.flameR = m * lerp(0.07, 0.155, d);
    this.lightR = m * lerp(0.455, 0.305, d);
    const mid = (this.flameR + this.lightR) * 0.52;
    const period = lerp(2.25, 1.55, d);
    const omega = (Math.PI * 2) / period;
    this.gm = mid * mid * mid * omega * omega;
  }

  private resetSpark(orbit: boolean): void {
    this.cx = this.w * 0.5;
    this.cy = this.h * 0.48;
    this.updateRadii();
    const r = (this.flameR + this.lightR) * 0.52;
    const ang = -Math.PI * 0.25;
    this.px = this.cx + Math.cos(ang) * r;
    this.py = this.cy + Math.sin(ang) * r;
    const speed = circularSpeed(this.gm, r);
    this.vx = -Math.sin(ang) * speed;
    this.vy = Math.cos(ang) * speed;
    this.trail = [];
    this.seared = false;
    if (!orbit) {
      this.vx = 0;
      this.vy = 0;
    }
  }

  private startRun(): void {
    this.mode = "play";
    this.score = 0;
    this.combo = 0;
    this.comboTimer = 0;
    this.collected = 0;
    this.time = 0;
    this.motes = [];
    this.floaters = [];
    this.spawnTimer = 0.15;
    this.hint = "HOLD TO ORBIT";
    this.hintLife = 2.4;
    this.taughtRelease = false;
    this.allowRestart = false;
    this.deathAge = 0;
    this.flash = 0.35;
    this.zoomPunch = 1.04;
    this.wasHolding = true;
    this.lastT = 0;
    this.resetSpark(true);
    this.placeRailMote(0.85);
    this.audio.setHolding(true);
    this.buzz(12);
  }

  private die(kind: DeathKind): void {
    if (this.mode !== "play") return;
    this.mode = "death";
    this.deathKind = kind;
    this.deathAge = 0;
    this.allowRestart = false;
    this.hitStop = 0.08;
    this.shake = kind === "burn" ? 1 : 0.55;
    this.flash = kind === "burn" ? 0.9 : 0.45;
    this.audio.setHolding(false);
    if (kind === "burn") {
      this.audio.burn();
      this.burst(this.px, this.py, 70, 255, 90, 20, 420);
      this.buzz(40);
    } else {
      this.audio.fade();
      this.burst(this.px, this.py, 48, 140, 210, 255, 80, true);
      this.buzz(25);
    }
    if (this.score > this.best) {
      this.best = this.score;
      localStorage.setItem(STORAGE_KEY, String(this.best));
    }
  }

  private updatePhysics(dt: number): void {
    if (this.mode === "title") {
      this.cling(dt);
      this.rememberTrail();
      return;
    }
    if (this.mode === "death" && dt <= 0) return;

    const justHeld = this.holding && !this.wasHolding;
    this.wasHolding = this.holding;

    if (this.holding) {
      if (justHeld && this.mode === "play") this.snapRecapture();
      this.cling(dt);
    } else {
      // Coast. Tiny drag keeps high-speed voids readable.
      this.vx *= 1 - 0.04 * dt;
      this.vy *= 1 - 0.04 * dt;
      const cap = this.minDim() * 2.4;
      const spd = len(this.vx, this.vy);
      if (spd > cap) {
        this.vx = (this.vx / spd) * cap;
        this.vy = (this.vy / spd) * cap;
      }
      this.px += this.vx * dt;
      this.py += this.vy * dt;
    }

    this.rememberTrail();

    const dx = this.px - this.cx;
    const dy = this.py - this.cy;
    const dist = Math.hypot(dx, dy);
    this.flameLeanX = lerp(this.flameLeanX, clamp(dx / this.lightR, -1, 1) * 10, 0.12);
    this.flameLeanY = lerp(this.flameLeanY, clamp(dy / this.lightR, -1, 1) * 10, 0.12);

    if (this.mode !== "play") return;

    if (dist < this.flameR + 7) this.die("burn");
    else if (dist > this.lightR - 5) this.die("fade");

    if (dist < this.flameR + 18) {
      if (!this.seared && dist > this.flameR + 8) {
        const receding = dx * this.vx + dy * this.vy > 0;
        if (receding) {
          this.seared = true;
          this.audio.nearMiss();
          this.addFloater(this.px, this.py - 18, "SEARED", "#ffb089");
          this.bumpScore(1);
          this.shake = Math.max(this.shake, 0.28);
          this.buzz(8);
        }
      }
    } else if (dist > this.flameR + 28) {
      this.seared = false;
    }
  }

  /**
   * Hold = locked circular rail at the current altitude.
   * Radius stays put (the flame grows into you); release to change altitude.
   */
  private cling(dt: number): void {
    const dx = this.px - this.cx;
    const dy = this.py - this.cy;
    const dist = Math.max(12, Math.hypot(dx, dy));
    let ang = Math.atan2(dy, dx);
    const tx = -Math.sin(ang);
    const ty = Math.cos(ang);
    const sign = this.vx * tx + this.vy * ty >= 0 ? 1 : -1;
    const spd = circularSpeed(this.gm, dist);
    ang += sign * (spd / dist) * dt;
    this.px = this.cx + Math.cos(ang) * dist;
    this.py = this.cy + Math.sin(ang) * dist;
    this.vx = -Math.sin(ang) * spd * sign;
    this.vy = Math.cos(ang) * spd * sign;
  }

  private snapRecapture(): void {
    const now = this.time;
    if (now - this.lastRecapture <= 0.18) return;
    this.lastRecapture = now;
    const dist = Math.max(12, Math.hypot(this.px - this.cx, this.py - this.cy));
    const rx = (this.px - this.cx) / dist;
    const ry = (this.py - this.cy) / dist;
    const radial = Math.abs(this.vx * rx + this.vy * ry);
    this.audio.recapture();
    this.burst(this.px, this.py, radial > 90 ? 18 : 10, 255, 200, 80, 90);
    if (radial > 90) {
      this.shake = Math.max(this.shake, 0.22);
      this.zoomPunch = 1.03;
    }
  }

  private rememberTrail(): void {
    this.trail.push({ x: this.px, y: this.py, a: 1 });
    if (this.trail.length > 22) this.trail.shift();
  }

  private updateMotes(dt: number): void {
    if (this.mode !== "play") return;
    const maxMotes = 2 + Math.floor(this.difficulty * 3);
    this.spawnTimer -= dt;
    if (this.spawnTimer <= 0 && this.motes.length < maxMotes) {
      this.spawnMote();
      this.spawnTimer = lerp(1.15, 0.55, this.difficulty);
    }

    for (let i = this.motes.length - 1; i >= 0; i--) {
      const mote = this.motes[i]!;
      mote.phase += dt * 3;
      if (len(mote.x - this.px, mote.y - this.py) < 16) {
        this.collect(mote);
        this.motes.splice(i, 1);
      }
    }
  }

  private spawnMote(): void {
    const d = this.difficulty;
    for (let i = 0; i < 16; i++) {
      const hot = this.collected >= 2 && Math.random() < 0.28 + d * 0.35;
      if (hot && !this.taughtRelease) {
        this.hint = "RELEASE TO FLY";
        this.hintLife = 2.6;
        this.taughtRelease = true;
      }
      const u = hot
        ? Math.random() < 0.5
          ? rand(0.06, 0.2)
          : rand(0.8, 0.94)
        : rand(0.32, 0.68);
      const dist = lerp(this.flameR + 22, this.lightR - 18, u);
      const ang = rand(0, Math.PI * 2);
      const x = this.cx + Math.cos(ang) * dist;
      const y = this.cy + Math.sin(ang) * dist;
      if (len(x - this.px, y - this.py) > this.minDim() * 0.16) {
        this.motes.push({ x, y, phase: rand(0, 6), worth: hot ? 2 : 1, hot });
        return;
      }
    }
  }

  private placeRailMote(ahead: number): void {
    const dx = this.px - this.cx;
    const dy = this.py - this.cy;
    const dist = Math.max(8, Math.hypot(dx, dy));
    const ang = Math.atan2(dy, dx) + ahead;
    this.motes.push({
      x: this.cx + Math.cos(ang) * dist,
      y: this.cy + Math.sin(ang) * dist,
      phase: 0,
      worth: 1,
      hot: false,
    });
  }

  private collect(mote: Mote): void {
    this.combo = this.comboTimer > 0 ? this.combo + 1 : 1;
    this.comboTimer = COLLECT_NOTES_RESET;
    const gain = mote.worth * this.combo;
    this.bumpScore(gain);
    this.collected += 1;
    this.audio.collect(this.combo);
    this.zoomPunch = 1.045;
    this.flash = Math.max(this.flash, 0.2);
    this.shake = Math.max(this.shake, 0.18);
    this.hitStop = 0.03;
    this.burst(mote.x, mote.y, 18, 255, 220, 120, 140);
    const label = this.combo > 1 ? `+${gain}  x${this.combo}` : `+${gain}`;
    this.addFloater(mote.x, mote.y - 12, label, mote.hot ? "#ffd18a" : "#fff4d2");
    this.buzz(10);
    if (this.collected === 1) {
      this.hint = "RELEASE TO FLY";
      this.hintLife = 2.5;
    }
  }

  private bumpScore(n: number): void {
    this.score += n;
  }

  private updateParticles(dt: number): void {
    for (const p of this.particles) {
      if (p.life <= 0) continue;
      p.life -= dt;
      p.vx *= 1 - p.drag * dt;
      p.vy *= 1 - p.drag * dt;
      p.x += p.vx * dt;
      p.y += p.vy * dt;
    }
    if (this.mode !== "death") {
      // Candle embers
      if (Math.random() < 0.6) {
        const a = rand(0, Math.PI * 2);
        const r = rand(0, this.flameR * 0.45);
        this.spawnParticle(
          this.cx + this.flameLeanX + Math.cos(a) * r,
          this.cy + this.flameLeanY + Math.sin(a) * r - this.flameR * 0.15,
          rand(-18, 18),
          rand(-70, -20),
          rand(0.4, 1.1),
          rand(2, 5),
          255,
          rand(140, 210),
          40,
          0.6,
        );
      }
    }
  }

  private updateFloaters(dt: number): void {
    for (let i = this.floaters.length - 1; i >= 0; i--) {
      const f = this.floaters[i]!;
      f.life -= dt;
      f.y -= 28 * dt;
      if (f.life <= 0) this.floaters.splice(i, 1);
    }
  }

  private spawnParticle(
    x: number,
    y: number,
    vx: number,
    vy: number,
    life: number,
    size: number,
    r: number,
    g: number,
    b: number,
    drag: number,
  ): void {
    const p = this.particles[this.particleAt];
    const next: Particle = { x, y, vx, vy, life, max: life, size, r, g, b, drag };
    if (this.particles.length < MAX_PARTICLES) this.particles.push(next);
    else if (p) this.particles[this.particleAt] = next;
    this.particleAt = (this.particleAt + 1) % MAX_PARTICLES;
  }

  private burst(
    x: number,
    y: number,
    n: number,
    r: number,
    g: number,
    b: number,
    speed: number,
    freeze = false,
  ): void {
    for (let i = 0; i < n; i++) {
      const a = rand(0, Math.PI * 2);
      const s = rand(0.2, 1) * speed;
      this.spawnParticle(
        x,
        y,
        Math.cos(a) * s,
        Math.sin(a) * s + (freeze ? 40 : 0),
        rand(0.25, 0.7),
        rand(2, 6),
        r,
        g,
        b,
        freeze ? 2.4 : 1.2,
      );
    }
  }

  private addFloater(x: number, y: number, text: string, color: string): void {
    this.floaters.push({ x, y, text, life: 0.7, color });
    if (this.floaters.length > 8) this.floaters.shift();
  }

  private seedStars(): void {
    this.stars = [];
    const n = 48;
    for (let i = 0; i < n; i++) {
      this.stars.push({
        x: Math.random(),
        y: Math.random(),
        a: rand(0.15, 0.5),
        s: rand(0.6, 1.8),
      });
    }
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
    const sx = this.shake ? (Math.random() - 0.5) * 18 * this.shake : 0;
    const sy = this.shake ? (Math.random() - 0.5) * 18 * this.shake : 0;
    ctx.translate(sx, sy);

    const z = this.zoomPunch;
    ctx.translate(this.cx, this.cy);
    ctx.scale(z, z);
    ctx.translate(-this.cx, -this.cy);

    this.drawBackdrop();
    this.drawLight();
    this.drawBounds();
    this.drawMotes();
    this.drawTrail();
    this.drawSpark();
    this.drawFlame();
    this.drawParticles();
    this.drawFloaters();
    this.drawHud();
    if (this.flash > 0) {
      ctx.fillStyle = `rgba(255, 236, 210, ${this.flash * 0.55})`;
      ctx.fillRect(0, 0, w, h);
    }
    ctx.restore();
  }

  private drawBackdrop(): void {
    const { ctx, w, h } = this;
    const g = ctx.createRadialGradient(
      this.cx,
      this.cy,
      this.flameR,
      this.cx,
      this.cy,
      this.lightR * 1.55,
    );
    g.addColorStop(0, "#2a120c");
    g.addColorStop(0.35, "#14080c");
    g.addColorStop(1, "#07040a");
    ctx.fillStyle = g;
    ctx.fillRect(0, 0, w, h);

    // Cold void beyond the lantern light
    const ice = ctx.createRadialGradient(
      this.cx,
      this.cy,
      this.lightR * 0.92,
      this.cx,
      this.cy,
      Math.max(w, h),
    );
    ice.addColorStop(0, "rgba(0,0,0,0)");
    ice.addColorStop(1, "rgba(30, 70, 120, 0.22)");
    ctx.fillStyle = ice;
    ctx.fillRect(0, 0, w, h);

    ctx.fillStyle = "#dce8ff";
    for (const s of this.stars) {
      ctx.globalAlpha = s.a * (0.6 + 0.4 * Math.sin(this.ambientT * 1.3 + s.x * 12));
      ctx.beginPath();
      ctx.arc(s.x * w, s.y * h, s.s, 0, Math.PI * 2);
      ctx.fill();
    }
    ctx.globalAlpha = 1;
  }

  private drawLight(): void {
    const { ctx } = this;
    const g = ctx.createRadialGradient(
      this.cx,
      this.cy,
      0,
      this.cx,
      this.cy,
      this.lightR,
    );
    const pulse = 0.5 + 0.5 * Math.sin(this.ambientT * 3.2);
    g.addColorStop(0, `rgba(255, 170, 70, ${0.22 + pulse * 0.05})`);
    g.addColorStop(0.45, "rgba(255, 110, 40, 0.07)");
    g.addColorStop(1, "rgba(0,0,0,0)");
    ctx.fillStyle = g;
    ctx.beginPath();
    ctx.arc(this.cx, this.cy, this.lightR, 0, Math.PI * 2);
    ctx.fill();

    if (this.holding && this.mode === "play") {
      ctx.strokeStyle = "rgba(255, 180, 80, 0.18)";
      ctx.lineWidth = 1.5;
      for (let i = 0; i < 3; i++) {
        const t = (this.ambientT * 0.7 + i * 0.33) % 1;
        ctx.beginPath();
        ctx.arc(this.cx, this.cy, lerp(this.lightR, this.flameR * 1.4, t), 0, Math.PI * 2);
        ctx.globalAlpha = 0.35 * (1 - t);
        ctx.stroke();
      }
      ctx.globalAlpha = 1;
    }
  }

  private drawBounds(): void {
    const { ctx } = this;
    ctx.save();
    ctx.setLineDash([6, 10]);
    ctx.strokeStyle = "rgba(255, 92, 40, 0.35)";
    ctx.lineWidth = 2;
    ctx.beginPath();
    ctx.arc(this.cx, this.cy, this.flameR + 8, 0, Math.PI * 2);
    ctx.stroke();
    ctx.strokeStyle = "rgba(140, 200, 255, 0.32)";
    ctx.beginPath();
    ctx.arc(this.cx, this.cy, this.lightR - 6, 0, Math.PI * 2);
    ctx.stroke();
    ctx.restore();
  }

  private drawFlame(): void {
    const { ctx } = this;
    const fx = this.cx + this.flameLeanX * 0.15;
    const fy = this.cy + this.flameLeanY * 0.15;
    const t = this.ambientT;

    // Wick
    ctx.strokeStyle = "#2a1a12";
    ctx.lineWidth = 3;
    ctx.lineCap = "round";
    ctx.beginPath();
    ctx.moveTo(this.cx, this.cy + this.flameR * 0.15);
    ctx.lineTo(this.cx, this.cy + this.flameR * 0.55);
    ctx.stroke();

    ctx.save();
    ctx.globalCompositeOperation = "lighter";
    for (let i = 0; i < 6; i++) {
      const wobble = Math.sin(t * 7 + i) * this.flameR * 0.06;
      const rx = this.flameR * (0.38 + i * 0.09);
      const ry = this.flameR * (0.55 + i * 0.12);
      const grd = ctx.createRadialGradient(fx, fy, 0, fx, fy, ry);
      const alpha = 0.22 - i * 0.028;
      grd.addColorStop(0, `rgba(255, 245, 210, ${alpha + 0.15})`);
      grd.addColorStop(0.35, `rgba(255, 150, 40, ${alpha})`);
      grd.addColorStop(1, "rgba(180, 20, 0, 0)");
      ctx.fillStyle = grd;
      ctx.beginPath();
      ctx.ellipse(fx + wobble, fy - this.flameR * 0.08, rx, ry, wobble * 0.02, 0, Math.PI * 2);
      ctx.fill();
    }
    ctx.fillStyle = "#fff8e0";
    ctx.beginPath();
    ctx.ellipse(fx, fy, this.flameR * 0.16, this.flameR * 0.22, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.restore();
  }

  private drawMotes(): void {
    const { ctx } = this;
    for (const mote of this.motes) {
      const s = 5 + Math.sin(mote.phase) * 1.4;
      ctx.save();
      ctx.translate(mote.x, mote.y);
      ctx.rotate(mote.phase * 0.4);
      ctx.shadowColor = mote.hot ? "#ffb060" : "#ffe9a8";
      ctx.shadowBlur = 12;
      ctx.fillStyle = mote.hot ? "#ffd19a" : "#fff6d2";
      ctx.beginPath();
      ctx.moveTo(0, -s);
      ctx.lineTo(s * 0.7, 0);
      ctx.lineTo(0, s);
      ctx.lineTo(-s * 0.7, 0);
      ctx.closePath();
      ctx.fill();
      ctx.restore();
    }
  }

  private drawTrail(): void {
    const { ctx } = this;
    if (this.trail.length < 2) return;
    ctx.lineCap = "round";
    ctx.lineJoin = "round";
    for (let i = 1; i < this.trail.length; i++) {
      const a = this.trail[i - 1]!;
      const b = this.trail[i]!;
      const u = i / this.trail.length;
      ctx.strokeStyle = this.holding
        ? `rgba(255, 190, 90, ${u * 0.55})`
        : `rgba(160, 220, 255, ${u * 0.5})`;
      ctx.lineWidth = lerp(1, 7, u);
      ctx.beginPath();
      ctx.moveTo(a.x, a.y);
      ctx.lineTo(b.x, b.y);
      ctx.stroke();
    }
  }

  private drawSpark(): void {
    const { ctx } = this;
    const spd = len(this.vx, this.vy);
    const ang = Math.atan2(this.vy, this.vx);
    const streaks = spd > this.minDim() * 0.35;
    const heat = clamp(
      1 - (Math.hypot(this.px - this.cx, this.py - this.cy) - this.flameR) /
        Math.max(1, this.lightR - this.flameR),
      0,
      1,
    );

    if (!this.holding && this.mode === "play" && streaks) {
      ctx.strokeStyle = "rgba(180, 220, 255, 0.18)";
      ctx.lineWidth = 1;
      for (let i = 0; i < 5; i++) {
        const back = 12 + i * 10;
        ctx.beginPath();
        ctx.moveTo(
          this.px - Math.cos(ang) * back + Math.sin(ang) * 6,
          this.py - Math.sin(ang) * back - Math.cos(ang) * 6,
        );
        ctx.lineTo(
          this.px - Math.cos(ang) * (back + 8),
          this.py - Math.sin(ang) * (back + 8),
        );
        ctx.stroke();
      }
    }

    ctx.save();
    ctx.translate(this.px, this.py);
    ctx.rotate(ang);
    ctx.shadowColor = `rgba(${lerp(140, 255, heat)|0}, ${lerp(200, 140, heat)|0}, ${lerp(255, 60, heat)|0}, 0.9)`;
    ctx.shadowBlur = 16;
    ctx.fillStyle = `rgb(${lerp(210, 255, heat)|0}, ${lerp(240, 210, heat)|0}, ${lerp(255, 180, heat)|0})`;
    ctx.beginPath();
    ctx.moveTo(8, 0);
    ctx.lineTo(-6, 4.5);
    ctx.lineTo(-3.5, 0);
    ctx.lineTo(-6, -4.5);
    ctx.closePath();
    ctx.fill();
    ctx.restore();
  }

  private drawParticles(): void {
    const { ctx } = this;
    ctx.save();
    ctx.globalCompositeOperation = "lighter";
    for (const p of this.particles) {
      if (p.life <= 0) continue;
      const u = p.life / p.max;
      ctx.fillStyle = `rgba(${p.r|0},${p.g|0},${p.b|0},${u * 0.85})`;
      ctx.beginPath();
      ctx.arc(p.x, p.y, p.size * u, 0, Math.PI * 2);
      ctx.fill();
    }
    ctx.restore();
  }

  private drawFloaters(): void {
    const { ctx } = this;
    ctx.font = "700 14px system-ui, sans-serif";
    ctx.textAlign = "center";
    for (const f of this.floaters) {
      ctx.globalAlpha = clamp(f.life * 2, 0, 1);
      ctx.fillStyle = f.color;
      ctx.fillText(f.text, f.x, f.y);
    }
    ctx.globalAlpha = 1;
  }

  private drawHud(): void {
    const { ctx, w, h } = this;
    const pad = Math.max(18, w * 0.06);
    const top = Math.max(36, h * 0.06);

    ctx.textAlign = "left";
    ctx.fillStyle = "rgba(255,255,255,0.38)";
    ctx.font = "600 12px system-ui, sans-serif";
    ctx.fillText("BEST  " + this.best, pad, top);
    ctx.textAlign = "right";
    ctx.fillText(this.holding ? "CLINGING" : "DRIFTING", w - pad, top);

    ctx.textAlign = "center";
    ctx.fillStyle = "#fff6e8";
    ctx.font = `800 ${Math.round(this.minDim() * 0.11)}px system-ui, sans-serif`;
    if (this.mode === "play") {
      ctx.fillText(String(this.score), w * 0.5, top + Math.round(this.minDim() * 0.1));
    }

    if (this.hintLife > 0 && this.mode === "play") {
      ctx.globalAlpha = clamp(this.hintLife, 0, 1) * 0.9;
      ctx.font = "700 15px system-ui, sans-serif";
      ctx.fillStyle = "#ffe0b0";
      ctx.fillText(this.hint, w * 0.5, h * 0.82);
      ctx.globalAlpha = 1;
    }

    if (this.mode === "title") {
      ctx.fillStyle = "#fff3d8";
      ctx.font = `800 ${Math.round(this.minDim() * 0.16)}px system-ui, sans-serif`;
      ctx.fillText("W I C K", w * 0.5, h * 0.2);
      ctx.font = "600 14px system-ui, sans-serif";
      ctx.fillStyle = "rgba(255, 214, 160, 0.85)";
      ctx.fillText("HOLD TO ORBIT   ·   RELEASE TO FLY", w * 0.5, h * 0.2 + 28);
      ctx.fillStyle = "rgba(255,255,255,0.55)";
      ctx.font = "600 13px system-ui, sans-serif";
      ctx.fillText("Don't burn. Don't fade.", w * 0.5, h * 0.2 + 50);
      const pulse = 0.55 + 0.45 * Math.sin(this.ambientT * 3);
      ctx.globalAlpha = pulse;
      ctx.font = "700 16px system-ui, sans-serif";
      ctx.fillStyle = "#ffd9a0";
      ctx.fillText("HOLD TO BEGIN", w * 0.5, h * 0.88);
      ctx.globalAlpha = 1;
    }

    if (this.mode === "death") {
      ctx.fillStyle = "rgba(6, 3, 8, 0.28)";
      ctx.fillRect(0, 0, w, h);
      ctx.fillStyle = "#fff3d8";
      ctx.font = `800 ${Math.round(this.minDim() * 0.09)}px system-ui, sans-serif`;
      ctx.fillText(this.deathKind === "burn" ? "BURNED" : "FADED", w * 0.5, h * 0.2);
      ctx.font = `800 ${Math.round(this.minDim() * 0.16)}px system-ui, sans-serif`;
      ctx.fillText(String(this.score), w * 0.5, h * 0.2 + this.minDim() * 0.16);
      ctx.font = "600 14px system-ui, sans-serif";
      ctx.fillStyle = "rgba(255,214,160,0.8)";
      ctx.fillText(
        this.score >= this.best && this.score > 0 ? "NEW BEST" : "BEST  " + this.best,
        w * 0.5,
        h * 0.2 + this.minDim() * 0.2,
      );
      if (this.allowRestart) {
        const pulse = 0.55 + 0.45 * Math.sin(this.ambientT * 3);
        ctx.globalAlpha = pulse;
        ctx.font = "700 16px system-ui, sans-serif";
        ctx.fillStyle = "#ffd9a0";
        ctx.fillText("HOLD TO RETRY", w * 0.5, h * 0.88);
        ctx.globalAlpha = 1;
      }
    }
  }
}
