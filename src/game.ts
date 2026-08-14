import type { AudioBus } from "./audio";
import {
  approachTime,
  comboMult,
  judge,
  runDifficulty,
  type Verdict,
  windowsFor,
} from "./judge";
import { clamp, lerp } from "./math";
import {
  LANES,
  LIVES,
  award,
  buildWave,
  laneAngle,
  laneFromPoint,
  liveLane,
  shortestArc,
  waveBonus,
  waveHint,
  waveTitle,
  type Spawn,
  type SpikeKind,
} from "./rules";

export type Mode = "idle" | "play" | "pause" | "over";

export type Hud = {
  score: number;
  best: number;
  lives: number;
  wave: number;
  streak: number;
  combo: number;
  title: string;
};

export type GameHooks = {
  onMode: (mode: Mode) => void;
  onHud: (hud: Hud) => void;
};

type Spike = Spawn & {
  id: number;
  t: number;
  dur: number;
  dead: boolean;
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
  text: string;
  color: string;
  life: number;
  max: number;
  scale: number;
};

const BEST_KEY = "flinch-best";
const MAX_PARTICLES = 180;

function readBest(): number {
  try {
    return Number(localStorage.getItem(BEST_KEY) || 0);
  } catch {
    return 0;
  }
}

function writeBest(n: number): void {
  try {
    localStorage.setItem(BEST_KEY, String(n));
  } catch {
    /* ignore */
  }
}

function smooth(t: number): number {
  return t * t * (3 - 2 * t);
}

export class FlinchGame {
  readonly canvas: HTMLCanvasElement;
  private readonly ctx: CanvasRenderingContext2D;
  private readonly audio: AudioBus;
  private readonly hooks: GameHooks;

  private w = 390;
  private h = 844;
  private lastT = 0;
  private time = 0;
  private mode: Mode = "idle";

  private spikes: Spike[] = [];
  private queue: Spawn[] = [];
  private nextId = 1;
  private spawnWait = 0;
  private waveHold = false;
  private waveHoldT = 0;

  private score = 0;
  private best = 0;
  private streak = 0;
  private lives = LIVES;
  private wave = 1;
  private cleared = 0;

  private hitStop = 0;
  private deathAge = 0;
  private pulse = 0;
  private shake = 0;
  private flash = 0;
  private hurtFlash = 0;
  private goldFlash = 0;
  private tapLane: number | null = null;
  private tapLaneT = 0;
  private ringSpin = 0;
  private shards: Shard[] = [];
  private particles: Particle[] = [];
  private floaters: Floater[] = [];

  constructor(canvas: HTMLCanvasElement, audio: AudioBus, hooks: GameHooks) {
    this.canvas = canvas;
    const ctx = canvas.getContext("2d", { alpha: false });
    if (!ctx) throw new Error("Canvas 2D is unavailable");
    this.ctx = ctx;
    this.audio = audio;
    this.hooks = hooks;
    this.best = readBest();
    this.pushHud();
  }

  getMode(): Mode {
    return this.mode;
  }

