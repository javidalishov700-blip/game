import type { AudioBus } from "./audio";
import {
  approachTime,
  comboMult,
  gapTime,
  hitScore,
  judge,
  runDifficulty,
  Verdict,
  windowsFor,
} from "./judge";
import { clamp, lerp, rand } from "./math";

type Mode = "title" | "play" | "death";

type Spike = {
  ang: number;
  t: number;
  dur: number;
  resolved: boolean;
};

type Shard = {
  x: number;
  y: number;
  vx: number;
  vy: number;
  rot: number;
  vr: number;
  life: number;
  max: number;
  len: number;
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
};

type Floater = {
  x: number;
  y: number;
  text: string;
  life: number;
  color: string;
  scale: number;
};

const STORAGE_KEY = "flinch-best";
const MAX_PARTICLES = 160;

export class FlinchGame {
  readonly canvas: HTMLCanvasElement;
  private readonly ctx: CanvasRenderingContext2D;
  private readonly audio: AudioBus;

  private w = 390;
  private h = 844;
  private lastT = 0;
  private time = 0;
  private mode: Mode = "title";

  private spike: Spike | null = null;
  private gap = 0;
  private score = 0;
  private best = 0;
  private streak = 0;
  private cleared = 0;
  private taught = 0;
  private hitStop = 0;
  private deathAge = 0;
  private allowRetry = false;
  private shield = 0;
  private pulse = 0;

  private shards: Shard[] = [];
  private particles: Particle[] = [];
  private particleAt = 0;
  private floaters: Floater[] = [];
  private shake = 0;
  private flash = 0;

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
  }

  skipClock(): void {
    this.lastT = 0;
  }

  tap(x: number, y: number): void {
    this.audio.unlock();
    if (x > this.w - 56 && y < 52) {
      this.audio.toggleMute();
      return;
    }
    if (this.mode === "title") {
      this.startRun();
      return;
    }
    if (this.mode === "death") {
      if (this.allowRetry) this.startRun();
      return;
    }
    this.tryParry();
  }

  tick(nowMs: number): void {
    if (!this.lastT) this.lastT = nowMs;
    let dt = (nowMs - this.lastT) / 1000;
    this.lastT = nowMs;
    if (dt > 0.05) dt = 0.05;
    const raw = dt;

    if (this.mode === "death") {
      this.deathAge += raw;
      if (this.deathAge > 0.55) this.allowRetry = true;
      dt *= this.deathAge < 0.85 ? 0.14 : 0;
    }
    if (this.hitStop > 0) {
      this.hitStop -= raw;
      dt *= 0.08;
    }

    this.time += dt;
    this.shake = Math.max(0, this.shake - raw * 7);
    this.flash = Math.max(0, this.flash - raw * 3.4);
    this.shield = Math.max(0, this.shield - raw * 3.5);
    this.pulse = Math.max(0, this.pulse - raw * 2.2);

    this.updateSpike(dt);
    this.updateShards(dt);
    this.updateParticles(dt);
    this.updateFloaters(dt);
    this.draw();
  }

  private startRun(): void {
    this.mode = "play";
    this.score = 0;
    this.streak = 0;
    this.cleared = 0;
    this.taught = 0;
    this.deathAge = 0;
    this.allowRetry = false;
    this.spike = null;
    this.gap = 0.35;
    this.shards = [];
    this.flash = 0.2;
    this.audio.setApproach(false);
  }

  private difficulty(): number {
    return runDifficulty(this.cleared, this.score);
  }

  private spawnSpike(): void {
    const d = this.difficulty();
    this.spike = {
      ang: rand(0, Math.PI * 2),
      t: 0,
      dur: approachTime(d),
      resolved: false,
    };
  }

  private tryParry(): void {
    if (!this.spike || this.spike.resolved) return;
    const p = this.spike.t / this.spike.dur;
    const verdict = judge(p, windowsFor(this.difficulty()));
    this.resolve(verdict);
  }

  private resolve(verdict: Verdict): void {
    if (!this.spike || this.spike.resolved) return;
    this.spike.resolved = true;
    this.audio.setApproach(false);
    this.shield = 1;
    this.taught += 1;

    if (verdict === "late") {
      this.die();
      return;
    }
    if (verdict === "early") {
      this.streak = 0;
      this.audio.flinch();
      this.addFloater("FLINCH", "#8a8a90", 0.9);
      this.shake = 0.18;
      this.cleared += 1;
      this.gap = gapTime(this.difficulty());
      return;
    }
    if (verdict === "good") {
      this.streak = 0;
      const gain = hitScore("good", 0);
      this.score += gain;
      this.audio.good();
      this.addFloater(`+${gain}`, "#d8d8de", 0.85);
      this.cleared += 1;
      this.gap = gapTime(this.difficulty());
      this.pulse = 0.4;
      return;
    }

    this.streak += 1;
    const mult = comboMult(this.streak);
    const gain = hitScore("perfect", this.streak);
    this.score += gain;
    this.audio.perfect(mult);
    this.hitStop = 0.045 + Math.min(0.04, this.streak * 0.004);
    this.shake = 0.22 + Math.min(0.25, this.streak * 0.03);
    this.flash = 0.28;
    this.pulse = 1;
    this.burstRing();
    this.addFloater(`${mult}x`, "#f0c75e", 1.15 + Math.min(0.4, this.streak * 0.06));
    this.addFloater(`+${gain}`, "#fff6d2", 0.8);
    this.cleared += 1;
    this.gap = gapTime(this.difficulty()) * 0.85;
    this.buzz(10);
  }

  private die(): void {
    this.mode = "death";
    this.deathAge = 0;
    this.allowRetry = false;
    this.audio.shatter();
    this.shake = 1;
    this.flash = 0.55;
    this.spawnShards();
    if (this.score > this.best) {
      this.best = this.score;
      writeBest(this.best);
    }
    this.buzz(40);
  }

  private updateSpike(dt: number): void {
    if (this.mode === "title") return;

    if (!this.spike) {
      if (this.mode !== "play") return;
      this.gap -= dt;
      if (this.gap <= 0) this.spawnSpike();
      return;
    }

    if (this.spike.resolved && this.mode === "play") {
      this.spike.t -= dt * 2.8;
      if (this.spike.t <= 0) this.spike = null;
      return;
    }

    this.spike.t += dt;
    const p = this.spike.t / this.spike.dur;
    this.audio.setApproach(this.mode === "play" && !this.spike.resolved, p);

    if (!this.spike.resolved && p >= 1) this.resolve("late");
  }

  private cx(): number {
    return this.w * 0.5;
  }
  private cy(): number {
    return this.h * 0.5;
  }
  private reach(): number {
    return Math.min(this.w, this.h) * 0.42;
  }
  private coreR(): number {
    return Math.min(this.w, this.h) * 0.072;
  }

  private spikePos(progress: number): { x: number; y: number } {
    const s = this.spike;
    if (!s) return { x: this.cx(), y: this.cy() };
    const dist = lerp(this.reach(), this.coreR() * 0.2, clamp(progress, 0, 1.15));
    return {
      x: this.cx() + Math.cos(s.ang) * dist,
      y: this.cy() + Math.sin(s.ang) * dist,
    };
  }

  private spawnShards(): void {
    const cx = this.cx();
    const cy = this.cy();
    this.shards = [];
    for (let i = 0; i < 18; i++) {
      const a = rand(0, Math.PI * 2);
      const s = rand(80, 420);
      this.shards.push({
        x: cx,
        y: cy,
        vx: Math.cos(a) * s,
        vy: Math.sin(a) * s,
        rot: rand(0, Math.PI),
        vr: rand(-8, 8),
        life: rand(0.7, 1.3),
        max: 1.3,
        len: rand(8, 22),
      });
    }
    this.burst(cx, cy, 40, 240, 210, 160, 360);
  }

  private burstRing(): void {
    const cx = this.cx();
    const cy = this.cy();
    this.burst(cx, cy, 22, 240, 196, 80, 280);
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
      const s = rand(0.2, 1) * speed;
      const next: Particle = {
        x,
        y,
        vx: Math.cos(a) * s,
        vy: Math.sin(a) * s,
        life: rand(0.22, 0.55),
        max: 0.55,
        size: rand(2, 6),
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

  private updateShards(dt: number): void {
    for (const s of this.shards) {
      if (s.life <= 0) continue;
      s.life -= dt;
      s.x += s.vx * dt;
      s.y += s.vy * dt;
      s.vx *= 0.99;
      s.vy += 90 * dt;
      s.rot += s.vr * dt;
    }
  }

  private updateParticles(dt: number): void {
    for (const p of this.particles) {
      if (p.life <= 0) continue;
      p.life -= dt;
      p.x += p.vx * dt;
      p.y += p.vy * dt;
    }
  }

  private updateFloaters(dt: number): void {
    for (let i = this.floaters.length - 1; i >= 0; i--) {
      const f = this.floaters[i]!;
      f.life -= dt;
      f.y -= 40 * dt;
      if (f.life <= 0) this.floaters.splice(i, 1);
    }
  }

  private addFloater(text: string, color: string, scale: number): void {
    this.floaters.push({
      x: this.cx(),
      y: this.cy() - this.coreR() * 2.2,
      text,
      life: 0.7,
      color,
      scale,
    });
    if (this.floaters.length > 6) this.floaters.shift();
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
      ctx.translate(
        (Math.random() - 0.5) * 16 * this.shake,
        (Math.random() - 0.5) * 16 * this.shake,
      );
    }

    ctx.fillStyle = "#09090b";
    ctx.fillRect(0, 0, w, h);
    const g = ctx.createRadialGradient(this.cx(), this.cy(), 20, this.cx(), this.cy(), Math.max(w, h) * 0.7);
    g.addColorStop(0, "#16141c");
    g.addColorStop(1, "#09090b");
    ctx.fillStyle = g;
    ctx.fillRect(0, 0, w, h);

    this.drawCore();
    this.drawSpike();
    this.drawShards();
    this.drawParticles();
    this.drawFloaters();
    this.drawHud();
    if (this.mode === "title") this.drawTitle();
    if (this.mode === "death") this.drawDeath();
    if (this.flash > 0) {
      ctx.fillStyle = `rgba(255, 236, 180, ${this.flash * 0.45})`;
      ctx.fillRect(0, 0, w, h);
    }
    ctx.restore();
  }

  private drawCore(): void {
    const { ctx } = this;
    const cx = this.cx();
    const cy = this.cy();
    const r = this.coreR() * (1 + this.pulse * 0.12);
    if (this.shield > 0) {
      ctx.strokeStyle = `rgba(240, 199, 94, ${this.shield * 0.85})`;
      ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.arc(cx, cy, r * 1.55, 0, Math.PI * 2);
      ctx.stroke();
    }
    const s = this.spike;
    if (s && this.mode === "play" && !s.resolved) {
      const p = s.t / s.dur;
      const w = windowsFor(this.difficulty());
      if (p >= w.perfect) {
        ctx.strokeStyle = "rgba(240, 199, 94, 0.95)";
        ctx.lineWidth = 4;
        ctx.beginPath();
        ctx.arc(cx, cy, r * 1.85, 0, Math.PI * 2);
        ctx.stroke();
      }
    }
    ctx.save();
    ctx.translate(cx, cy);
    ctx.rotate(Math.PI / 4);
    const cracked = this.mode === "death";
    ctx.fillStyle = cracked ? "#6a6460" : "#f4f0e6";
    ctx.strokeStyle = cracked ? "#2a2420" : "#c4b89a";
    ctx.lineWidth = 2;
    ctx.beginPath();
    ctx.rect(-r, -r, r * 2, r * 2);
    ctx.fill();
    ctx.stroke();
    if (!cracked) {
      ctx.fillStyle = "rgba(255,255,255,0.45)";
      ctx.fillRect(-r * 0.7, -r * 0.7, r * 0.9, r * 0.35);
    }
    ctx.restore();
  }

  private drawSpike(): void {
    const s = this.spike;
    if (!s) return;
    const { ctx } = this;
    const p = s.t / s.dur;
    const pos = this.spikePos(p);
    const w = windowsFor(this.difficulty());
    const gold = p >= w.perfect && !s.resolved && this.mode === "play";
    const live = p >= w.good && !s.resolved && this.mode === "play";

    const trail = this.spikePos(Math.max(0, p - 0.08));
    ctx.strokeStyle = gold
      ? "rgba(240, 199, 94, 0.55)"
      : live
        ? "rgba(230, 230, 236, 0.28)"
        : "rgba(180, 180, 190, 0.16)";
    ctx.lineWidth = gold ? 5 : 3;
    ctx.beginPath();
    ctx.moveTo(trail.x, trail.y);
    ctx.lineTo(pos.x, pos.y);
    ctx.stroke();

    ctx.save();
    ctx.translate(pos.x, pos.y);
    ctx.rotate(s.ang);
    ctx.fillStyle = gold ? "#f0c75e" : live ? "#f2f2f6" : "#9a9aa4";
    ctx.beginPath();
    ctx.moveTo(18, 0);
    ctx.lineTo(-14, 6);
    ctx.lineTo(-8, 0);
    ctx.lineTo(-14, -6);
    ctx.closePath();
    ctx.fill();
    ctx.restore();
  }

  private drawShards(): void {
    const { ctx } = this;
    for (const s of this.shards) {
      if (s.life <= 0) continue;
      ctx.save();
      ctx.globalAlpha = clamp(s.life / s.max, 0, 1);
      ctx.translate(s.x, s.y);
      ctx.rotate(s.rot);
      ctx.fillStyle = "#e8e0d0";
      ctx.fillRect(-s.len * 0.5, -2, s.len, 4);
      ctx.restore();
    }
    ctx.globalAlpha = 1;
  }

  private drawParticles(): void {
    const { ctx } = this;
    ctx.save();
    ctx.globalCompositeOperation = "lighter";
    for (const p of this.particles) {
      if (p.life <= 0) continue;
      const u = p.life / p.max;
      ctx.fillStyle = `rgba(${p.r|0},${p.g|0},${p.b|0},${u * 0.9})`;
      ctx.beginPath();
      ctx.arc(p.x, p.y, p.size * u, 0, Math.PI * 2);
      ctx.fill();
    }
    ctx.restore();
  }

  private drawFloaters(): void {
    const { ctx } = this;
    ctx.textAlign = "center";
    for (const f of this.floaters) {
      ctx.globalAlpha = clamp(f.life * 2, 0, 1);
      ctx.fillStyle = f.color;
      ctx.font = `800 ${Math.round(28 * f.scale)}px system-ui, sans-serif`;
      ctx.fillText(f.text, f.x, f.y);
    }
    ctx.globalAlpha = 1;
  }

  private drawHud(): void {
    const { ctx, w } = this;
    ctx.fillStyle = "rgba(255,255,255,0.35)";
    ctx.font = "600 12px system-ui, sans-serif";
    ctx.textAlign = "left";
    ctx.fillText("BEST  " + this.best, 18, 28);
    ctx.textAlign = "right";
    ctx.fillText(this.audio.isMuted ? "MUTE" : "AUDIO", w - 18, 28);

    if (this.mode === "play" || this.mode === "death") {
      ctx.textAlign = "center";
      ctx.fillStyle = "#f4f0e6";
      ctx.font = "800 36px system-ui, sans-serif";
      ctx.fillText(String(this.score), w * 0.5, 44);
      if (this.streak > 0 && this.mode === "play") {
        ctx.fillStyle = "#f0c75e";
        ctx.font = "800 14px system-ui, sans-serif";
        ctx.fillText(comboMult(this.streak) + "x", w * 0.5, 64);
      }
    }

    if (this.mode === "play" && this.taught < 3) {
      ctx.fillStyle = "rgba(240, 199, 94, 0.7)";
      ctx.font = "600 13px system-ui, sans-serif";
      ctx.textAlign = "center";
      ctx.fillText("WAIT FOR GOLD", w * 0.5, this.h - 28);
    }
  }

  private drawTitle(): void {
    const { ctx, w, h } = this;
    ctx.textAlign = "center";
    ctx.fillStyle = "#f4f0e6";
    ctx.font = "800 44px system-ui, sans-serif";
    ctx.fillText("FLINCH", w * 0.5, h * 0.22);
    ctx.fillStyle = "rgba(240, 199, 94, 0.85)";
    ctx.font = "600 14px system-ui, sans-serif";
    ctx.fillText("Wait. Then tap.", w * 0.5, h * 0.22 + 28);
    ctx.globalAlpha = 0.5 + 0.5 * Math.sin(this.time * 3.2);
    ctx.fillStyle = "#f4f0e6";
    ctx.font = "700 15px system-ui, sans-serif";
    ctx.fillText("TAP TO PLAY", w * 0.5, h * 0.86);
    ctx.globalAlpha = 1;
  }

  private drawDeath(): void {
    const { ctx, w, h } = this;
    ctx.textAlign = "center";
    ctx.fillStyle = "#f4f0e6";
    ctx.font = "800 22px system-ui, sans-serif";
    ctx.fillText("TOO LATE", w * 0.5, h * 0.2);
    if (this.allowRetry) {
      ctx.globalAlpha = 0.5 + 0.5 * Math.sin(this.time * 3.2);
      ctx.font = "700 15px system-ui, sans-serif";
      ctx.fillText("TAP TO RETRY", w * 0.5, h * 0.86);
      ctx.globalAlpha = 1;
    }
  }
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