  getHud(): Hud {
    return {
      score: this.score,
      best: this.best,
      lives: this.lives,
      wave: this.wave,
      streak: this.streak,
      combo: comboMult(this.streak),
      title: waveTitle(this.wave),
    };
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

  startRun(): void {
    this.mode = "play";
    this.score = 0;
    this.streak = 0;
    this.lives = LIVES;
    this.wave = 1;
    this.cleared = 0;
    this.spikes = [];
    this.shards = [];
    this.particles = [];
    this.floaters = [];
    this.nextId = 1;
    this.deathAge = 0;
    this.hitStop = 0;
    this.shake = 0;
    this.flash = 0.2;
    this.hurtFlash = 0;
    this.goldFlash = 0;
    this.waveHold = false;
    this.audio.startBgm();
    this.beginWave(1);
    this.setMode("play");
    this.pushHud();
  }

  pause(): void {
    if (this.mode !== "play") return;
    this.audio.setApproach(false);
    this.setMode("pause");
  }

  resume(): void {
    if (this.mode !== "pause") return;
    this.lastT = 0;
    this.setMode("play");
  }

  goHome(): void {
    this.spikes = [];
    this.queue = [];
    this.shards = [];
    this.particles = [];
    this.floaters = [];
    this.audio.stopBgm();
    this.audio.setApproach(false);
    this.setMode("idle");
    this.pushHud();
  }

  replay(): void {
    this.startRun();
  }

  tap(x: number, y: number): void {
    this.audio.unlock();
    if (this.mode !== "play") return;
    const lane = laneFromPoint(x, y, this.cx(), this.cy());
    this.tapLane = lane;
    this.tapLaneT = 0.14;
    const hit = this.pickSpike(lane);
    if (!hit) {
      this.audio.tapUi();
      return;
    }
    this.resolveTap(hit);
  }

  tapLaneIndex(lane: number): void {
    this.audio.unlock();
    if (this.mode !== "play") return;
    this.tapLane = lane;
    this.tapLaneT = 0.14;
    const hit = this.pickSpike(lane);
    if (!hit) {
      this.audio.tapUi();
      return;
    }
    this.resolveTap(hit);
  }

  tick(nowMs: number): void {
    if (!this.lastT) this.lastT = nowMs;
    let dt = (nowMs - this.lastT) / 1000;
    this.lastT = nowMs;
    if (dt > 0.05) dt = 0.05;
    const raw = dt;

    if (this.mode === "over") {
      this.deathAge += raw;
      dt *= this.deathAge < 0.9 ? 0.16 : 0;
    } else if (this.mode === "pause") {
      dt = 0;
    } else if (this.mode === "idle") {
      dt *= 0.35;
    }

    if (this.hitStop > 0) {
      this.hitStop -= raw;
      dt *= 0.08;
    }

    this.time += dt;
    this.shake = Math.max(0, this.shake - raw * 7);
    this.flash = Math.max(0, this.flash - raw * 3.4);
    this.hurtFlash = Math.max(0, this.hurtFlash - raw * 2.8);
    this.goldFlash = Math.max(0, this.goldFlash - raw * 3.2);
    this.pulse = Math.max(0, this.pulse - raw * 2.2);
    this.tapLaneT = Math.max(0, this.tapLaneT - raw);
    this.ringSpin += raw * (0.1 + this.streak * 0.03);

    if (this.mode === "play") this.updatePlay(dt);
    this.updateShards(dt);
    this.updateParticles(dt);
    this.updateFloaters(dt);
    this.updateApproach();
    this.draw();
  }

  private setMode(mode: Mode): void {
    this.mode = mode;
    this.hooks.onMode(mode);
  }

  private pushHud(): void {
    this.hooks.onHud(this.getHud());
  }

  private difficulty(): number {
    return runDifficulty(this.cleared, this.score);
  }

  private beginWave(n: number): void {
    this.wave = n;
    this.queue = buildWave(n);
    this.spawnWait = 0.45;
    this.waveHold = false;
    this.waveHoldT = 0;
    this.pulse = 1;
    this.audio.wave();
    this.addFloater(`WAVE ${n}  ${waveTitle(n)}`, "#e8c36a", 1.05);
    this.addFloater(waveHint(n), "#9a9386", 0.78);
    this.pushHud();
  }

  private updatePlay(dt: number): void {
    if (this.waveHold) {
      this.waveHoldT -= dt;
      if (this.waveHoldT <= 0) this.beginWave(this.wave + 1);
    } else {
      this.spawnWait -= dt;
      if (this.spawnWait <= 0 && this.queue.length > 0) {
        const spec = this.queue.shift()!;
        this.spawn(spec);
        this.spawnWait = spec.gap;
      }
    }

    for (const s of this.spikes) {
      if (s.dead) continue;
      s.t += dt;
      const p = s.t / s.dur;
      if (p >= 1) this.resolveImpact(s);
    }

    this.spikes = this.spikes.filter((s) => !s.dead);
    this.checkWaveClear();
  }

  private spawn(spec: Spawn): void {
    const d = this.difficulty();
    this.spikes.push({
      ...spec,
      id: this.nextId++,
      t: 0,
      dur: approachTime(d),
      dead: false,
    });
  }

  private pickSpike(lane: number): Spike | null {
    let best: Spike | null = null;
    let bestDist = 99;
    for (const s of this.spikes) {
      if (s.dead) continue;
      const p = s.t / s.dur;
      if (liveLane(s, p) !== lane) continue;
      if (p < 0.12 || p > 1.05) continue;
      const dist = Math.abs(p - 0.82);
      if (dist < bestDist) {
        best = s;
        bestDist = dist;
      }
    }
    return best;
  }

  private resolveTap(s: Spike): void {
    const p = s.t / s.dur;
    const v = judge(p, windowsFor(this.difficulty()));
    s.dead = true;

    if (s.kind === "ghost") {
      this.hurt("GHOST");
      this.burst(this.spikeXY(s), 16, 140, 160, 190, 220);
      return;
    }
    if (v === "early") {
      this.flinch();
      this.burst(this.spikeXY(s), 14, 201, 162, 39, 200);
      return;
    }
    if (v === "late") {
      this.hurt("LATE");
      this.burst(this.spikeXY(s), 16, 196, 90, 74, 240);
      return;
    }
    this.land(s, v);
  }

  private resolveImpact(s: Spike): void {
    s.dead = true;
    if (s.kind === "ghost") {
      this.streak += 1;
      const gain = award("late", "ghost", this.streak, this.wave);
      this.score += gain;
      this.cleared += 1;
      this.goldFlash = 0.45;
      this.addFloater(`PASS  +${gain}`, "#8b9bb4", 0.9);
      this.audio.ghost();
      this.audio.vibrate(8);
      this.saveBest();
      this.pushHud();
      return;
    }
    this.hurt("HIT");
    this.burst({ x: this.cx(), y: this.cy() }, 22, 196, 90, 74, 280);
  }

  private land(s: Spike, v: Verdict): void {
    if (v === "perfect") this.streak += 1;
    else this.streak = Math.max(0, this.streak);
    const gain = award(v, s.kind, this.streak, this.wave);
    this.score += gain;
    this.cleared += 1;
    this.goldFlash = 1;
    this.pulse = 1;
    this.shake = v === "perfect" ? 0.28 : 0.14;
    if (v === "perfect") {
      this.hitStop = 0.04 + Math.min(0.04, this.streak * 0.004);
      this.flash = 0.22;
      this.audio.perfect(comboMult(this.streak));
      this.audio.vibrate(10);
      this.addFloater(`${comboMult(this.streak)}x`, "#f0c75e", 1.2);
    } else {
      this.audio.good();
    }
    this.addFloater(
      v === "perfect" ? `PERFECT  +${gain}` : `GOOD  +${gain}`,
      v === "perfect" ? "#fff6d2" : "#d8c89a",
      0.88,
    );
    this.burst(
      this.spikeXY(s),
      v === "perfect" ? 20 : 12,
      240,
      200,
      110,
      260,
    );
    this.saveBest();
    this.pushHud();
  }

  private flinch(): void {
    this.streak = 0;
    this.cleared += 1;
    this.flash = 0.4;
    this.shake = 0.2;
    this.addFloater("FLINCH", "#c9a227", 0.95);
    this.audio.flinch();
    this.audio.vibrate(6);
    this.pushHud();
  }

  private hurt(why: string): void {
    this.lives -= 1;
    this.streak = 0;
    this.hurtFlash = 1;
    this.shake = 0.55;
    this.addFloater(why, "#e07060", 1);
    this.audio.hurt();
    this.audio.vibrate(24);
    this.pushHud();
    if (this.lives <= 0) {
      this.die();
      return;
    }
  }

  private checkWaveClear(): void {
    if (this.waveHold || this.mode !== "play") return;
    const live = this.spikes.some((s) => !s.dead);
    if (live || this.queue.length > 0) return;
    this.waveHold = true;
    this.waveHoldT = 0.9;
    const bonus = waveBonus(this.wave);
    this.score += bonus;
    this.addFloater(`CLEAR  +${bonus}`, "#7fd4a8", 1.05);
    this.saveBest();
    this.pushHud();
  }

  private die(): void {
    this.audio.setApproach(false);
    this.audio.stopBgm();
    this.audio.shatter();
    this.audio.vibrate(40);
    this.deathAge = 0;
    this.shake = 1;
    this.flash = 0.55;
    this.spawnShards();
    this.saveBest();
    this.setMode("over");
    this.pushHud();
  }

  private saveBest(): void {
    if (this.score > this.best) {
      this.best = this.score;
      writeBest(this.best);
    }
  }

  private updateApproach(): void {
    if (this.mode !== "play") {
      this.audio.setApproach(false);
      return;
    }
    let urgency = 0;
    let on = false;
    for (const s of this.spikes) {
      if (s.dead || s.kind === "ghost") continue;
      const p = s.t / s.dur;
      if (p > 0.2 && p < 1) {
        on = true;
        urgency = Math.max(urgency, p);
      }
    }
    this.audio.setApproach(on, urgency);
  }

  private cx(): number {
    return this.w * 0.5;
  }

  private cy(): number {
    return this.h * 0.52;
  }

  private reach(): number {
    return Math.min(this.w, this.h) * 0.38;
  }

  private coreR(): number {
    return Math.min(this.w, this.h) * 0.07;
  }

  private spikeAngle(s: Spike, p: number): number {
    const a0 = laneAngle(s.lane);
    if (s.feintTo == null || s.feintAt == null) return a0;
    const t = smooth(clamp((p - (s.feintAt - 0.14)) / 0.22, 0, 1));
    return a0 + shortestArc(a0, laneAngle(s.feintTo)) * t;
  }

  private spikeXY(s: Spike): { x: number; y: number } {
    const p = clamp(s.t / s.dur, 0, 1.08);
    const a = this.spikeAngle(s, p);
    const dist = lerp(this.reach(), this.coreR() * 0.15, p);
    return {
      x: this.cx() + Math.cos(a) * dist,
      y: this.cy() + Math.sin(a) * dist,
    };
  }

  private addFloater(text: string, color: string, scale: number): void {
    this.floaters.push({ text, color, life: 0.85, max: 0.85, scale });
    if (this.floaters.length > 6) this.floaters.shift();
  }

  private burst(
    pos: { x: number; y: number },
    n: number,
    r: number,
    g: number,
    b: number,
    speed: number,
  ): void {
    for (let i = 0; i < n; i++) {
      const a = Math.random() * Math.PI * 2;
      const sp = speed * (0.25 + Math.random() * 0.85);
      this.particles.push({
        x: pos.x,
        y: pos.y,
        vx: Math.cos(a) * sp,
        vy: Math.sin(a) * sp,
        life: 0.28 + Math.random() * 0.4,
        max: 0.7,
        size: 1.2 + Math.random() * 2.6,
        r,
        g,
        b,
      });
    }
    if (this.particles.length > MAX_PARTICLES) {
      this.particles.splice(0, this.particles.length - MAX_PARTICLES);
    }
  }

  private spawnShards(): void {
    const cx = this.cx();
    const cy = this.cy();
    this.shards = [];
    for (let i = 0; i < 18; i++) {
      const a = Math.random() * Math.PI * 2;
      const s = 80 + Math.random() * 340;
      this.shards.push({
        x: cx,
        y: cy,
        vx: Math.cos(a) * s,
        vy: Math.sin(a) * s,
        rot: Math.random() * Math.PI,
        vr: (Math.random() - 0.5) * 16,
        life: 0.7 + Math.random() * 0.6,
        max: 1.3,
        len: 8 + Math.random() * 14,
      });
    }
    this.burst({ x: cx, y: cy }, 36, 240, 210, 160, 360);
  }

  private updateShards(dt: number): void {
    for (const s of this.shards) {
      s.x += s.vx * dt;
      s.y += s.vy * dt;
      s.vy += 420 * dt;
      s.rot += s.vr * dt;
      s.life -= dt;
    }
    this.shards = this.shards.filter((s) => s.life > 0);
  }

  private updateParticles(dt: number): void {
    for (const p of this.particles) {
      p.x += p.vx * dt;
      p.y += p.vy * dt;
      p.vy += 90 * dt;
      p.life -= dt;
    }
    this.particles = this.particles.filter((p) => p.life > 0);
  }

  private updateFloaters(dt: number): void {
    for (const f of this.floaters) f.life -= dt;
    this.floaters = this.floaters.filter((f) => f.life > 0);
  }

  private draw(): void {
    const ctx = this.ctx;
    const W = this.w;
    const H = this.h;
    const S = Math.min(W, H);

    ctx.fillStyle = "#09090b";
    ctx.fillRect(0, 0, W, H);
    const g = ctx.createRadialGradient(this.cx(), this.cy() - S * 0.08, 16, this.cx(), this.cy(), S * 0.82);
    g.addColorStop(0, "#16140f");
    g.addColorStop(1, "#070705");
    ctx.fillStyle = g;
    ctx.fillRect(0, 0, W, H);

    ctx.save();
    if (this.shake > 0) {
      ctx.translate((Math.random() - 0.5) * this.shake * 18, (Math.random() - 0.5) * this.shake * 18);
    }

    this.drawLanes(S);
    this.drawRing(S);
    this.drawSpikes(S);
    this.drawCore(S);
    this.drawShards();
    this.drawParticles();
    this.drawFloaters(S);

    if (this.flash > 0) {
      ctx.fillStyle = `rgba(201,162,39,${this.flash * 0.16})`;
      ctx.fillRect(-20, -20, W + 40, H + 40);
    }
    if (this.hurtFlash > 0) {
      ctx.fillStyle = `rgba(180,50,40,${this.hurtFlash * 0.15})`;
      ctx.fillRect(-20, -20, W + 40, H + 40);
    }
    ctx.restore();
  }

  private drawLanes(S: number): void {
    const ctx = this.ctx;
    const x = this.cx();
    const y = this.cy();
    const R = this.reach() * 1.12;
    for (let i = 0; i < LANES; i++) {
      const a0 = laneAngle(i) - Math.PI / LANES;
      const a1 = laneAngle(i) + Math.PI / LANES;
      ctx.beginPath();
      ctx.moveTo(x, y);
      ctx.arc(x, y, R, a0, a1);
      ctx.closePath();
      const hot = this.tapLane === i && this.tapLaneT > 0;
      ctx.fillStyle = hot
        ? "rgba(232,195,106,0.12)"
        : i % 2 === 0
          ? "rgba(255,255,255,0.018)"
          : "rgba(0,0,0,0.1)";
      ctx.fill();
      ctx.strokeStyle = "rgba(232,195,106,0.07)";
      ctx.lineWidth = 1;
      ctx.beginPath();
      ctx.moveTo(x, y);
      ctx.lineTo(x + Math.cos(a0) * R, y + Math.sin(a0) * R);
      ctx.stroke();
    }
    void S;
  }

  private drawRing(S: number): void {
    const ctx = this.ctx;
    const x = this.cx();
    const y = this.cy();
    ctx.save();
    ctx.translate(x, y);
    ctx.rotate(this.ringSpin);
    ctx.strokeStyle = "rgba(232,195,106,0.14)";
    ctx.lineWidth = 1;
    ctx.setLineDash([4, 10]);
    ctx.beginPath();
    ctx.arc(0, 0, this.reach(), 0, Math.PI * 2);
    ctx.stroke();
    ctx.setLineDash([]);
    ctx.restore();

    const win = windowsFor(this.difficulty());
    const goldR = lerp(this.reach(), this.coreR() * 0.15, win.perfect);
    ctx.beginPath();
    ctx.arc(x, y, goldR, 0, Math.PI * 2);
    ctx.strokeStyle = `rgba(232,195,106,${0.2 + this.goldFlash * 0.5})`;
    ctx.lineWidth = 1.5 + this.goldFlash * 2.2;
    ctx.stroke();
    void S;
  }

  private drawSpikes(S: number): void {
    const ctx = this.ctx;
    const win = windowsFor(this.difficulty());
    for (const s of this.spikes) {
      if (s.dead) continue;
      const p = s.t / s.dur;
      const pos = this.spikeXY(s);
      const a = this.spikeAngle(s, p);
      const v = judge(p, win);
      const gold = v === "good" || v === "perfect";
      const size = S * (0.032 + (gold ? 0.007 : 0));
      ctx.save();
      ctx.translate(pos.x, pos.y);
      ctx.rotate(a + Math.PI / 2);
      this.drawSpikeShape(s.kind, gold, size, s.feintTo != null);
      ctx.restore();
    }
  }

  private drawSpikeShape(
    kind: SpikeKind,
    gold: boolean,
    size: number,
    feint: boolean,
  ): void {
    const ctx = this.ctx;
    ctx.beginPath();
    ctx.moveTo(0, -size);
    ctx.lineTo(size * 0.62, size * 0.72);
    ctx.lineTo(0, size * 0.28);
    ctx.lineTo(-size * 0.62, size * 0.72);
    ctx.closePath();
    if (kind === "ghost") {
      ctx.globalAlpha = 0.52 + Math.sin(this.time * 11) * 0.16;
      ctx.setLineDash([3, 4]);
      ctx.lineWidth = 1.6;
      ctx.strokeStyle = gold ? "rgba(190,210,235,0.95)" : "rgba(140,155,180,0.75)";
      ctx.fillStyle = "rgba(20,24,32,0.35)";
      ctx.fill();
      ctx.stroke();
      ctx.setLineDash([]);
      ctx.globalAlpha = 1;
      return;
    }
    const col = kind === "gold" && gold ? "#f6e7b0" : gold ? "#e8c36a" : "#8a8374";
    ctx.shadowColor = gold ? col : "transparent";
    ctx.shadowBlur = gold ? 16 : 0;
    ctx.fillStyle = col;
    ctx.fill();
    if (kind === "gold" || feint) {
      ctx.shadowBlur = 0;
      ctx.strokeStyle = feint && !gold ? "rgba(232,195,106,0.35)" : "rgba(255,240,200,0.7)";
      ctx.lineWidth = 1.2;
      ctx.stroke();
    }
  }

  private drawCore(S: number): void {
    const ctx = this.ctx;
    const x = this.cx();
    const y = this.cy();
    const r = this.coreR() * (1 + this.pulse * 0.08);
    if (this.mode === "over" && this.shards.length > 0) {
      return;
    }
    ctx.beginPath();
    ctx.arc(x, y, r * 1.6, 0, Math.PI * 2);
    ctx.fillStyle = `rgba(232,195,106,${0.05 + this.goldFlash * 0.12})`;
    ctx.fill();
    ctx.beginPath();
    ctx.arc(x, y, r, 0, Math.PI * 2);
    ctx.fillStyle = "#12110d";
    ctx.fill();
    const lastLife = this.mode === "play" && this.lives === 1;
    ctx.strokeStyle = this.hurtFlash > 0 || lastLife ? "#c45a4a" : "#e8c36a";
    ctx.lineWidth = 2;
    ctx.stroke();
    ctx.beginPath();
    ctx.arc(x, y, r * 0.36, 0, Math.PI * 2);
    ctx.fillStyle = this.goldFlash > 0 ? "#f0d48a" : "#2a261c";
    ctx.fill();
    if (this.streak > 0 && this.mode === "play") {
      ctx.strokeStyle = "rgba(240,199,94,0.55)";
      ctx.lineWidth = 2.2;
      ctx.beginPath();
      ctx.arc(x, y, r * 1.28, -Math.PI / 2, -Math.PI / 2 + (Math.min(this.streak, 7) / 7) * Math.PI * 2);
      ctx.stroke();
    }
    void S;
  }

  private drawShards(): void {
    const ctx = this.ctx;
    for (const s of this.shards) {
      ctx.save();
      ctx.globalAlpha = Math.max(0, s.life / s.max);
      ctx.translate(s.x, s.y);
      ctx.rotate(s.rot);
      ctx.fillStyle = "rgba(220,210,190,0.7)";
      ctx.fillRect(-s.len * 0.5, -2, s.len, 4);
      ctx.restore();
    }
    ctx.globalAlpha = 1;
  }

  private drawParticles(): void {
    const ctx = this.ctx;
    for (const p of this.particles) {
      ctx.globalAlpha = Math.max(0, p.life / p.max);
      ctx.fillStyle = `rgb(${p.r},${p.g},${p.b})`;
      ctx.beginPath();
      ctx.arc(p.x, p.y, p.size, 0, Math.PI * 2);
      ctx.fill();
    }
    ctx.globalAlpha = 1;
  }

  private drawFloaters(S: number): void {
    const ctx = this.ctx;
    ctx.textAlign = "center";
    ctx.font = `700 ${Math.round(S * 0.042)}px "Iowan Old Style", Palatino, Georgia, serif`;
    let i = 0;
    for (const f of this.floaters) {
      const k = f.life / f.max;
      ctx.globalAlpha = clamp(k < 0.15 ? k / 0.15 : k, 0, 1);
      ctx.fillStyle = f.color;
      ctx.save();
      ctx.translate(this.cx(), this.h * 0.2 - (1 - k) * 22 - i * 18);
      ctx.scale(f.scale, f.scale);
      ctx.fillText(f.text, 0, 0);
      ctx.restore();
      i += 1;
    }
    ctx.globalAlpha = 1;
  }
}
